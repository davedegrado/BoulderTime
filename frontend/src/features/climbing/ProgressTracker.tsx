import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Check, Minus, Plus, RotateCcw, Target } from "lucide-react";
import type { BoulderDetail } from "@/features/boulders/api";
import { useSetAttempt, useSetProject, useSetRating } from "@/features/climbing/api";
import { StarInput } from "@/features/climbing/ClimbingBits";
import { useAuth } from "@/auth/AuthProvider";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

const SAVE_DELAY_MS = 700;

/**
 * Attempts, completion and rating for the signed-in climber: attempts and the climber's own rating side by side, then
 * the one big action, "Mark as completed" (or, once done, when and a way to undo it), always in the same place.
 * Changes apply instantly on screen and are saved after a short pause, so fast tapping sends one request
 * with the final value instead of racing several.
 */
export function ProgressTracker({ boulder }: { boulder: BoulderDetail }) {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) {
    return (
      <section className="card tracker" aria-labelledby="tracker-title">
        <h2 id="tracker-title" className="section__title">{t("Your progress")}</h2>
        <p className="list__sub">{t("Sign in to log attempts, mark sends and rate boulders.")}</p>
        <Link to={`/sign-in?next=${encodeURIComponent(location.pathname)}`} className="btn btn--primary"><span>{t("Sign in")}</span></Link>
      </section>
    );
  }
  return <SignedInTracker boulder={boulder} />;
}

function SignedInTracker({ boulder }: { boulder: BoulderDetail }) {
  const saveAttempt = useSetAttempt(boulder.id);
  const setRating = useSetRating(boulder.id);
  const toast = useToast();
  const [attempts, setAttempts] = useState(boulder.viewer?.attempts ?? 0);
  const [completed, setCompleted] = useState(boulder.viewer?.completed ?? false);
  const [pending, setPending] = useState(false);
  const timer = useRef<number>();
  const latest = useRef({ attempts, completed });

  // Flush an unsaved change if the climber leaves the page before the delay passes.
  useEffect(() => () => {
    if (timer.current) {
      window.clearTimeout(timer.current);
      saveAttempt.mutate(latest.current);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  function change(next: { attempts: number; completed: boolean }) {
    const value = { attempts: Math.max(0, Math.min(999, next.attempts)), completed: next.completed };
    if (value.completed && value.attempts === 0) value.attempts = 1;
    setAttempts(value.attempts);
    setCompleted(value.completed);
    latest.current = value;
    setPending(true);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      timer.current = undefined;
      saveAttempt.mutate(latest.current, {
        onSettled: () => setPending(false),
        onError: (e) => {
          toast.error(errorMessage(e));
          setAttempts(boulder.viewer?.attempts ?? 0);
          setCompleted(boulder.viewer?.completed ?? false);
        },
      });
    }, SAVE_DELAY_MS);
  }

  const saving = pending || saveAttempt.isPending;
  const canRate = attempts > 0 && !saving;
  const myRating = boulder.viewer?.rating ?? null;

  return (
    <section className="card tracker" aria-labelledby="tracker-title">
      <div className="tracker__head">
        <h2 id="tracker-title" className="section__title">{t("Your progress")}</h2>
        <span className="tracker__status" aria-live="polite">{saving ? t("Saving…") : boulder.viewer ? t("Saved") : ""}</span>
      </div>

      <div className="tracker__rows">
        <div className="stepper">
          <span className="stepper__label" id="attempts-label">{t("Attempts")}</span>
          <div className="stepper__controls" role="group" aria-labelledby="attempts-label">
            <button type="button" className="stepper__btn" onClick={() => change({ attempts: attempts - 1, completed })}
              disabled={attempts === 0 || (completed && attempts === 1)} aria-label={t("One attempt less")}><Minus aria-hidden /></button>
            <output className="stepper__value" aria-live="polite">{attempts}</output>
            <button type="button" className="stepper__btn" onClick={() => change({ attempts: attempts + 1, completed })} aria-label={t("One more attempt")}><Plus aria-hidden /></button>
          </div>
        </div>

        <div className="tracker__rating">
          <span className="stepper__label">{t("Your rating")}</span>
          <StarInput value={myRating} disabled={!canRate || setRating.isPending}
            onChange={(v) => setRating.mutate(v, { onError: (e) => toast.error(errorMessage(e)) })} />
        </div>
      </div>
      {attempts === 0 && <p className="field__hint">{t("Log an attempt to rate this boulder.")}</p>}
      {!completed && <ProjectSwitch boulder={boulder} />}
      {completed ? (
        <div className="tracker__sent">
          <span className="sent-mark" aria-hidden><Check /></span>
          <p className="tracker__sent-text">
            <strong>{t("Completed")}</strong>
            <span> · {boulder.viewer?.completedAt ? formatDate(boulder.viewer.completedAt) : t("Just now")}</span>
          </p>
          <button type="button" className="tracker__undo" onClick={() => change({ attempts, completed: false })}>
            <RotateCcw aria-hidden />{t("Undo")}
          </button>
        </div>
      ) : (
        <Button block icon={<Check aria-hidden />} onClick={() => change({ attempts, completed: true })}>
          {t("Mark as completed")}
        </Button>
      )}

    </section>
  );
}

/**
 * "Project": a boulder the climber chooses to work on (ADR-067). Only by hand, never from the attempts. Gone once the
 * boulder is completed — a completed project is among the sends — and back if the send is undone. A boulder off the
 * wall can't become one, but one that was can be unmarked. Nothing against a server that doesn't know projects.
 */
function ProjectSwitch({ boulder }: { boulder: BoulderDetail }) {
  const setProject = useSetProject(boulder.id);
  const toast = useToast();
  if (boulder.isProject === undefined) return null;
  const on = boulder.isProject;
  if (!on && boulder.status !== "ACTIVE") return null;
  return (
    <div className="tracker__project">
      <span className="tracker__project-label"><Target aria-hidden /> {t("Project")}</span>
      <button type="button" role="switch" aria-checked={on} aria-label={t("Project")} disabled={setProject.isPending}
        className={`switch ${on ? "is-on" : ""}`}
        onClick={() => setProject.mutate(!on, {
          onSuccess: (r) => toast.success(r.isProject ? t("Added to your projects") : t("Removed from your projects")),
          onError: (e) => toast.error(errorMessage(e)),
        })}>
        <span className="switch__thumb" />
      </button>
    </div>
  );
}
