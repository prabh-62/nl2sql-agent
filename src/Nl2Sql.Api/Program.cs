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
builder.Services.AddSingleton<DatabaseTools>();
builder.Services.AddSingleton(services => CreateChatClient(builder.Configuration, builder.Environment));

var app = builder.Build();

app.UseExceptionHandler();

var tools = app.Services.GetRequiredService<DatabaseTools>();
var agent = app.Services.GetRequiredService<IChatClient>().AsAIAgent(
    name: "nl2sql",
    instructions: """
        You help café owners and managers understand their coffee-ordering data.
        When asked about the database, call the ping_db tool and summarise what it returns.
        """,
    tools: [AIFunctionFactory.Create(tools.PingDbAsync, name: "ping_db")],
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
