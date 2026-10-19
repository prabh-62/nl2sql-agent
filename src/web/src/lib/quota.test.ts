import { describe, expect, it } from "vite-plus/test";

import { quotaText } from "./quota";

describe("quotaText", () => {
  it("shows what's left, or that the demo is paused", () => {
    expect(quotaText({ remaining: 7, limit: 10, demoPaused: false })).toBe(
      "7 of 10 questions left today",
    );
    expect(quotaText({ remaining: 7, limit: 10, demoPaused: true })).toBe(
      "Demo paused, come back tomorrow",
    );
  });
});
