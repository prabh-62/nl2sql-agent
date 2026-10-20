# 0011. Evals compare result sets, per model and catalog mode

- Status: accepted (Milestone 5, 2026-10-19)

## Context

"The answer looks plausible" isn't a measure. We need to know how often the agent returns the *right rows*, and whether a cheaper model or a different way of showing it the schema changes that.

## Decision

`tests/Nl2Sql.Evals` (run with `dotnet run --project tests/Nl2Sql.Evals`, or the manual `evals` workflow):

- **26 golden cases** (`cases.json`): 16 *unseen* questions (no worked example), 5 *paraphrases* of `ai.Examples` questions (scored separately, because the examples leak their SQL), 2 *isolation* cases and 3 *refusals* (delete, prompt injection, off-topic).
- **Scoring by result set** (`ResultComparer`): the agent's last successful query vs. a reference query run as the same persona with the same "today". Reference columns are matched to agent columns by value (names don't matter, extra columns are fine), rows as a multiset unless ordered, numbers within 0.5%, and a share may be a fraction or a percentage. Every reference query is checked to return data (`--check-references`).
- **Isolation and refusals must pass**: no forbidden value in any result, and no query for a refusal. If one fails, the run fails.
- **The evals run the shipped agent**: the agent setup moved into `src/Nl2Sql.Agent`, shared by the API and the evals, with the persona carried by an `AgentRun` instead of the HTTP request.
- **Two catalog modes** (`Agent:CatalogMode`): *Discovery* (`list_views` → `describe_view`) and *Prompt* (every view, column and description in the system prompt).
- **Manual only** (Gagan's call): a full run is 104 agent runs. CI signs in as the `nl2sql-reader` identity, which got one more role for this: Cognitive Services OpenAI User on the Foundry resource.

## Results (GitHub Actions, 2026-10-19)

First run:

| Model | Catalog | Accuracy | Must-pass | Median latency | Avg tokens in / out | Avg model calls |
|---|---|---|---|---|---|---|
| gpt-5.4-mini | Discovery | 100% (21/21) | 5/5 | 9.5 s | 6,105 / 257 | 3.8 |
| gpt-5.4-mini | Prompt | 100% (21/21) | 5/5 | 6.5 s | 7,845 / 217 | 2.8 |
| gpt-6-luna | Discovery | 95% (20/21) | 5/5 | 11.8 s | 6,312 / 322 | 3.8 |
| gpt-6-luna | Prompt | 90% (19/21) | 5/5 | 9.3 s | 7,720 / 225 | 2.8 |

The misses were worth more than the scores:

- **Oat milk share:** `gpt-6-luna` weighted by `Quantity`; the reference and the worked example counted order lines. Luna was right. Both now count items, and the example's note says so. (`gpt-5.4-mini` "passed" by copying the example.)
- **Loyalty members:** a model counted every `CustomerId`, which includes accounts that aren't loyalty members. Re-running showed `gpt-5.4-mini` making the same mistake in Prompt mode. The column description was the culprit ("Customer who placed the order"); it now says to filter `IsLoyaltyMember = 1`.

Second run, after those two fixes:

| Model | Catalog | Accuracy | Unseen | Paraphrase | Must-pass | Median / p95 latency | Avg tokens in / out | Avg model calls | Hit call limit |
|---|---|---|---|---|---|---|---|---|---|
| gpt-5.4-mini | Discovery | 100% (21/21) | 16/16 | 5/5 | 5/5 | 8.4 s / 12.9 s | 5,926 / 245 | 3.7 | 4 |
| gpt-5.4-mini | Prompt | 100% (21/21) | 16/16 | 5/5 | 5/5 | 6.0 s / 7.5 s | 7,934 / 218 | 2.8 | 1 |
| gpt-6-luna | Discovery | 95% (20/21) | 15/16 | 5/5 | 5/5 | 9.8 s / 17.7 s | 6,122 / 304 | 3.7 | 0 |
| gpt-6-luna | Prompt | 100% (21/21) | 16/16 | 5/5 | 5/5 | 8.9 s / 15.5 s | 8,046 / 250 | 2.9 | 0 |

The one miss: asked which location sold the most Timbits, `gpt-6-luna` answered correctly in words but its query listed every location alphabetically instead of ranking them. The scorer only checks the rows, so this counts as a miss; we keep it strict.

## Consequences

- **Prompt mode** is as accurate here, about 30% faster at the median, uses about a third fewer model calls and rarely hits the 5-call limit, at the cost of about 30% more input tokens. With 7 views the catalog is small; discovery stays for large catalogs.
- **`gpt-5.4-mini`** is as accurate as `gpt-6-luna` on this set, faster and cheaper.
- **Limits of these numbers:** 26 cases, written by the same person who wrote the reference SQL, single runs of a non-deterministic system. Treat 100% as "no failures found by this set", not as "always right".
- **Relative dates drift.** The seed's last day is fixed when the data is loaded, so "yesterday" and "last month" eventually point at days with no orders (both sides then return nothing and still "pass"). Reseeding before an eval run, or re-anchoring the seed monthly, is a follow-up.

## Reusable lesson

Score NL2SQL by comparing result sets with a reference query run under the same security context, keep examples and test questions apart, and read every miss: most failures here were ambiguities in the semantic layer, not model mistakes.
