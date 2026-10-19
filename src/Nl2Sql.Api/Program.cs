using Azure.Identity;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Nl2Sql.Agent;
using Nl2Sql.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Health checks would keep waking the serverless free-tier database, burning its monthly vCore allowance.
builder.AddSqlServerClient("db", settings => settings.DisableHealthChecks = true);

var agentOptions = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(builder.Configuration.GetSection("Quota").Get<QuotaOptions>() ?? new QuotaOptions());
builder.Services.AddSingleton<QuestionQuota>();
builder.Services.AddSingleton<IAgentRunAccessor, HttpAgentRunAccessor>();
builder.Services.AddSingleton(new AgentToolsOptions(
    builder.Configuration.GetConnectionString("db") ?? throw new InvalidOperationException("Connection string 'db' is missing."),
    builder.Configuration["Database:ImpersonateUser"]));
builder.Services.AddSingleton<AgentTools>();
builder.Services.AddAGUIServer();

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

// Entra only: Azure CLI login locally, managed identity in Azure. Key auth is disabled on Foundry.
var model = AgentFactory.CreateFoundryChatClient(
    builder.Configuration.GetConnectionString("foundry") ?? throw new InvalidOperationException("Connection string 'foundry' is missing."),
    new DefaultAzureCredential());
var pipeline = AgentFactory.BuildPipeline(
    model,
    app.Services.GetRequiredService<IAgentRunAccessor>(),
    app.Services.GetRequiredService<TimeProvider>(),
    agentOptions,
    builder.Environment.ApplicationName,
    recordSensitiveData: builder.Environment.IsDevelopment(),
    app.Services);
var agent = await AgentFactory.CreateAsync(
    pipeline,
    app.Services.GetRequiredService<AgentTools>(),
    agentOptions,
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

await app.RunAsync();
