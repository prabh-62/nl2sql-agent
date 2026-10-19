import { ChevronDownIcon, DatabaseIcon } from "lucide-react";

import { CodeBlock, CodeBlockCopyButton } from "@/components/ai-elements/code-block";
import { Badge } from "@/components/ui/badge";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { QueryOutcome } from "@/lib/chat-parts";
import { columnLabel, formatCell, isMonthColumn } from "@/lib/format";

/** The rows behind the answer, and a "Show SQL" toggle with the query and the views it read. */
export function QueryDetails({ query }: { query: QueryOutcome }) {
  const shown = query.rows.length;
  const monthColumns = query.columns.map((_, i) =>
    isMonthColumn(query.rows.map((row) => row[i] ?? null)),
  );
  const numericColumns = query.columns.map(
    (_, i) =>
      query.rows.length > 0 &&
      query.rows.every((row) => row[i] === null || typeof row[i] === "number"),
  );
  const caption =
    query.rowCount > shown
      ? `Showing ${shown} of ${query.truncated && query.rowCount >= 500 ? "500+" : query.rowCount} rows.`
      : `${query.rowCount} ${query.rowCount === 1 ? "row" : "rows"}.`;

  return (
    <div className="flex flex-col gap-3">
      {query.rows.length > 0 && (
        <div className="max-h-96 overflow-auto rounded-md border">
          <Table>
            <TableCaption className="mb-2">{caption}</TableCaption>
            <TableHeader className="sticky top-0 bg-background">
              <TableRow>
                {query.columns.map((column, i) => (
                  <TableHead
                    key={column}
                    scope="col"
                    className={numericColumns[i] ? "text-right" : undefined}
                  >
                    {columnLabel(column)}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {query.rows.map((row, rowIndex) => (
                <TableRow key={rowIndex}>
                  {row.map((cell, cellIndex) => (
                    <TableCell
                      key={cellIndex}
                      className={numericColumns[cellIndex] ? "text-right tabular-nums" : undefined}
                    >
                      {formatCell(cell, { months: monthColumns[cellIndex] })}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Collapsible className="group">
        <CollapsibleTrigger className="flex items-center gap-2 text-muted-foreground text-sm hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 rounded">
          <DatabaseIcon aria-hidden className="size-4" />
          <span className="group-data-[state=open]:hidden">Show SQL</span>
          <span className="hidden group-data-[state=open]:inline">Hide SQL</span>
          <ChevronDownIcon
            aria-hidden
            className="size-4 transition-transform group-data-[state=open]:rotate-180"
          />
        </CollapsibleTrigger>
        <CollapsibleContent className="mt-2 flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2 text-muted-foreground text-xs">
            <span>Views used:</span>
            {query.views.map((view) => (
              <Badge key={view} variant="secondary">
                {view}
              </Badge>
            ))}
          </div>
          <CodeBlock code={query.sql} language="sql">
            <CodeBlockCopyButton aria-label="Copy SQL" />
          </CodeBlock>
        </CollapsibleContent>
      </Collapsible>
    </div>
  );
}
