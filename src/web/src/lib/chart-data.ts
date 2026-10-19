import type { ChartSpec, QueryOutcome } from "@/lib/chat-parts";
import { formatCell, isMonthColumn } from "@/lib/format";

/** Turns result rows into Recharts data, or null if the chart doesn't fit them. */
export function chartData(
  spec: ChartSpec,
  query: QueryOutcome,
): Array<Record<string, string | number>> | null {
  const index = new Map(query.columns.map((column, i) => [column.toLowerCase(), i]));
  const xIndex = index.get(spec.x.toLowerCase());
  const yIndexes = spec.y.map((y) => index.get(y.toLowerCase()));
  if (xIndex === undefined || yIndexes.some((i) => i === undefined) || query.rows.length < 2)
    return null;

  const months = isMonthColumn(query.rows.map((row) => row[xIndex] ?? null));
  const data: Array<Record<string, string | number>> = [];
  for (const row of query.rows) {
    const point: Record<string, string | number> = {
      x: formatCell(row[xIndex] ?? null, { months }),
    };
    for (const [n, i] of yIndexes.entries()) {
      const value = row[i!];
      if (typeof value !== "number") return null;
      point[`y${n}`] = value;
    }
    data.push(point);
  }

  return data;
}
