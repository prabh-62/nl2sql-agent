import { describe, expect, it } from "vite-plus/test";

import { columnLabel, formatCell, isMonthColumn } from "./format";

describe("formatCell", () => {
  it("formats numbers, booleans and nulls for people", () => {
    expect(formatCell(1230.9)).toBe(
      (1230.9).toLocaleString(undefined, { minimumFractionDigits: 2 }),
    );
    expect(formatCell(17985)).toBe((17985).toLocaleString());
    expect(formatCell(true)).toBe("Yes");
    expect(formatCell(null)).toBe("—");
  });

  it("shows midnight timestamps as dates and month columns as months", () => {
    expect(formatCell("2026-09-01T00:00:00")).not.toContain(":");
    expect(formatCell("2026-09-01T00:00:00", { months: true })).toMatch(/2026/);
    expect(formatCell("Queen St W")).toBe("Queen St W");
  });
});

describe("isMonthColumn", () => {
  it("is true only when every value is the first of a month", () => {
    expect(isMonthColumn(["2026-08-01T00:00:00", "2026-09-01T00:00:00"])).toBe(true);
    expect(isMonthColumn(["2026-08-01T00:00:00", "2026-09-02T00:00:00"])).toBe(false);
    expect(isMonthColumn([])).toBe(false);
  });
});

describe("columnLabel", () => {
  it("splits PascalCase", () => {
    expect(columnLabel("NetRevenue")).toBe("Net Revenue");
    expect(columnLabel("LocalHour")).toBe("Local Hour");
  });
});
