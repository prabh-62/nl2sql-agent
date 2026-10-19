import { fetchServerSentEvents, useChat } from "@tanstack/ai-react";
import { AlertCircleIcon, ArrowUpIcon, SquareIcon } from "lucide-react";
import { type FormEvent, type KeyboardEvent, useEffect, useMemo, useRef, useState } from "react";

import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from "@/components/ai-elements/conversation";
import { Message, MessageContent } from "@/components/ai-elements/message";
import { Suggestion } from "@/components/ai-elements/suggestion";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { chatFetch, type Persona } from "@/lib/api";
import { MAX_QUESTION_LENGTH, STARTER_QUESTIONS } from "@/lib/questions";

import { AssistantMessage } from "./AssistantMessage";

/** One conversation, as one persona. Remount it (key) to start over as someone else. */
export function Chat({
  persona,
  onQuestionAnswered,
}: {
  persona: Persona;
  onQuestionAnswered: () => void;
}) {
  const connection = useMemo(
    () =>
      fetchServerSentEvents("/api/agui", {
        headers: { "X-Persona": persona.id },
        fetchClient: chatFetch,
      }),
    [persona.id],
  );
  const { messages, sendMessage, isLoading, error, stop } = useChat({ connection });
  const [input, setInput] = useState("");
  const inputRef = useRef<HTMLTextAreaElement>(null);

  // Refresh the quota badge each time an answer finishes (or fails).
  const wasLoading = useRef(false);
  useEffect(() => {
    if (wasLoading.current && !isLoading) onQuestionAnswered();
    wasLoading.current = isLoading;
  }, [isLoading, onQuestionAnswered]);

  function ask(question: string) {
    const trimmed = question.trim();
    if (!trimmed || isLoading || trimmed.length > MAX_QUESTION_LENGTH) return;
    setInput("");
    void sendMessage(trimmed);
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    ask(input);
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      ask(input);
    }
  }

  const lastMessage = messages.at(-1);

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <Conversation className="min-h-0 flex-1" aria-live="polite" aria-busy={isLoading}>
        <ConversationContent className="mx-auto w-full max-w-3xl">
          {messages.length === 0 ? (
            <ConversationEmptyState
              title={`Ask about ${persona.name.replace(/ (owner|manager)$/, "")}'s sales`}
              description={persona.description}
            >
              <div className="flex w-full max-w-xl flex-col items-center gap-4">
                <div className="space-y-1">
                  <h2 className="font-medium">Ask a question about your sales</h2>
                  <p className="text-muted-foreground text-sm">{persona.description}</p>
                </div>
                <ul aria-label="Example questions" className="flex flex-wrap justify-center gap-2">
                  {STARTER_QUESTIONS.map((question) => (
                    <li key={question}>
                      <Suggestion
                        suggestion={question}
                        onClick={ask}
                        className="h-auto whitespace-normal py-1.5 text-left"
                      />
                    </li>
                  ))}
                </ul>
              </div>
            </ConversationEmptyState>
          ) : (
            messages.map((message) =>
              message.role === "user" ? (
                <Message key={message.id} from="user">
                  <MessageContent>
                    {message.parts.map((part, index) =>
                      part.type === "text" ? <p key={index}>{part.content}</p> : null,
                    )}
                  </MessageContent>
                </Message>
              ) : message.role === "assistant" ? (
                <AssistantMessage
                  key={message.id}
                  message={message}
                  isStreaming={isLoading && message === lastMessage}
                />
              ) : null,
            )
          )}
          {isLoading && lastMessage?.role === "user" && (
            <p className="text-muted-foreground text-sm" role="status">
              Thinking…
            </p>
          )}
        </ConversationContent>
        <ConversationScrollButton aria-label="Scroll to the latest message" />
      </Conversation>

      <div className="mx-auto w-full max-w-3xl px-4 pb-4">
        {error && (
          <Alert variant="destructive" className="mb-3">
            <AlertCircleIcon aria-hidden />
            <AlertDescription>{error.message}</AlertDescription>
          </Alert>
        )}
        <form
          onSubmit={onSubmit}
          className="relative rounded-xl border bg-background shadow-sm focus-within:ring-2 focus-within:ring-ring"
        >
          <label htmlFor="question" className="sr-only">
            Your question
          </label>
          <Textarea
            id="question"
            ref={inputRef}
            value={input}
            maxLength={MAX_QUESTION_LENGTH}
            rows={2}
            placeholder="Ask about revenue, drinks, customers, refunds…"
            className="min-h-16 resize-none border-0 pr-14 shadow-none focus-visible:ring-0"
            aria-describedby="question-hint"
            onChange={(event) => setInput(event.target.value)}
            onKeyDown={onKeyDown}
          />
          <div className="flex items-center justify-between px-3 pb-2">
            <span id="question-hint" className="text-muted-foreground text-xs">
              Enter to send, Shift+Enter for a new line · {input.length}/{MAX_QUESTION_LENGTH}
            </span>
            {isLoading ? (
              <Button
                type="button"
                size="icon"
                variant="secondary"
                onClick={stop}
                aria-label="Stop"
              >
                <SquareIcon aria-hidden />
              </Button>
            ) : (
              <Button type="submit" size="icon" disabled={input.trim() === ""} aria-label="Ask">
                <ArrowUpIcon aria-hidden />
              </Button>
            )}
          </div>
        </form>
      </div>
    </div>
  );
}
