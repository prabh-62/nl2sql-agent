# 0006. Two-level row-level security with read-only session context

- Status: accepted (Milestone 1, 2026-10-19)

## Context

Every tenant is a café brand with several locations. A brand owner may see the whole brand; a location manager only their location. The SQL the model writes must never be able to see, or widen its way into, another tenant's rows, and it must never write anything.

## Decision

**Row-level security on the base tables** (`security.TenantPolicy`), with filter predicates that read session context:

| Key | Owner | Manager | Missing |
|---|---|---|---|
| `BrandId` | brand | brand | no rows |
| `LocationId` | `0` (whole brand) | location ID | no rows |

Every tenant table carries its own `BrandId` and `LocationId` columns, so predicates need no joins. The model only reads views in schema `ai`; RLS applies through them because they read the filtered base tables.

**Per connection** (`Nl2Sql.Data.ReaderConnection`):

1. Locally only: `EXECUTE AS USER = 'nl2sql_reader_dev' WITH COOKIE INTO @cookie`.
2. `sp_set_session_context` for `BrandId` and `LocationId` with `@read_only = 1`.
3. Check that the effective user is in the `nl2sql_reader` role and isn't `db_owner`, or refuse.
4. On dispose, `REVERT WITH COOKIE` before the connection goes back to the pool.

**Principals:**

- Role `nl2sql_reader`: `SELECT` on schema `ai`. Nothing else.
- `nl2sql-reader`: a user-assigned managed identity, the database user App Service (M6) and CI sign in as. Added to the role by the migration service from its client ID (`CREATE USER ... WITH SID = ..., TYPE = E`), so the server needs no Microsoft Graph permissions.
- `nl2sql_reader_dev`: a user without login in the same role. Locally the developer signs in as an Entra admin and switches to it for each query, so local runs get exactly the reader's permissions. Nobody can sign in as it.

## What the spike and the safety tests showed (Azure SQL, 2026-10-19)

- Users created by DacFx get **no `CONNECT`** permission, and `EXECUTE AS USER` then fails with error 916. The project grants it explicitly.
- With `@read_only = 1`, the reader can't change `BrandId` or `LocationId`, and can't `REVERT` without the cookie.
- **A key set to `NULL` isn't locked by `@read_only`.** Our first design used `LocationId = NULL` for owners; the safety tests caught that the key could then be set later. Owners now get `0`, and the predicate treats a missing key as no access.
- Pooled connections come back with no session context (the pool's reset clears it). The impersonation is reverted explicitly with the cookie before that; if the revert fails, the pool is cleared rather than reused.
- **Admins are filtered too.** RLS has no exemption for `dbo`, so an admin with no session context sees no tenant rows. The seeder therefore checks `dbo.SeedHistory` (not a tenant table) and switches the policy off inside its transaction while it deletes old data.

## Consequences

- The safety tests (`tests/Nl2Sql.SafetyTests`) run the same code path locally (as the developer, switching to `nl2sql_reader_dev`) and in CI (as the `nl2sql-reader` identity, signed in with GitHub OIDC).
- In production the app identity has no rights to switch users at all.
- A manager sees loyalty members whose home location is theirs, and only the orders placed at their location.

## Reusable lesson

For multi-tenant NL2SQL on SQL Server: put RLS on the base tables, expose only views, set tenant keys in read-only session context, and make every key fail closed (never `NULL`). Test it with the same connection code the app uses, against a real database, as a non-admin identity.
