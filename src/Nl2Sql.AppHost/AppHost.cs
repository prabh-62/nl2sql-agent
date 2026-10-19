var builder = DistributedApplication.CreateBuilder(args);

// Existing Azure resources, referenced by name only. Nothing here provisions or changes them:
// Aspire's Azure SQL and Azure OpenAI integrations would re-deploy the free-tier database and
// create a new model deployment, even with RunAsExisting (see docs/adr/0003-app-service-publish.md).
// Authentication is Entra-only everywhere: locally your `az login`, in Azure a managed identity.
var sqlServerName = builder.AddParameter("sqlServerName");
var sqlDatabaseName = builder.AddParameter("sqlDatabaseName");
var foundryName = builder.AddParameter("foundryName");
var chatDeployment = builder.AddParameter("chatDeployment");

var db = builder.AddConnectionString("db", ReferenceExpression.Create(
    $"Server=tcp:{sqlServerName}.database.windows.net,1433;Database={sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;Connect Timeout=60"));

var foundry = builder.AddConnectionString("foundry", ReferenceExpression.Create(
    $"Endpoint=https://{foundryName}.cognitiveservices.azure.com/;Deployment={chatDeployment}"));

var api = builder.AddProject<Projects.Nl2Sql_Api>("api")
    .WithReference(db)
    .WithReference(foundry)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (builder.ExecutionContext.IsRunMode)
{
    // Locally, only try developer credentials (Azure CLI etc.). Otherwise DefaultAzureCredential
    // first probes the managed-identity endpoint, which isn't there and adds seconds per token.
    api.WithEnvironment("AZURE_TOKEN_CREDENTIALS", "dev");
}

builder.AddViteApp("web", "../web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
