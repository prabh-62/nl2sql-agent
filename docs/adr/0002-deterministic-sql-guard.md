# 0002. A deterministic SQL guard plus a read-only user, not a classifier

- Status: accepted (Milestone 2, 2026-10-19)

## Context

The model writes SQL from untrusted input (the user's question, and anything a prompt injection slips into it). Before that SQL runs, something must decide whether it's allowed. Options: ask another model (for example the `MS-Decision-1` classifier deployed in Foundry) whether the query is safe, or check it with a parser.

## Decision

`Nl2Sql.Data.SqlGuard` parses the query with Microsoft's T-SQL parser (ScriptDom, `TSql180Parser`) and allows it only if it's **exactly one `SELECT`** that reads **only views in schema `ai`**. It's an allow-list:

- Table sources: two-part `ai.*` names, CTEs, derived tables, joins, `VALUES`, `PIVOT`/`UNPIVOT`. Everything else (`OPENROWSET`, `OPENQUERY`, `OPENDATASOURCE`, `OPENJSON`, table-valued functions, table variables) is rejected.
- No three- or four-part names, no other schema (`dbo`, `sys`, `INFORMATION_SCHEMA`, `security`).
- No `SELECT ... INTO`, no multiple statements or batches, no variables or `@@` system variables, no `NEXT VALUE FOR`, no table or query hints, no schema-qualified function calls, and no built-in functions that reveal metadata, identity, security or session state (`OBJECT_DEFINITION`, `SESSION_CONTEXT`, `USER_NAME`, `IS_ROLEMEMBER`, ...).
- Error messages are written for the model, so it can fix its query ("'Orders' needs its schema: write ai.Orders.").

Then `QueryRunner` compiles the query with `sp_describe_first_result_set` (as the reader), runs it with a 15-second timeout, and stops reading after 500 rows.

## Why not a classifier

A classifier is probabilistic: it's right most of the time, and an attacker only needs the one time it isn't. A parser gives the same answer for the same input, can be unit-tested exhaustively (`tests/Nl2Sql.UnitTests/SqlGuardTests.cs` has the evasion attempts: comments, casing, `;` and `GO` batching, `INTO`, `EXEC`, linked-server names, rowset functions), and its failures are bugs we can fix.

A classifier may still be useful for user experience, such as spotting off-topic questions before spending tokens (PLAN §8), but it is never a security boundary.

## Defence in depth

The guard is one of three independent layers. If it ever let something through:

1. the database user can only `SELECT` from schema `ai` (no writes, no DDL, no `dbo`), and
2. row-level security limits the rows to the user's tenant ([0006](0006-two-level-row-level-security.md)).

Any one of the three would stop a `DROP TABLE` on its own.

## Reusable lesson

Parse model-generated SQL with the database vendor's own parser and allow only the shapes you expect. Write the rejection messages for the model. And never make the guard the only layer.
