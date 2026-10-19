import type { Quota } from "@/lib/api";

export function quotaText(quota: Quota): string {
  if (quota.demoPaused) return "Demo paused, come back tomorrow";
  return `${quota.remaining} of ${quota.limit} questions left today`;
}
