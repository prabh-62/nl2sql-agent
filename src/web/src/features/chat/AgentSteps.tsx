import { lazy, Suspense } from "react";

import { Tool, ToolContent, ToolHeader } from "@/components/ai-elements/tool";
import type { Step } from "@/lib/chat-parts";

const StepDetails = lazy(() => import("./StepDetails").then((m) => ({ default: m.StepDetails })));

/** What the agent did, one collapsible card per tool call. */
export function AgentSteps({ steps }: { steps: Step[] }) {
  if (steps.length === 0) return null;

  return (
    <ol aria-label="Agent steps" className="flex flex-col gap-1">
      {steps.map((step) => (
        <li key={step.id}>
          <Tool className="mb-0">
            <ToolHeader
              type="dynamic-tool"
              toolName={step.name}
              title={step.label}
              state={step.state}
            />
            <ToolContent>
              <Suspense fallback={<p className="text-muted-foreground text-sm">Loading…</p>}>
                <StepDetails step={step} />
              </Suspense>
            </ToolContent>
          </Tool>
        </li>
      ))}
    </ol>
  );
}
