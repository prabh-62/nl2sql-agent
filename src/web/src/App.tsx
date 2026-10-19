import { useState, type FormEvent } from "react";
import { fetchServerSentEvents, useChat } from "@tanstack/ai-react";

const connection = fetchServerSentEvents("/api/agui");

export default function App() {
  const { messages, sendMessage, isLoading, error } = useChat({ connection });
  const [input, setInput] = useState("");

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    const question = input.trim();
    if (!question || isLoading) return;
    setInput("");
    void sendMessage(question);
  }

  return (
    <main>
      <h1>nl2sql-agent</h1>
      <ol className="messages" aria-live="polite">
        {messages.map((message) => (
          <li key={message.id} className={message.role}>
            {message.parts.map((part, index) => {
              switch (part.type) {
                case "text":
                  return <p key={index}>{part.content}</p>;
                case "tool-call":
                  return (
                    <details key={index} className="tool">
                      <summary>
                        Tool call: {part.name} ({part.state})
                      </summary>
                      <pre>{part.arguments || "{}"}</pre>
                    </details>
                  );
                case "tool-result":
                  return (
                    <details key={index} className="tool">
                      <summary>Tool result ({part.state})</summary>
                      <pre>
                        {typeof part.content === "string"
                          ? part.content
                          : JSON.stringify(part.content, null, 2)}
                      </pre>
                    </details>
                  );
                default:
                  return null;
              }
            })}
          </li>
        ))}
      </ol>
      {error && <p role="alert">{error.message}</p>}
      <form onSubmit={onSubmit}>
        <label htmlFor="question">Question</label>
        <input
          id="question"
          value={input}
          maxLength={500}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Which database version are you on?"
        />
        <button type="submit" disabled={isLoading}>
          {isLoading ? "Thinking…" : "Ask"}
        </button>
      </form>
    </main>
  );
}
