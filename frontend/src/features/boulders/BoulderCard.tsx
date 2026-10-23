import { useEffect, useRef, type MouseEvent } from "react";
import { Link } from "react-router-dom";
import { Check, MountainSnow, RotateCw, Users } from "lucide-react";
import { IntensityBadge } from "@/features/boulders/intensity";
import type { BoulderSummary } from "@/features/boulders/api";
import { GradeLine, HoldBadge } from "@/features/boulders/BoulderBits";
import { RatingSummaryText } from "@/features/climbing/ClimbingBits";
import { dataLabel } from "@/i18n/data";
import { GymAvatar } from "@/components/GymAvatar";
import { plural, t } from "@/i18n/i18n";

interface BoulderCardProps {
  boulder: BoulderSummary;
  to?: string;
  selectable?: boolean;
  selected?: boolean;
  onToggle?: () => void;
  /** Press and hold to start selecting, the way a phone's home screen arms its icons. */
  onLongPress?: () => void;
  /** Lists that mix gyms (Home): the gym's logo in the photo's corner says where the boulder is. */
  showGym?: boolean;
  /** A narrow column (the moderation queue): grade, holds and sector only. */
  compact?: boolean;
  /**
   * The climber's own state on the photo (sent, tries). Off where staff manage the boulders: there the only mark in
   * that corner is the selection.
   */
  showOwnState?: boolean;
}

const LONG_PRESS_MS = 450;

