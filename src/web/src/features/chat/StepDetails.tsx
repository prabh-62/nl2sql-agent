import { ToolInput, ToolOutput } from "@/components/ai-elements/tool-details";
import type { Step } from "@/lib/chat-parts";

/** A step's arguments and result. Loaded on demand: it pulls in the syntax highlighter. */
export function StepDetails({ step }: { step: Step }) {
  return (
    <>
      <ToolInput input={step.input} />
      <ToolOutput output={step.output} errorText={step.errorText} />
    </>
  );
}
