# 0007. Reference existing Azure SQL and Foundry; don't let Aspire provision them

- Status: accepted for local runs (Milestone 0); publish wiring to be finished in M6

## Context

We reuse an existing **free-tier** serverless Azure SQL database and an existing Foundry resource with its model deployments. Neither may be recreated, resized or extended by a deploy.

Aspire 13.6's Azure integrations offer `RunAsExisting` and `PublishAsExisting`. We tried them in the spike and inspected what `aspire publish` generates (`sql.bicep`, `foundry.bicep`, `appservice-acr.bicep`, role-assignment modules), without deploying anything.

## What `aspire publish` generated

With `AddAzureSqlServer(...).PublishAsExisting(...)`, `AddDatabase("db", "rasoolpur")`, `AddAzureOpenAI(...).PublishAsExisting(...)`, `AddDeployment(...)` and `AddAzureAppServiceEnvironment(...)`:

1. **The database is re-declared, not referenced.** The server is `existing`, but `rasoolpur` is a full `Microsoft.Sql/servers/databases` resource with `sku: GP_S_Gen5_2`, `useFreeLimit: true` and `freeLimitExhaustionBehavior: AutoPause`. A read-only `az deployment group what-if` shows the only difference from the live database is the SKU spelling (`GP_S_Gen5` with capacity 2 becomes `GP_S_Gen5_2`, the same size). Min capacity, auto-pause delay and max size are untouched. It's still a write to a database we don't want Aspire to own.
2. **A firewall rule is added** (`AllowAllAzureIps`) to the existing server.
3. **A new model deployment is created.** `AddDeployment("chat", "gpt-5.4-mini", ...)` emits a new deployment named `chat` (`Standard`, capacity 8) instead of referencing the existing `gpt-5.4-mini` Global Standard deployment. `what-if` confirms it as a Create.
4. **App Service needs a container registry.** The App Service environment adds an Azure Container Registry (Basic SKU, about US$0.17 a day, roughly $5 a month in westus2) and deploys the API as a container image, not as code.
5. A role assignment (Cognitive Services OpenAI User) is added for the API's managed identity on the Foundry resource. That one we want.

`RunAsExisting` runs the same kind of provisioning during `aspire run`, so it would make these changes from a developer's laptop too.

## Decision

For local runs, the AppHost doesn't use the Azure provisioning integrations at all. It builds plain connection strings from non-secret parameters:

```csharp
var db = builder.AddConnectionString("db", ReferenceExpression.Create(
    $"Server=tcp:{sqlServerName}.database.windows.net,1433;Database={sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True"));
var foundry = builder.AddConnectionString("foundry", ReferenceExpression.Create(
    $"Endpoint=https://{foundryName}.cognitiveservices.azure.com/;Deployment={chatDeployment}"));
```

Nothing in Azure is created or changed by `aspire run`.

## Open for M6

- Make the database and model deployment `existing` in the published Bicep (e.g. via `ConfigureInfrastructure`), or keep connection strings and add only the identity and role assignments.
- **Registry: ghcr.io (decided 2026-10-19).** It's free for public images, so we skip ACR. M6 must replace the ACR that Aspire's App Service environment adds by default with GitHub Container Registry, and confirm App Service **F1** on Linux can pull and run the public image.

## Reusable lesson

`PublishAsExisting`/`RunAsExisting` on a server doesn't make its child resources existing. Always run `aspire publish` to a local folder, read the Bicep and run `az deployment group what-if` on it before any deploy touches shared or free-tier resources.
