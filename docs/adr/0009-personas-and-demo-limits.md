# 0009. Demo personas, agent limits and the public demo's quota

- Status: accepted (Milestone 3, 2026-10-19)

## Context

The public demo has no real sign-in, runs on a free App Service instance with a free database, and pays per model token. Each question must run as exactly one tenant, must not be able to run away (in iterations, tokens or retries), and the demo as a whole must stay within a daily budget.

## Decision

**Personas instead of sign-in.** The browser sends only a persona ID in an `X-Persona` header (`tim-hortons-owner`, `tim-hortons-queen-st`, `starbucks-owner`, `dunkin-owner`). The server maps it to a `TenantScope`; a client can never send a brand or location ID. Tools read the persona from the current request (`IHttpContextAccessor`), so every connection they open is scoped to it.

**A gate in front of the agent** (`QuestionGate` middleware on `/api/agui`), cheapest check first, so bad requests never cost tokens or quota:

1. known persona (400 otherwise);
2. a request body of at most 64 KB with a user question of at most 500 characters (400/413);
3. the quota (429): **10 questions per client IP per day** and **200 per day overall** ("demo paused"). Built on `System.Threading.RateLimiting` fixed windows; `GET /api/quota` returns what's left for the UI. Counters are in memory and reset when the app restarts, which is fine for one F1 instance. Local development gets much higher limits.

The client IP comes from `X-Forwarded-For` behind App Service, trusting exactly one hop (`ForwardLimit = 1`): the front end appends the real address last, so a client can't spoof it.

**Agent limits:**

- at most **5 model round trips** per question (`FunctionInvokingChatClient.MaximumIterationsPerRequest`); the last round can't call tools, so the model has to answer;
- at most **2 failed queries** per question, after which `run_query` tells the model to stop and explain;
- **2,000 output tokens** per model call.

**Context on every request** (`AgentContextChatClient`): who the user is and today's date in their time zone. Before this, asked about "September", the model assumed the year from its training data.

## Consequences

- Agent Framework's own function loop is turned off (`UseProvidedChatClientAsIs`), so the one in our `IChatClient` pipeline, with our limits, is the only one.
- 5 round trips is tight when the model also retries a query (list → describe → examples → query → retry). M5's evals will show whether that costs accuracy, and whether putting the small catalog in the prompt helps.

## Reusable lesson

Map identity on the server and never accept tenant IDs from the client. Put every cost limit (iterations, retries, tokens, requests) in code before the model runs, and give the model today's date.
