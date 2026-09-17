import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Check, Minus, Plus, RotateCcw } from "lucide-react";
import type { BoulderDetail } from "@/features/boulders/api";
import { useSetAttempt, useSetRating } from "@/features/climbing/api";
import { RatingSummaryText, StarInput } from "@/features/climbing/ClimbingBits";
import { useAuth } from "@/auth/AuthProvider";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";

const SAVE_DELAY_MS = 700;

/**
 * Attempts, completion and rating for the signed-in climber.
 * Changes apply instantly on screen and are saved after a short pause, so fast tapping sends one request
 * with the final value instead of racing several.
 */
export function ProgressTracker({ boulder }: { boulder: BoulderDetail }) {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) {
    return (
      <section className="card tracker" aria-labelledby="tracker-title">
        <h2 id="tracker-title" className="section__title">Your progress</h2>
        <p className="list__sub">Sign in to log attempts, mark sends and rate boulders.</p>
        <Link to={`/sign-in?next=${encodeURIComponent(location.pathname)}`} className="btn btn--primary"><span>Sign in</span></Link>
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
        <h2 id="tracker-title" className="section__title">Your progress</h2>
        <span className="tracker__status" aria-live="polite">{saving ? "Saving…" : boulder.viewer ? "Saved" : ""}</span>
      </div>

      {completed ? (
        <div className="tracker__sent">
          <span className="sent-mark sent-mark--lg" aria-hidden><Check /></span>
          <div>
            <p className="list__title">Completed</p>
            <p className="list__sub">{boulder.viewer?.completedAt ? formatDate(boulder.viewer.completedAt) : "Just now"} · {attempts} {attempts === 1 ? "attempt" : "attempts"}</p>
          </div>
          <Button variant="ghost" icon={<RotateCcw aria-hidden />} onClick={() => change({ attempts, completed: false })}>Undo</Button>
        </div>
      ) : (
        <Button block className="btn--lg" icon={<Check aria-hidden />} onClick={() => change({ attempts, completed: true })}>
          Mark as completed
        </Button>
      )}

      <div className="stepper">
        <span className="stepper__label" id="attempts-label">Attempts</span>
        <div className="stepper__controls" role="group" aria-labelledby="attempts-label">
          <button type="button" className="stepper__btn" onClick={() => change({ attempts: attempts - 1, completed })}
            disabled={attempts === 0 || (completed && attempts === 1)} aria-label="One attempt less"><Minus aria-hidden /></button>
          <output className="stepper__value" aria-live="polite">{attempts}</output>
          <button type="button" className="stepper__btn" onClick={() => change({ attempts: attempts + 1, completed })} aria-label="One more attempt"><Plus aria-hidden /></button>
        </div>
      </div>

      <div className="tracker__rating">
        <span className="stepper__label">Your rating</span>
        <StarInput value={myRating} disabled={!canRate || setRating.isPending}
          onChange={(v) => setRating.mutate(v, { onError: (e) => toast.error(errorMessage(e)) })} />
        {attempts === 0 && <p className="field__hint">Log an attempt to rate this boulder.</p>}
      </div>
      <p className="tracker__community">Community: <RatingSummaryText rating={boulder.rating} /></p>
    </section>
  );
}
