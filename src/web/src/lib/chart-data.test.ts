import { describe, expect, it } from "vite-plus/test";

import { chartData } from "./chart-data";
import type { QueryOutcome } from "./chat-parts";

const query: QueryOutcome = {
  sql: "SELECT ...",
  columns: ["Month", "OatShare"],
  rows: [
    ["2026-08-01T00:00:00", 0.31],
    ["2026-09-01T00:00:00", 0.35],
  ],
  rowCount: 2,
  truncated: false,
  views: ["ai.OrderLines"],
};

describe("chartData", () => {
  it("maps rows to points, matching columns case-insensitively", () => {
    const data = chartData(
      { type: "line", title: "Oat share", x: "month", y: ["oatshare"] },
      query,
    );

    expect(data).toHaveLength(2);
    expect(data?.[1]?.y0).toBe(0.35);
  });

  it("refuses columns that don't exist or aren't numeric", () => {
    expect(chartData({ type: "bar", title: "t", x: "Week", y: ["OatShare"] }, query)).toBeNull();
    expect(chartData({ type: "bar", title: "t", x: "OatShare", y: ["Month"] }, query)).toBeNull();
  });
});
