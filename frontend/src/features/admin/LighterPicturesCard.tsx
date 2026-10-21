import { useRef, useState } from "react";
import { Undo2, Trash2, ImageDown, Square } from "lucide-react";
import { confirmLighterPictures, formatMegabytes, makeLighter, undoLighterPictures, useLighterPictures, useRefreshLighterPictures } from "@/features/admin/lighterPictures";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

const wait = (ms: number) => new Promise((r) => window.setTimeout(r, ms));

/**
 * Makes a gym's existing boulder pictures as light as new ones, on this device, one by one. Every app sees the
 * lighter pictures at once; until confirmed, the old files stay and everything can be put back.
 */
export function LighterPicturesCard({ gymId, gymSlug }: { gymId: string; gymSlug: string }) {
  const overview = useLighterPictures(gymId);
  const refresh = useRefreshLighterPictures(gymId);
  const toast = useToast();
  const [run, setRun] = useState<{ done: number; kept: number; total: number } | null>(null);
  const stopping = useRef(false);
  const [slug, setSlug] = useState("");
  const [busy, setBusy] = useState<"undo" | "confirm" | null>(null);

  if (overview.isPending) return <LoadingState label={t("Loading")} />;
  if (overview.isError) return <ErrorState error={overview.error} onRetry={() => overview.refetch()} />;
  const o = overview.data;

  async function start() {
    const todo = o.pending;
    stopping.current = false;
    const progress = { done: 0, kept: 0, total: todo.length };
    setRun({ ...progress });
    try {
      for (const c of todo) {
        if (stopping.current) break;
        for (let attempt = 0; ; attempt++) {
          try {
            const outcome = await makeLighter(gymId, c);
            if (outcome === "kept") progress.kept++;
            break;
          } catch (err) {
            // Too many requests in a minute: wait for the next one and go on.
            if (err instanceof ApiError && err.status === 429 && attempt < 3) { await wait(60_000); continue; }
            throw err;
          }
        }
        progress.done++;
        setRun({ ...progress });
      }
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setRun(null);
      await refresh();
    }
  }

  async function finish(kind: "undo" | "confirm") {
    setBusy(kind);
    try {
      const r = kind === "undo" ? await undoLighterPictures(gymId, slug.trim()) : await confirmLighterPictures(gymId, slug.trim());
      toast.success(kind === "undo"
        ? t("{count} pictures put back as they were", { count: r.changed }) + (r.skipped ? " · " + t("{count} changed by staff since, left as they are", { count: r.skipped }) : "")
        : t("Old files deleted: {size} freed", { size: formatMegabytes(r.bytesFreed) }));
      setSlug("");
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(null);
      await refresh();
    }
  }

  const saved = o.bytesBefore > 0 ? Math.round((1 - o.bytesAfter / o.bytesBefore) * 100) : 0;
  return (
    <div className="stack">
      <p className="field__hint">
        {t("Encodes the boulders' photos and card pictures again on this device, as light as the app now makes them, and swaps them in. Every app sees the lighter pictures at once. The old files are kept until you confirm, so you can put everything back. Wall photos are left as they are.")}
      </p>
      <p>{t("To make lighter: {count}", { count: o.pending.length })}</p>
      {o.replaced > 0 && (
        <p>{t("Replaced: {count} · from {before} to {after} ({saved}% less)", { count: o.replaced, before: formatMegabytes(o.bytesBefore), after: formatMegabytes(o.bytesAfter), saved })}</p>
      )}
      {o.confirmed > 0 && <p className="field__hint">{t("Already confirmed: {count}", { count: o.confirmed })}</p>}

      {run ? (
        <>
          <div className="progress" role="progressbar" aria-label={t("Making pictures lighter")} aria-valuenow={run.done} aria-valuemin={0} aria-valuemax={run.total}>
            <span className="progress__fill" style={{ width: `${run.total ? (run.done / run.total) * 100 : 100}%` }} />
          </div>
          <p className="field__hint">{t("{done} of {total}", { done: run.done, total: run.total })}{run.kept ? " · " + t("{count} already light, left as they are", { count: run.kept }) : ""}</p>
          <Button variant="secondary" icon={<Square aria-hidden />} onClick={() => { stopping.current = true; }}>{t("Stop")}</Button>
        </>
      ) : (
        <Button icon={<ImageDown aria-hidden />} disabled={o.pending.length === 0 || busy !== null} onClick={start}>{t("Make them lighter")}</Button>
      )}

      {o.replaced > 0 && !run && (
        <div className="stack">
          <TextField label={t("Type the gym's slug to confirm")} hint={gymSlug} value={slug} onChange={(e) => setSlug(e.target.value)} autoCapitalize="none" />
          <div className="form__actions">
            <Button variant="secondary" icon={<Undo2 aria-hidden />} loading={busy === "undo"} disabled={!slug.trim() || busy !== null} onClick={() => finish("undo")}>
              {t("Put them all back")}
            </Button>
            <Button variant="danger" icon={<Trash2 aria-hidden />} loading={busy === "confirm"} disabled={!slug.trim() || busy !== null} onClick={() => finish("confirm")}>
              {t("Confirm and delete the old files")}
            </Button>
          </div>
          <p className="field__hint">{t("Confirming can't be undone: the old files are deleted for good.")}</p>
        </div>
      )}
    </div>
  );
}
