import { Link } from "react-router-dom";
import { Check } from "lucide-react";
import type { BoulderSummary } from "@/features/boulders/api";
import { GradeLine, HoldBadge } from "@/features/boulders/BoulderBits";
import { RatingSummaryText } from "@/features/climbing/ClimbingBits";

interface BoulderCardProps {
  boulder: BoulderSummary;
  to?: string;
  selectable?: boolean;
  selected?: boolean;
  onToggle?: () => void;
}

/** Image-first card for scanning many boulders: grade and holds are always both visible and always labelled. */
export function BoulderCard({ boulder, to, selectable, selected, onToggle }: BoulderCardProps) {
  const body = (
    <>
      <div className="boulder-card__photo">
        <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" />
        {boulder.status === "REMOVED" && <span className="boulder-card__ribbon">Removed</span>}
        {!selectable && boulder.viewer?.completed && <span className="boulder-card__sent" aria-label="You completed this"><Check aria-hidden /></span>}
        {!selectable && boulder.viewer && !boulder.viewer.completed && boulder.viewer.attempts > 0 && (
          <span className="boulder-card__tries">{boulder.viewer.attempts} {boulder.viewer.attempts === 1 ? "try" : "tries"}</span>
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
  return <Link to={to ?? `/boulders/${boulder.id}`} className="boulder-card">{body}</Link>;
}
