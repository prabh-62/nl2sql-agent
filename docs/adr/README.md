# Architecture decision records

Each record explains one decision: the context, what we chose, what it costs, and a short **reusable lesson** for anyone building a natural-language-to-SQL agent on SQL Server.

| # | Decision | Status |
|---|---|---|
| [0001](0001-semantic-layer-and-discovery-tools.md) | A curated semantic layer with discovery tools | Accepted |
| [0002](0002-deterministic-sql-guard.md) | A deterministic SQL guard plus a read-only user, not a classifier | Accepted |
| [0003](0003-app-service-f1-with-bicep.md) | App Service F1, deployed with our own Bicep from GitHub Actions | Accepted |
| [0004](0004-agui-hosting-and-tanstack-ai.md) | AG-UI via Microsoft Agent Framework hosting, consumed by TanStack AI; shadcn/ui and AI Elements without the Vercel AI SDK | Accepted |
| [0005](0005-entra-only-authentication.md) | Entra-only authentication everywhere, no SQL logins | Accepted |
| [0006](0006-two-level-row-level-security.md) | Two-level row-level security with read-only session context | Accepted |
| [0007](0007-reuse-existing-azure-resources.md) | Reference existing Azure SQL and Foundry; don't let Aspire provision them | Accepted |
| [0008](0008-sql-project-and-dacfx.md) | Schema as a SQL project, deployed with DacFx | Accepted |
| [0009](0009-personas-and-demo-limits.md) | Demo personas, agent limits and the public demo's quota | Accepted |
| [0010](0010-charts-from-run-query.md) | Charts are suggested in the same call as the query | Accepted |
| [0011](0011-evals-by-result-set.md) | Evals compare result sets, per model and catalog mode | Accepted |

