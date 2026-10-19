# nl2sql-agent

Ask a coffee chain's sales data questions in plain English and get answers, the SQL behind them, and charts. A reference implementation of a multi-tenant natural-language-to-SQL agent on SQL Server: a curated semantic layer, a deterministic SQL guard, row-level security, a read-only database identity, and evals.

Built with .NET 11, Aspire, Microsoft Agent Framework (AG-UI), React with TanStack AI, Azure SQL and Azure AI Foundry. Design decisions are in [`docs/adr`](docs/adr).

> **Disclaimer:** the demo uses the names Tim Hortons, Starbucks and Dunkin' to make the scenarios relatable. **All data is invented** by a random generator: orders, revenue, menus, prices, tips, refunds and customers. None of it reflects these companies' real business, and this project is not affiliated with, endorsed by or sponsored by any of them. All trademarks belong to their owners.

_Full documentation (architecture, how to run and deploy, eval results) is coming with the last milestone._
