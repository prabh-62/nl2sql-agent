using System.ClientModel.Primitives;
using System.Data.Common;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.AI;
using Nl2Sql.Api;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Health checks would keep waking the serverless free-tier database, burning its monthly vCore allowance.
builder.AddSqlServerClient("db", settings => settings.DisableHealthChecks = true);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(builder.Configuration.GetSection("Quota").Get<QuotaOptions>() ?? new QuotaOptions());
builder.Services.AddSingleton<QuestionQuota>();
builder.Services.AddAGUIServer();
builder.Services.AddSingleton<AgentTools>();
builder.Services.AddSingleton(services => CreateChatClient(builder.Configuration, builder.Environment, services));

// Behind App Service the client IP arrives in X-Forwarded-For. Its front end appends the real
// address last, so trusting exactly one hop can't be spoofed by a client-supplied header.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

var tools = app.Services.GetRequiredService<AgentTools>();
var agent = new ChatClientAgent(
    app.Services.GetRequiredService<IChatClient>(),
    new ChatClientAgentOptions
    {
        Name = "nl2sql",
        // The chat client below already has its own function-invocation loop with our limits.
        UseProvidedChatClientAsIs = true,
        ChatOptions = new ChatOptions
        {
            Instructions = """
                You answer questions from café owners and managers about their coffee-ordering business by
                querying a SQL Server database (T-SQL).

                - Only the views in schema ai exist for you. Call list_views, then describe_view for the views
                  you need. Never guess column names.
                - Call get_examples with the user's question and follow the patterns it returns, especially the
                  business rules in their notes (net revenue, local time, what counts as a latte or a customer).
                - Answer with run_query: one SELECT, two-part names (ai.Orders). If it returns an error, fix the
                  query and try again; you have 2 retries.
                - The data is already limited to what this user may see. Never filter by brand or location
                  unless the user asks about a specific location.
                - Answer in plain language with the numbers that matter. If the question isn't about the data,
                  or asks you to change data, say you can only answer questions about it.
                """,
            MaxOutputTokens = builder.Configuration.GetValue("Agent:MaxOutputTokens", 2000),
            Tools =
            [
                AIFunctionFactory.Create(tools.ListViewsAsync, name: "list_views"),
                AIFunctionFactory.Create(tools.DescribeViewAsync, name: "describe_view"),
                AIFunctionFactory.Create(tools.GetExamplesAsync, name: "get_examples"),
                AIFunctionFactory.Create(tools.RunQueryAsync, name: "run_query"),
            ],
        },
    },
    app.Services.GetRequiredService<ILoggerFactory>(),
    app.Services);

// Every question passes the gate (persona, length, quota) before it reaches the agent.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/agui"),
    branch => branch.UseMiddleware<QuestionGate>());
app.MapAGUIServer("/api/agui", agent);

app.MapGet("/api/personas", () => Personas.All.Select(p => new { p.Id, p.Name, p.Description }));
app.MapGet("/api/quota", (HttpContext context, QuestionQuota quota) =>
{
    var (remaining, limit, demoPaused) = quota.Remaining(QuestionGate.ClientKey(context));
    return new { remaining, limit, demoPaused };
});

app.MapDefaultEndpoints();
app.UseFileServer();

app.Run();

static IChatClient CreateChatClient(IConfiguration configuration, IHostEnvironment environment, IServiceProvider services)
{
    // "Endpoint=https://<resource>.cognitiveservices.azure.com/;Deployment=<name>", set by the AppHost.
    var settings = new DbConnectionStringBuilder
    {
        ConnectionString = configuration.GetConnectionString("foundry")
            ?? throw new InvalidOperationException("Connection string 'foundry' is missing."),
    };
    var endpoint = new Uri(new Uri((string)settings["Endpoint"]), "openai/v1/");
    var deployment = (string)settings["Deployment"];

    // Entra only: Azure CLI login locally, managed identity in Azure. Key auth is disabled on the resource.
    var authentication = new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default");

    // The Responses API, not Chat Completions: reasoning models such as gpt-6-luna only accept
    // function tools there.
#pragma warning disable OPENAI001 // Token-credential constructor and Responses client are marked experimental in the OpenAI SDK.
    var client = new OpenAIClient(authentication, new OpenAIClientOptions { Endpoint = endpoint });
    var responses = client.GetResponsesClient().AsIChatClient(deployment);
#pragma warning restore OPENAI001

    return responses
        .AsBuilder()
        // Outermost: who the user is and today's date, once per question.
        .Use(inner => new AgentContextChatClient(inner, services.GetRequiredService<IHttpContextAccessor>(), services.GetRequiredService<TimeProvider>()))
        // At most 5 model round trips per question; the last one can't call tools, so it must answer.
        .UseFunctionInvocation(configure: invoker => invoker.MaximumIterationsPerRequest = configuration.GetValue("Agent:MaxIterations", 5))
        .UseOpenTelemetry(
            sourceName: environment.ApplicationName,
            configure: telemetry => telemetry.EnableSensitiveData = environment.IsDevelopment())
        .Build(services);
}
