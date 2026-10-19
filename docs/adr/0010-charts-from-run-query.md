# 0010. Charts are suggested in the same call as the query

- Status: accepted (Milestone 4, 2026-10-19)

## Context

Some answers read better as a chart: a trend over months, or a comparison across a few locations. Something has to decide whether to draw one and how. Options considered with Gagan:

1. **An AG-UI frontend tool** (`render_chart`) declared by the browser and called by the model. AG-UI-native, but Microsoft's AG-UI hosting with client-declared tools is unverified.
2. **A server-side chart tool** the model calls after the query. Proven path, but one more model round trip, and questions are already capped at 5.
3. **An optional `chart` argument on `run_query`.**

## Decision

Option 3, a server-side variant of option 2: `run_query(sql, chart?)` where `chart` is `{ type: "bar" | "line", title, x, y[1..3] }`. The model knows which columns it's selecting, so it can say how to plot them in the same call.

- The server validates the suggestion against the actual result (`ChartSuggestion.Validate`): known columns, numeric y values, at least two rows. A bad suggestion never fails the query; it's dropped and the reason returned as `chartError`.
- The browser validates it again with Zod (`chartSchema`), builds the points (`chart-data.ts`) and draws it with Recharts through shadcn's chart component. Month columns are labelled "Sep 2026".
- The table shows the same rows the model sees (up to 50), with "Showing 50 of N" when there are more.

## Consequences

- No extra round trip for charts, and no client-side tool plumbing.
- The model decides on the chart before it sees the numbers; the server's check catches charts that don't fit.

## Reusable lesson

When a UI decision only depends on the shape of a query's result, let the model describe it as part of the query call and validate it on both sides, instead of spending another agent turn.
