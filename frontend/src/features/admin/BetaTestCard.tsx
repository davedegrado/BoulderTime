import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { FlaskConical, Undo2 } from "lucide-react";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { api } from "@/lib/api";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";

interface BetaTestResult { betaTestBoulders: number; changed: number; betaTesters?: number }

/**
 * Launch day (ADR-056): every boulder of the gym off the wall becomes a trial boulder — kept in the history of whoever
 * tested the app, scoring no points — and whoever climbed one gets the Beta Tester badge. Running it again adds what
 * was taken down since; "Undo" takes every mark off (the badges stay). The gym's slug is typed to confirm, as the
 * server asks. Platform administrators only: the server checks it on every call.
 */
export function BetaTestCard({ gymId, gymSlug }: { gymId: string; gymSlug: string }) {
  const toast = useToast();
  const [slug, setSlug] = useState("");
  const [last, setLast] = useState<BetaTestResult | null>(null);
  const run = useMutation({
    mutationFn: (undo: boolean) => api.post<BetaTestResult>(`/api/admin/gyms/${gymId}/beta-test${undo ? "/undo" : ""}`, { gymSlug: slug.trim() }),
  });
  const confirmed = slug.trim() === gymSlug;
  const go = (undo: boolean) => run.mutate(undo, {
    onSuccess: (r) => {
      setLast(r);
      toast.success(undo ? plural(r.changed, "1 boulder is no longer a trial one", "{count} boulders are no longer trial ones")
        : plural(r.changed, "1 boulder marked as a trial one", "{count} boulders marked as trial ones"));
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <section className="card stack beta-test-card" aria-label={t("Close the trial period")}>
      <p className="field__hint">{t("On launch day: every boulder of this gym off the wall becomes a trial boulder. It stays in the history of who climbed it, with “Betatest” by its sector, but scores no points and counts in no leaderboard; who climbed one gets the Beta Tester badge. Boulders on the wall are not touched. Running it again adds those taken down since. Only on launch day: later it would also mark ordinary retraces.")}</p>
      <TextField label={t("Type the gym's slug to confirm: {slug}", { slug: gymSlug })} value={slug} onChange={(e) => setSlug(e.target.value)} autoComplete="off" />
      <div className="form__actions">
        <Button icon={<FlaskConical aria-hidden />} disabled={!confirmed || run.isPending} loading={run.isPending && run.variables === false} onClick={() => go(false)}>
          {t("Mark the boulders off the wall as trial ones")}
        </Button>
        <Button variant="ghost" icon={<Undo2 aria-hidden />} disabled={!confirmed || run.isPending} loading={run.isPending && run.variables === true} onClick={() => go(true)}>
          {t("Undo: take every trial mark off")}
        </Button>
      </div>
      {last && (
        <p className="field__hint" role="status">
          {plural(last.betaTestBoulders, "1 trial boulder in this gym now", "{count} trial boulders in this gym now")}
          {last.betaTesters != null && last.betaTesters > 0 && ` · ${plural(last.betaTesters, "1 beta tester", "{count} beta testers")}`}
        </p>
      )}
    </section>
  );
}
