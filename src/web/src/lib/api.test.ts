import { afterEach, describe, expect, it, vi } from "vite-plus/test";

import { chatFetch } from "./api";

afterEach(() => vi.unstubAllGlobals());

describe("chatFetch", () => {
  it("turns the question gate's errors into readable messages", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json(
          { error: "client_limit", message: "You've used your 10 questions for today." },
          { status: 429 },
        ),
      ),
    );

    await expect(chatFetch("/api/agui")).rejects.toThrow(
      "You've used your 10 questions for today.",
    );
  });

  it("falls back to the status code when the body isn't the gate's JSON", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response("oops", { status: 502 })),
    );

    await expect(chatFetch("/api/agui")).rejects.toThrow("HTTP 502");
  });

  it("passes successful responses through", async () => {
    const ok = new Response("data: {}\n\n", { status: 200 });
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ok),
    );

    await expect(chatFetch("/api/agui")).resolves.toBe(ok);
  });
});
