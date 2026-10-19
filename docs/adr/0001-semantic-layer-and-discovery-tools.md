# 0001. A curated semantic layer with discovery tools

- Status: accepted (Milestones 1–2, 2026-10-19); full-catalog-in-prompt mode to be compared in M5

## Context

Letting a model read the raw schema (`dbo` tables, `INFORMATION_SCHEMA`) means it has to guess business rules: what counts as revenue, which orders to exclude, which time zone a timestamp is in. Those guesses are where NL2SQL answers go wrong.

## Decision

- The model only sees **views in schema `ai`** that encode the rules: `NetRevenue` (after refunds, excluding tax and tips), voided and cancelled orders excluded, local time per location (`LocalDate`, `LocalHour`), `IsLatte` covering hot and iced, `ai.Customers` meaning loyalty members.
- Every view and column has a plain-English **`MS_Description`**, stored in the database (the SQL project), so the catalog has one source of truth.
- **Discovery tools** read it live, as the reader: `list_views`, `describe_view(name)`, and `get_examples(question)` for worked question → SQL pairs in `ai.Examples` (deployed with the schema; every example is run by the safety tests).

## Consequences

- With 7 views the whole catalog would fit in the system prompt. We keep the tools anyway, because they scale to large catalogs; M5 evals compare both modes on accuracy, tokens and latency.
- The first end-to-end runs show the model calling `list_views` → `describe_view` → `get_examples` → `run_query`, and copying the examples' rules (for example `NetRevenue`, not `Subtotal`).
- Observed in testing: asked about "September", the model first guessed the year from its training data, got no rows, and corrected itself. Since M3 the agent gets today's date ([0009](0009-personas-and-demo-limits.md)).
- Also observed: the model looked for `LocationName` on `ai.OrderLines`, got an error and joined to `ai.Orders` instead. M3 added `LocationName` to `ai.OrderLines` and `ai.Refunds`; denormalizing the semantic layer for the questions people actually ask saves round trips.

## Reusable lesson

Don't make the model infer business rules from a raw schema: encode them in views, describe every column in the database, and hand the model worked examples.