/** Image-first card for scanning many boulders: grade and holds are always both visible, and the holds always labelled. */
export function BoulderCard({ boulder, to, selectable, selected, onToggle, onLongPress, showGym = false, compact = false, showOwnState = true }: BoulderCardProps) {
  const timer = useRef<number | null>(null);
  const fired = useRef(false);

  const cancel = () => { if (timer.current !== null) { window.clearTimeout(timer.current); timer.current = null; } };
  const start = () => {
    if (!onLongPress) return;
    fired.current = false;
    timer.current = window.setTimeout(() => {
      fired.current = true;
      // Whatever the press began to select (a name, a sector) is let go: the press selected the boulder, not text.
      window.getSelection()?.removeAllRanges();
      onLongPress();
    }, LONG_PRESS_MS);
  };
  // The hold has already selected the card, so letting the tap through would open the boulder at the same time.
  const swallowClickAfterHold = (e: MouseEvent) => { if (fired.current) { e.preventDefault(); fired.current = false; } };

  const pressHandlers = onLongPress
    ? {
        onPointerDown: start,
        onPointerUp: cancel,
        onPointerLeave: cancel,
        onPointerCancel: cancel,
        onClick: swallowClickAfterHold,
        onContextMenu: (e: MouseEvent) => { if (timer.current !== null || fired.current) e.preventDefault(); },
      }
    : {};

  useEffect(() => cancel, []);

  // The hold turned this card into a selectable one under the finger: lifting that finger must not toggle it straight
  // back off. Only that click is ignored; a new press, or the keyboard, toggles as always.
  const releasing = useRef(false);
  const toggleHandlers = {
    onPointerDown: () => { releasing.current = false; fired.current = false; },
    onPointerUp: () => { if (fired.current) { fired.current = false; releasing.current = true; } },
    onClick: () => {
      if (releasing.current || fired.current) { releasing.current = false; fired.current = false; return; }
      onToggle?.();
    },
  };

  // Fixed anatomy, so a card never grows with what it happens to hold. On the photo: the grade (bottom left, as on the
  // boulder's page) with its intensity, the points (bottom right), the climber's own state (top right), the gym's logo in lists that mix gyms (top left). Under it: the
  // name when the boulder has one, the holds, and the sector with how it is rated and how many sent it.
  const name = boulder.name?.trim() || null;
  const sends = boulder.completions ?? 0;
  const tries = boulder.viewer && !boulder.viewer.completed ? boulder.viewer.attempts : 0;
  const body = (
    <>
      <div className="boulder-card__photo">
        {boulder.photoUrl
          ? <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" draggable={false} />
          : <span className="boulder-card__gone" aria-hidden><MountainSnow /></span>}
        {/* Top left: the gym's logo where a list mixes gyms, then whether the boulder is still on the wall. */}
        {(showGym || boulder.status !== "ACTIVE") && (
          <span className="boulder-card__lead">
            {showGym && (
              <span className="boulder-card__gym" title={boulder.gymName}>
                <GymAvatar name={boulder.gymName} logoUrl={boulder.gymLogoUrl ?? null} size={30} />
                <span className="sr-only">{t("At {gym}", { gym: boulder.gymName })}</span>
              </span>
            )}
            {boulder.status === "REMOVED" && <span className="boulder-card__ribbon">{t("Removed")}</span>}
            {boulder.status === "DELETED" && <span className="boulder-card__ribbon">{t("Deleted")}</span>}
          </span>
        )}
        {!selectable && showOwnState && (
          // Top right: the climber's own state.
          <span className="boulder-card__corner">
            {boulder.viewer?.completed && <span className="boulder-card__sent" aria-label={t("You completed this")}><Check aria-hidden /></span>}
            {tries > 0 && (
              <span className="boulder-card__tries" title={plural(tries, "{count} try", "{count} tries")}>
                <RotateCw aria-hidden /><span aria-hidden>{tries}</span>
                <span className="sr-only">{plural(tries, "{count} try", "{count} tries")}</span>
              </span>
            )}
          </span>
        )}
        {selectable && <span className={`boulder-card__check ${selected ? "is-on" : ""}`} aria-hidden><Check /></span>}
        <span className="boulder-card__overlay">
          <span className="boulder-card__grades">
            <GradeLine grades={boulder.grades} />
            {boulder.intensity && <span className="boulder-card__intensity"><IntensityBadge intensity={boulder.intensity} compact /></span>}
          </span>
          {boulder.points != null && (
            <span className="boulder-card__points" title={plural(boulder.points, "Worth {count} point", "Worth {count} points")}>
              <span aria-hidden>{t("+{points} pt", { points: boulder.points })}</span>
              <span className="sr-only">{plural(boulder.points, "Worth {count} point", "Worth {count} points")}</span>
            </span>
          )}
        </span>
      </div>
      <div className="boulder-card__body">
        {name && <p className="boulder-card__name">{name}</p>}
        <div className="boulder-card__holds"><HoldBadge color={boulder.holdColor} compact /></div>
        <div className="boulder-card__foot">
          <p className="boulder-card__sector">{boulder.sectorName}</p>
          <span className="boulder-card__stats">
            <RatingSummaryText rating={boulder.rating} compact />
            {sends > 0 && (
              <span className="boulder-card__sends" title={plural(sends, "Completed by 1 person", "Completed by {count} people")}>
                <Users aria-hidden />{sends}
                <span className="sr-only">{plural(sends, "Completed by 1 person", "Completed by {count} people")}</span>
              </span>
            )}
          </span>
        </div>
      </div>
    </>
  );

  if (selectable) {
    return (
      <button type="button" className={`boulder-card boulder-card--pressable ${selected ? "boulder-card--selected" : ""} ${compact ? "boulder-card--compact" : ""}`} {...toggleHandlers} aria-pressed={selected}
        aria-label={[name, boulder.grades.map((g) => dataLabel(g.label)).join(" ") || t("Ungraded"), boulder.sectorName].filter(Boolean).join(", ")}>
        {body}
      </button>
    );
  }
  return (
    <Link to={to ?? `/boulders/${boulder.id}`} className={`boulder-card ${onLongPress ? "boulder-card--pressable" : ""} ${compact ? "boulder-card--compact" : ""}`}
      draggable={onLongPress ? false : undefined} {...pressHandlers}>
      {body}
    </Link>
  );
}
