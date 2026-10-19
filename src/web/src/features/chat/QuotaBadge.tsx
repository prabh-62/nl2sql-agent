import { Badge } from "@/components/ui/badge";
import type { Quota } from "@/lib/api";
import { quotaText } from "@/lib/quota";

export function QuotaBadge({ quota }: { quota: Quota | null }) {
  if (!quota) return null;
  const exhausted = quota.demoPaused || quota.remaining === 0;
  return (
    <Badge variant={exhausted ? "destructive" : "secondary"} role="status" aria-live="polite">
      {quotaText(quota)}
    </Badge>
  );
}
