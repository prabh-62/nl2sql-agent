import type { UIMessage } from "@tanstack/ai-react";
import { lazy, Suspense, useMemo } from "react";

import { Message, MessageContent } from "@/components/ai-elements/message";
import { Shimmer } from "@/components/ai-elements/shimmer";
import { analyzeMessage } from "@/lib/chat-parts";

import { AgentSteps } from "./AgentSteps";

// The markdown renderer, syntax highlighter and charts are the heavy parts of the bundle. Load them
// when the first answer arrives, so the page itself stays small.
const MessageResponse = lazy(() =>
  import("@/components/ai-elements/message-response").then((m) => ({ default: m.MessageResponse })),
);
const QueryDetails = lazy(() =>
  import("./QueryDetails").then((m) => ({ default: m.QueryDetails })),
);
const ResultChart = lazy(() => import("./ResultChart").then((m) => ({ default: m.ResultChart })));

export function AssistantMessage({
  message,
  isStreaming,
}: {
  message: UIMessage;
  isStreaming: boolean;
}) {
  const turn = useMemo(() => analyzeMessage(message), [message]);
  const running = turn.steps.findLast(
    (step) => step.state === "input-available" || step.state === "input-streaming",
  );

  return (
    <Message from="assistant">
      <MessageContent className="w-full max-w-none gap-4">
        <AgentSteps steps={turn.steps} />
        {isStreaming && turn.text === "" && (
          <Shimmer>{running ? `${running.label}…` : "Thinking…"}</Shimmer>
        )}
        {turn.text !== "" && (
          <Suspense fallback={<p className="whitespace-pre-wrap">{turn.text}</p>}>
            <MessageResponse isAnimating={isStreaming}>{turn.text}</MessageResponse>
          </Suspense>
        )}
        {turn.query && (
          <Suspense fallback={null}>
            {turn.query.chart && <ResultChart spec={turn.query.chart} query={turn.query} />}
            <QueryDetails query={turn.query} />
          </Suspense>
        )}
      </MessageContent>
    </Message>
  );
}
