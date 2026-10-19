# 0004. AG-UI via Microsoft Agent Framework hosting, consumed by TanStack AI

- Status: accepted (Milestone 0 spike, 2026-10-19)

## Context

The browser needs a streaming protocol that carries text, tool calls and tool results from the .NET agent. [AG-UI](https://github.com/ag-ui-protocol/ag-ui) is an open, MIT-licensed event protocol over SSE. We want no CopilotKit runtime and nothing paid.

Two .NET server options exist:

| Package | Version tested | Notes |
|---|---|---|
| `AGUI.Server` (AG-UI protocol org) | 1.0.1, stable | Converts a `Microsoft.Extensions.AI` chat stream into AG-UI events. Two dependencies. |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` (Microsoft) | 1.25.0-preview.261014.1 | `AddAGUIServer()` + `MapAGUIServer(path, agent)`. Depends on `AGUI.Server` itself and hosts an Agent Framework `AIAgent`. |

On the client, `@tanstack/ai-react` 0.30.4 (`useChat` + `fetchServerSentEvents`) sends an AG-UI `RunAgentInput` and parses AG-UI events natively.

## Decision

Use Microsoft's `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`, pinned to an exact preview version, with an Agent Framework agent built from an `IChatClient`:

```csharp
builder.Services.AddAGUIServer();
var agent = chatClient.AsAIAgent(name: "nl2sql", instructions: "...", tools: [...]);
app.MapAGUIServer("/api/agui", agent);
```

On the client:

```ts
const { messages, sendMessage } = useChat({ connection: fetchServerSentEvents("/api/agui") });
```

## What the spike showed

It works end to end with no adapter code. One question produced this event sequence:

```
RUN_STARTED → TOOL_CALL_START (ping_db) → TOOL_CALL_ARGS → TOOL_CALL_END → TOOL_CALL_RESULT
→ TEXT_MESSAGE_START → TEXT_MESSAGE_CONTENT × 55 → TEXT_MESSAGE_END → RUN_FINISHED
```

TanStack rendered the text plus `tool-call` and `tool-result` parts, and a follow-up question carried the earlier tool call and result back to the server correctly. The Aspire dashboard shows `POST /api/agui → chat gpt-5.4-mini → execute_tool ping_db → SELECT`.

Things to know:

- Every event carries a `rawEvent` field with the underlying Agent Framework update (model id, function-call contents). It's useful while developing, but before going public we must check what it exposes and strip it if needed (M3/M4).
- The tool name comes from the method name (`PingDb`) unless you pass `name:` to `AIFunctionFactory.Create`.
- `ChatClientAgent` adds function invocation itself; no `UseFunctionInvocation()` needed.

## Consequences

- We track a weekly preview package. Pin the exact version and re-test on upgrade.
- If Microsoft's hosting layer gets in the way, dropping down to `AGUI.Server` directly is a small change, since it's the same protocol code underneath.

## Reusable lesson

A .NET `IChatClient` agent and a TanStack AI React client talk AG-UI out of the box: `MapAGUIServer` on the server, `fetchServerSentEvents` on the client. Name your tools explicitly and look at `rawEvent` before shipping.
