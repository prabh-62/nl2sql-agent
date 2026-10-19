import type { UIMessage } from "@tanstack/ai-react";
import { z } from "zod";

import type { ToolState } from "@/components/ai-elements/types";

/** One agent step: a tool call and, once it's done, its result. */
export interface Step {
  id: string;
  name: string;
  label: string;
  state: ToolState;
  input: unknown;
  output: unknown;
  errorText?: string;
}

export const chartSchema = z.object({
  type: z.enum(["bar", "line"]),
  title: z.string(),
  x: z.string(),
  y: z.array(z.string()).min(1).max(3),
});
export type ChartSpec = z.infer<typeof chartSchema>;

const cellSchema = z.union([z.string(), z.number(), z.boolean(), z.null()]);

export const queryResultSchema = z.object({
  columns: z.array(z.string()),
  rows: z.array(z.array(cellSchema)),
  rowCount: z.number(),
  truncated: z.boolean(),
  views: z.array(z.string()),
  chart: chartSchema.optional(),
});

/** A successful run_query: the SQL, what it read and what it returned. */
export interface QueryOutcome extends z.infer<typeof queryResultSchema> {
  sql: string;
}

export interface AssistantTurn {
  text: string;
  steps: Step[];
  /** The last query that succeeded; earlier attempts stay visible as steps. */
  query?: QueryOutcome;
}

const runQueryArgsSchema = z.object({ sql: z.string() });

/** Splits a TanStack AI message into the pieces the UI shows. */
export function analyzeMessage(message: UIMessage): AssistantTurn {
  const results = new Map<string, { content: unknown; isError: boolean }>();
  for (const part of message.parts) {
    if (part.type === "tool-result") {
      results.set(part.toolCallId, {
        content: parseJson(part.content),
        isError: part.state === "error",
      });
    }
  }

  const steps: Step[] = [];
  let query: QueryOutcome | undefined;
  for (const part of message.parts) {
    if (part.type !== "tool-call") continue;

    const input = part.input ?? parseJson(part.arguments);
    const result = results.get(part.id);
    const output = result?.content ?? part.output;
    const reportedError = errorOf(output);
    steps.push({
      id: part.id,
      name: part.name,
      label: stepLabel(part.name, input),
      state: stepState(
        part.state,
        result !== undefined || part.output !== undefined,
        result?.isError || reportedError !== undefined,
      ),
      input,
      output,
      errorText: reportedError,
    });

    if (part.name === "run_query" && reportedError === undefined) {
      const args = runQueryArgsSchema.safeParse(input);
      const parsed = queryResultSchema.safeParse(output);
      if (args.success && parsed.success) {
        query = { ...parsed.data, sql: args.data.sql };
      }
    }
  }

  const text = message.parts
    .flatMap((part) => (part.type === "text" ? [part.content] : []))
    .join("");

  return { text, steps, query };
}

export function stepLabel(name: string, input: unknown): string {
  switch (name) {
    case "list_views":
      return "Looking up the available views";
    case "describe_view": {
      const view = z.object({ viewName: z.string() }).safeParse(input);
      return view.success
        ? `Reading the columns of ${view.data.viewName}`
        : "Reading a view's columns";
    }
    case "get_examples":
      return "Finding similar example questions";
    case "run_query":
      return "Running the query";
    default:
      return name;
  }
}

function stepState(callState: string, hasResult: boolean, isError: boolean): ToolState {
  if (isError || callState === "error") return "output-error";
  if (hasResult || callState === "complete") return "output-available";
  if (callState === "input-complete") return "input-available";
  return "input-streaming";
}

function errorOf(output: unknown): string | undefined {
  const parsed = z.object({ error: z.string() }).safeParse(output);
  return parsed.success ? parsed.data.error : undefined;
}

function parseJson(value: unknown): unknown {
  if (typeof value !== "string") return value;
  try {
    const parsed: unknown = JSON.parse(value);
    // Tool results arrive JSON-encoded twice: the tool returns a JSON string, which is itself serialized.
    return typeof parsed === "string" ? parseJson(parsed) : parsed;
  } catch {
    return value;
  }
}
