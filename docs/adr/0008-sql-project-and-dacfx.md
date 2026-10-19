# 0008. Schema as a SQL project, deployed with DacFx

- Status: accepted (Milestone 1, 2026-10-19)

## Context

The schema is mostly things SQL Server does natively: views, a security policy and its predicate functions, a role and its grants, and `MS_Description` extended properties that document the semantic layer. We considered numbered SQL scripts, EF Core code-first migrations, DbUp, and SQL projects.

## Decision

A [`Microsoft.Build.Sql`](https://www.nuget.org/packages/Microsoft.Build.Sql) project (`src/Nl2Sql.Database`, SDK 2.3.0) builds a dacpac. The migration service deploys it with DacFx (`Microsoft.SqlServer.DacFx` 170.5.96) using Entra sign-in, then loads the seed from C#.

Deploy options: `BlockOnPossibleDataLoss`, no dropping of objects, permissions or role members that aren't in the project, no database-option changes, and `IncludeTransactionalScripts` so the deploy is one transaction.

## Why not the others

- **EF Core** can't model views, security policies, grants or extended properties, so each would be a raw SQL string in a migration with no build-time check. And the app never reads data through EF: the model writes the SQL.
- **Numbered scripts** have no build-time validation.
- **The SQL project compiles the whole schema on `dotnet build`**, so a view that references a renamed column fails in CI (`SQL71501`) before anything is deployed.

## What we learned

- Builds and deploys on .NET 11 RC2. DacFx is compiled against SqlClient 6.1.5; it works with SqlClient 7.1.1 unified in.
- In an `.slnx` solution the `.sqlproj` needs an explicit `<Build />` element, or the solution build silently skips it.
- Write check constraints the way SQL Server stores them (`IN (...)` becomes reversed `OR`s), or DacFx recreates them on every deploy.
- With `IncludeTransactionalScripts`, a deploy that has to drop and recreate the security policy never exposes the tables without RLS.
- `DropObjectsNotInSource = false` matters on a shared database: `rasoolpur` also holds unrelated tables.

## Reusable lesson

For a SQL Server-first app, a SQL project gives you a compiler for your schema. Pair it with conservative DacFx options and keep data loading in code.
