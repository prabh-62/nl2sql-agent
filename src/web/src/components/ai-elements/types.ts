// Local stand-ins for the few Vercel AI SDK ("ai" package) types that the copied AI Elements
// components were written against. This file is the only seam between AI Elements and our chat
// client (TanStack AI); see src/lib/chat-parts.ts for the mapping. Importing "ai" is a lint error.

export type MessageRole = "system" | "user" | "assistant";

export type ToolState =
  | "input-streaming"
  | "input-available"
  | "approval-requested"
  | "approval-responded"
  | "output-available"
  | "output-error"
  | "output-denied";

export interface TextUIPart {
  type: "text";
  text: string;
}

interface ToolPartBase {
  state: ToolState;
  input?: unknown;
  output?: unknown;
  errorText?: string;
}

export interface ToolUIPart extends ToolPartBase {
  type: `tool-${string}`;
}

export interface DynamicToolUIPart extends ToolPartBase {
  type: "dynamic-tool";
  toolName: string;
}

export interface UIMessage {
  id: string;
  role: MessageRole;
  parts: Array<TextUIPart | ToolUIPart | DynamicToolUIPart>;
}
