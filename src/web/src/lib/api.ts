import { z } from "zod";

export const personaSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string(),
});
export type Persona = z.infer<typeof personaSchema>;

export const quotaSchema = z.object({
  remaining: z.number(),
  limit: z.number(),
  demoPaused: z.boolean(),
});
export type Quota = z.infer<typeof quotaSchema>;

export async function fetchPersonas(signal?: AbortSignal): Promise<Persona[]> {
  const response = await fetch("/api/personas", { signal });
  if (!response.ok) throw new Error(`Couldn't load personas (HTTP ${response.status}).`);
  return z.array(personaSchema).parse(await response.json());
}

export async function fetchQuota(signal?: AbortSignal): Promise<Quota> {
  const response = await fetch("/api/quota", { signal });
  if (!response.ok) throw new Error(`Couldn't load the quota (HTTP ${response.status}).`);
  return quotaSchema.parse(await response.json());
}

const apiErrorSchema = z.object({ error: z.string(), message: z.string() });

/**
 * fetch for the chat stream: turns the question gate's JSON errors (unknown persona, too long,
 * quota used up, demo paused) into errors with a message a person can read.
 */
export const chatFetch: typeof fetch = async (input, init) => {
  const response = await fetch(input, init);
  if (response.ok) return response;

  const body = apiErrorSchema.safeParse(await response.json().catch(() => null));
  throw new Error(
    body.success ? body.data.message : `The request failed (HTTP ${response.status}).`,
  );
};
