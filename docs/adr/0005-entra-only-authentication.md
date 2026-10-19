# 0005. Entra-only authentication everywhere, no SQL logins

- Status: accepted (2026-10-19)

## Context

The project must contain no secrets: no API keys, no connection-string passwords, no `.env` files. Azure OpenAI (Foundry) has key authentication disabled, and the Azure SQL server is Entra-only.

The SQL Server Docker image can't accept Entra sign-ins, so a local container would need a SQL login and password.

## Decision

Use Microsoft Entra ID for every connection, locally and in Azure:

- **Locally:** the developer's `az login`, through `DefaultAzureCredential`. The AppHost sets `AZURE_TOKEN_CREDENTIALS=dev` for the API in run mode so it only tries developer credentials.
- **In Azure:** the App Service managed identity.
- **CI:** GitHub OIDC, signed in as the `nl2sql-reader` managed identity (`infra/reader-identity`).
- **No SQL container.** Local development uses the existing Azure SQL database (see [0007](0007-reuse-existing-azure-resources.md)), with `Authentication=Active Directory Default` in the connection string.

## Consequences

- Nobody can clone and run the project without their own Azure SQL database and Foundry resource.
- The serverless free-tier database auto-pauses. The first query after a pause waits while it resumes (about a minute), and anything that polls the database keeps it awake and uses the free monthly allowance. So the API turns off Aspire's SQL health check.
- Locally the developer is the Entra admin, so generated SQL switches to a reader user for each query (see [0006](0006-two-level-row-level-security.md)).

## Reusable lesson

Without `AZURE_TOKEN_CREDENTIALS=dev`, `DefaultAzureCredential` on a laptop first probes the managed-identity endpoint (`169.254.169.254`), which fails slowly and shows up as exceptions in traces. Set it in the AppHost for local runs. And disable database health checks against serverless databases you don't want kept awake.
