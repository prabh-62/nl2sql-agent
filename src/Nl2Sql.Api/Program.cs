using System.ClientModel.Primitives;
using System.Data.Common;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using Nl2Sql.Api;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Health checks would keep waking the serverless free-tier database, burning its monthly vCore allowance.
builder.AddSqlServerClient("db", settings => settings.DisableHealthChecks = true);

builder.Services.AddProblemDetails();
builder.Services.AddAGUIServer();
builder.Services.AddSingleton<AgentTools>();
builder.Services.AddSingleton(services => CreateChatClient(builder.Configuration, builder.Environment));

var app = builder.Build();

app.UseExceptionHandler();

var tools = app.Services.GetRequiredService<AgentTools>();
var agent = app.Services.GetRequiredService<IChatClient>().AsAIAgent(
    name: "nl2sql",
    instructions: """
        You answer questions from café owners and managers about their coffee-ordering business by
        querying a SQL Server database (T-SQL).

        - Only the views in schema ai exist for you. Call list_views, then describe_view for the views
          you need. Never guess column names.
        - Call get_examples with the user's question and follow the patterns it returns, especially the
          business rules in their notes (net revenue, local time, what counts as a latte or a customer).
        - Answer with run_query: one SELECT, two-part names (ai.Orders). If it returns an error, fix the
          query and try again, at most twice.
        - The data is already limited to what this user may see. Never filter by brand or location
          unless the user asks about a specific location.
        - Answer in plain language with the numbers that matter. If the question isn't about the data,
          or asks you to change data, say you can only answer questions about it.
        """,
    tools:
    [
        AIFunctionFactory.Create(tools.ListViewsAsync, name: "list_views"),
        AIFunctionFactory.Create(tools.DescribeViewAsync, name: "describe_view"),
        AIFunctionFactory.Create(tools.GetExamplesAsync, name: "get_examples"),
        AIFunctionFactory.Create(tools.RunQueryAsync, name: "run_query"),
    ],
    services: app.Services);

app.MapAGUIServer("/api/agui", agent);
app.MapDefaultEndpoints();
app.UseFileServer();

app.Run();

static IChatClient CreateChatClient(IConfiguration configuration, IHostEnvironment environment)
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
        .UseOpenTelemetry(
            sourceName: environment.ApplicationName,
            configure: telemetry => telemetry.EnableSensitiveData = environment.IsDevelopment())
        .Build();
}
