import { useCallback, useEffect, useState } from "react";

import { Chat } from "@/features/chat/Chat";
import { PersonaSelect } from "@/features/chat/PersonaSelect";
import { QuotaBadge } from "@/features/chat/QuotaBadge";
import { fetchPersonas, fetchQuota, type Persona, type Quota } from "@/lib/api";

export default function App() {
  const [personas, setPersonas] = useState<Persona[]>([]);
  const [personaId, setPersonaId] = useState<string>();
  const [quota, setQuota] = useState<Quota | null>(null);
  const [loadError, setLoadError] = useState<string>();

  const refreshQuota = useCallback(() => {
    fetchQuota().then(setQuota, () => setQuota(null));
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    fetchPersonas(controller.signal).then(
      (loaded) => {
        setPersonas(loaded);
        setPersonaId((current) => current ?? loaded[0]?.id);
      },
      (error: unknown) => {
        if (!controller.signal.aborted)
          setLoadError(error instanceof Error ? error.message : String(error));
      },
    );
    refreshQuota();
    return () => controller.abort();
  }, [refreshQuota]);

  const persona = personas.find((p) => p.id === personaId);

  return (
    <div className="flex h-dvh flex-col bg-background text-foreground">
      <header className="border-b">
        <div className="mx-auto flex w-full max-w-3xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <h1 className="font-semibold text-lg">
            nl2sql<span className="text-muted-foreground">-agent</span>
          </h1>
          <div className="flex flex-wrap items-center gap-3">
            <QuotaBadge quota={quota} />
            {personas.length > 0 && personaId && (
              <PersonaSelect personas={personas} value={personaId} onChange={setPersonaId} />
            )}
          </div>
        </div>
      </header>

      <main className="flex min-h-0 flex-1 flex-col">
        {loadError && <p className="m-4 text-destructive text-sm">{loadError}</p>}
        {/* Switching persona changes whose data is visible, so it starts a new conversation. */}
        {persona && <Chat key={persona.id} persona={persona} onQuestionAnswered={refreshQuota} />}
      </main>

      <footer className="border-t px-4 py-2 text-center text-muted-foreground text-xs">
        Demo with invented data. Tim Hortons, Starbucks and Dunkin' are used for illustration only;
        the numbers don't reflect their business, and this project isn't affiliated with them.
      </footer>
    </div>
  );
}
