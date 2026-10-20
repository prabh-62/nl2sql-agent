# 0003. App Service F1, deployed with our own Bicep from GitHub Actions

- Status: accepted (Milestone 6, 2026-10-20). Replaces the original plan of deploying with Aspire.
- Live: https://ms-sql-nl2sql-agent.azurewebsites.net

## Context

The demo must cost nothing, run next to the free-tier database (westus2), keep "no secrets" (ADR 0005), and not let any deploy touch the database or Foundry (ADR 0007). The original plan was to deploy with Aspire's App Service integration. Its defaults don't fit: it deploys containers (so it needs a registry), uses a Premium P0v3 plan, creates its own identity and role assignments, and would redeclare the database and add a model deployment.

## Decision

- **Hosting:** one Linux App Service on the **F1 (Free)** plan serves the API and the built React app from the same origin. Container Apps and other hosts were not considered further.
- **Infrastructure as code we own:** `infra/app/main.bicep` (plan, web app, settings). The app runs as the existing **`nl2sql-reader`** managed identity: SELECT on the `ai` views and OpenAI User on Foundry, nothing else, and the same identity CI tests. HTTPS only, TLS 1.2+, FTP and basic-auth publishing disabled.
- **No container, no registry:** CI publishes the API with the React build in `wwwroot` and zip-deploys it.
- **Two identities, two places:** `infra/bootstrap` (the reader and deployer identities, their roles and GitHub OIDC credentials) is applied once by an Owner from their machine. Everything else is deployed by `.github/workflows/deploy.yml` after CI passes on `main`, signed in as **`nl2sql-deployer`**. That identity can only manage web apps and plans in the resource group and attach `nl2sql-reader`: no access to the SQL server, the database, Foundry or data.
- **Schema migrations stay manual**, run from a developer's machine (Gagan's call): rare, and the database is shared.
- **Smoke test** after every deploy: wait out the cold start, load the page, and ask one real question as a persona (proving the managed identity reaches both the database and Foundry).
- Aspire remains the local development orchestrator.

## What we learned deploying

- **App Service's .NET 11 stack (`DOTNET|11`) had RC1 while the app targets RC2.** The app exited at startup ("You must install or update .NET"), App Service restarted it in a loop, and on F1 that used up the CPU quota and suspended the site (`QuotaExceeded`). We could only read the logs after briefly scaling to B1. **Fix:** publish **self-contained** for linux-x64 (about 170 MB) and start the app's own executable. Revert to framework-dependent once App Service ships RC2 or GA.
- On F1, a crash loop is expensive: the quota is counted in CPU time, and suspension hides the logs. Reading `LogFiles/*_docker.log` through Kudu with an Entra token works when basic auth is off.
- A GitHub **environment** on the deploy job would change the OIDC subject (`environment:production` instead of `ref:refs/heads/main`), so the deploy job doesn't use one.
- The client IP behind App Service arrives in `X-Forwarded-For`; the per-IP quota works with `ForwardLimit = 1` (verified: the live quota counts down per visitor).
- The serverless database auto-pauses; connections now retry for up to about a minute while it resumes.

## Consequences

- Free, but F1 has no Always On: after about 20 idle minutes the app sleeps, and the next visitor waits for a cold start. There are 60 CPU minutes a day; the demo cap (200 questions) keeps us well under it.
- In-memory quota counters reset when the app restarts (ADR 0009).
- For production use, point the agent at a nightly restored copy or a read replica of the database, never the primary (to be covered in the README).

## Reusable lesson

For a small .NET app on App Service, a 100-line Bicep file plus a zip deploy is simpler and more auditable than a generated pipeline. Give the deploy identity only the rights to the resources it deploys. Check the platform's exact runtime version before targeting a preview .NET.
