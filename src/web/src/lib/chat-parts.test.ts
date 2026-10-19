import type { UIMessage } from "@tanstack/ai-react";
import { describe, expect, it } from "vite-plus/test";

import { analyzeMessage, stepLabel } from "./chat-parts";

// Tool results arrive JSON-encoded twice: the .NET tool returns a JSON string, which is serialized again.
const encode = (value: unknown) => JSON.stringify(JSON.stringify(value));

const queryResult = {
  columns: ["LocationName", "NetRevenue"],
  rows: [
    ["Queen St W", 5070.74],
    ["Union Station", 5297.07],
  ],
  rowCount: 2,
  truncated: false,
  views: ["ai.Orders"],
  chart: { type: "bar", title: "Net revenue by location", x: "LocationName", y: ["NetRevenue"] },
};

function assistant(parts: unknown[]): UIMessage {
  return { id: "m1", role: "assistant", parts } as UIMessage;
}

describe("analyzeMessage", () => {
  it("pairs each tool call with its result and finds the query", () => {
    const turn = analyzeMessage(
      assistant([
        {
          type: "tool-call",
          id: "c1",
          name: "describe_view",
          arguments: '{"viewName":"ai.Orders"}',
          state: "complete",
        },
        {
          type: "tool-result",
          toolCallId: "c1",
          content: encode([{ name: "NetRevenue" }]),
          state: "complete",
        },
        {
          type: "tool-call",
          id: "c2",
          name: "run_query",
          arguments: '{"sql":"SELECT 1"}',
          state: "complete",
        },
        { type: "tool-result", toolCallId: "c2", content: encode(queryResult), state: "complete" },
        { type: "text", content: "Union Station led." },
      ]),
    );

    expect(turn.text).toBe("Union Station led.");
    expect(turn.steps.map((s) => [s.label, s.state])).toEqual([
      ["Reading the columns of ai.Orders", "output-available"],
      ["Running the query", "output-available"],
    ]);
    expect(turn.query?.sql).toBe("SELECT 1");
    expect(turn.query?.rows).toHaveLength(2);
    expect(turn.query?.chart?.type).toBe("bar");
  });

  it("marks a failed query as an error and keeps the last successful one", () => {
    const turn = analyzeMessage(
      assistant([
        {
          type: "tool-call",
          id: "c1",
          name: "run_query",
          arguments: '{"sql":"SELECT Revenue"}',
          state: "complete",
        },
        {
          type: "tool-result",
          toolCallId: "c1",
          content: encode({ error: "Invalid column name 'Revenue'." }),
          state: "complete",
        },
        {
          type: "tool-call",
          id: "c2",
          name: "run_query",
          arguments: '{"sql":"SELECT NetRevenue"}',
          state: "complete",
        },
        { type: "tool-result", toolCallId: "c2", content: encode(queryResult), state: "complete" },
      ]),
    );

    expect(turn.steps[0]?.state).toBe("output-error");
    expect(turn.steps[0]?.errorText).toContain("Invalid column name");
    expect(turn.query?.sql).toBe("SELECT NetRevenue");
  });

  it("shows a step as running until its result arrives", () => {
    const turn = analyzeMessage(
      assistant([
        {
          type: "tool-call",
          id: "c1",
          name: "list_views",
          arguments: "{}",
          state: "input-complete",
        },
      ]),
    );

    expect(turn.steps[0]?.state).toBe("input-available");
    expect(turn.query).toBeUndefined();
  });

  it("ignores a chart that doesn't match the schema", () => {
    const turn = analyzeMessage(
      assistant([
        {
          type: "tool-call",
          id: "c1",
          name: "run_query",
          arguments: '{"sql":"SELECT 1"}',
          state: "complete",
        },
        {
          type: "tool-result",
          toolCallId: "c1",
          content: encode({ ...queryResult, chart: { type: "pie" } }),
          state: "complete",
        },
      ]),
    );

    expect(turn.query).toBeUndefined();
  });
});

describe("stepLabel", () => {
  it("uses plain language and falls back to the tool name", () => {
    expect(stepLabel("get_examples", {})).toBe("Finding similar example questions");
    expect(stepLabel("describe_view", { viewName: "ai.Refunds" })).toBe(
      "Reading the columns of ai.Refunds",
    );
    expect(stepLabel("something_new", {})).toBe("something_new");
  });
});
