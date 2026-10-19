# Architecture decision records

Each record explains one decision: the context, what we chose, what it costs, and a short **reusable lesson** for anyone building a natural-language-to-SQL agent on SQL Server.

| # | Decision | Status |
|---|---|---|
| [0004](0004-agui-hosting-and-tanstack-ai.md) | AG-UI via Microsoft Agent Framework hosting, consumed by TanStack AI | Accepted |
| [0005](0005-entra-only-authentication.md) | Entra-only authentication everywhere, no SQL logins | Accepted |
| [0007](0007-reuse-existing-azure-resources.md) | Reference existing Azure SQL and Foundry; don't let Aspire provision them | Accepted |

Numbers are reserved for decisions still to be written (0001 semantic layer, 0002 SQL guard, 0003 App Service hosting, 0006 domain and row-level security).
