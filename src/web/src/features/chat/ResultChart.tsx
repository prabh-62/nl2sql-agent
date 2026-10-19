import { Bar, BarChart, CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";

import {
  type ChartConfig,
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart";
import type { ChartSpec, QueryOutcome } from "@/lib/chat-parts";
import { chartData } from "@/lib/chart-data";
import { columnLabel } from "@/lib/format";

export function ResultChart({ spec, query }: { spec: ChartSpec; query: QueryOutcome }) {
  const data = chartData(spec, query);
  if (data === null) return null;

  const config: ChartConfig = Object.fromEntries(
    spec.y.map((y, n) => [`y${n}`, { label: columnLabel(y), color: `var(--chart-${n + 1})` }]),
  );
  const series = spec.y.map((_, n) => `y${n}`);

  return (
    <figure className="flex flex-col gap-2">
      <figcaption className="font-medium text-sm">{spec.title}</figcaption>
      <ChartContainer
        config={config}
        className="aspect-auto h-64 w-full"
        role="img"
        aria-label={`${spec.type} chart: ${spec.title}`}
      >
        {spec.type === "line" ? (
          <LineChart data={data} accessibilityLayer>
            <CartesianGrid vertical={false} />
            <XAxis dataKey="x" tickLine={false} axisLine={false} />
            <YAxis tickLine={false} axisLine={false} width={64} />
            <ChartTooltip content={<ChartTooltipContent />} />
            {series.length > 1 && <ChartLegend content={<ChartLegendContent />} />}
            {series.map((key) => (
              <Line
                key={key}
                dataKey={key}
                type="monotone"
                stroke={`var(--color-${key})`}
                strokeWidth={2}
                dot={false}
              />
            ))}
          </LineChart>
        ) : (
          <BarChart data={data} accessibilityLayer>
            <CartesianGrid vertical={false} />
            <XAxis dataKey="x" tickLine={false} axisLine={false} />
            <YAxis tickLine={false} axisLine={false} width={64} />
            <ChartTooltip content={<ChartTooltipContent />} />
            {series.length > 1 && <ChartLegend content={<ChartLegendContent />} />}
            {series.map((key) => (
              <Bar key={key} dataKey={key} fill={`var(--color-${key})`} radius={4} />
            ))}
          </BarChart>
        )}
      </ChartContainer>
    </figure>
  );
}
