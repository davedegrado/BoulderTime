import { useEffect, useRef, type MouseEvent } from "react";
import { Link } from "react-router-dom";
import { Check, MountainSnow } from "lucide-react";
import type { BoulderSummary } from "@/features/boulders/api";
import { GradeLine, HoldBadge } from "@/features/boulders/BoulderBits";
import { RatingSummaryText } from "@/features/climbing/ClimbingBits";
import { plural, t } from "@/i18n/i18n";

interface BoulderCardProps {
  boulder: BoulderSummary;
  to?: string;
  selectable?: boolean;
  selected?: boolean;
  onToggle?: () => void;
  /** Press and hold to start selecting, the way a phone's home screen arms its icons. */
  onLongPress?: () => void;
}

const LONG_PRESS_MS = 450;

/** Image-first card for scanning many boulders: grade and holds are always both visible and always labelled. */
export function BoulderCard({ boulder, to, selectable, selected, onToggle, onLongPress }: BoulderCardProps) {
  const timer = useRef<number | null>(null);
  const fired = useRef(false);

  const cancel = () => { if (timer.current !== null) { window.clearTimeout(timer.current); timer.current = null; } };
  const start = () => {
    if (!onLongPress) return;
    fired.current = false;
    timer.current = window.setTimeout(() => { fired.current = true; onLongPress(); }, LONG_PRESS_MS);
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

  const body = (
    <>
      <div className="boulder-card__photo">
        {boulder.photoUrl
          ? <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" />
          : <span className="boulder-card__gone" aria-hidden><MountainSnow /></span>}
        {boulder.status === "REMOVED" && <span className="boulder-card__ribbon">{t("Removed")}</span>}
        {boulder.status === "DELETED" && <span className="boulder-card__ribbon">{t("Deleted")}</span>}
        {!selectable && boulder.viewer?.completed && <span className="boulder-card__sent" aria-label={t("You completed this")}><Check aria-hidden /></span>}
        {!selectable && boulder.viewer && !boulder.viewer.completed && boulder.viewer.attempts > 0 && (
          <span className="boulder-card__tries">{plural(boulder.viewer.attempts, "{count} try", "{count} tries")}</span>
        )}
        {selectable && <span className={`boulder-card__check ${selected ? "is-on" : ""}`} aria-hidden><Check /></span>}
      </div>
      <div className="boulder-card__body">
        <GradeLine grades={boulder.grades} />
        <HoldBadge color={boulder.holdColor} compact />
        <div className="boulder-card__foot">
          <p className="boulder-card__sector">{boulder.sectorName}</p>
          <RatingSummaryText rating={boulder.rating} compact />
        </div>
      </div>
    </>
  );

  if (selectable) {
    return (
      <button type="button" className={`boulder-card ${selected ? "boulder-card--selected" : ""}`} onClick={onToggle} aria-pressed={selected}
        aria-label={`${boulder.grades.map((g) => g.label).join(" ")}, ${boulder.sectorName}`}>
        {body}
      </button>
    );
  }
  return <Link to={to ?? `/boulders/${boulder.id}`} className="boulder-card" {...pressHandlers}>{body}</Link>;
}
