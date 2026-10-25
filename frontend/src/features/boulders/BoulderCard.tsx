import { Link } from "react-router-dom";
import { Check, MountainSnow, RotateCw, Users, Target } from "lucide-react";
import { IntensityBadge } from "@/features/boulders/intensity";
import { useLongPress } from "@/features/boulders/useLongPress";
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
  /** The mark of an open project; off in the row of projects itself, where every card is one. */
  showProjectMark?: boolean;
}

/** Image-first card for scanning many boulders: grade and holds are always both visible, and the holds always labelled. */
export function BoulderCard({ boulder, to, selectable, selected, onToggle, onLongPress, showGym = false, compact = false, showOwnState = true, showProjectMark = true }: BoulderCardProps) {
  const { pressHandlers, toggleHandlers } = useLongPress(onLongPress, onToggle);

  // Fixed anatomy, so a card never grows with what it happens to hold. On the photo: the grade (bottom left, as on the
  // boulder's page) with its intensity, the points (bottom right), the climber's own state (top right), the gym's logo in lists that mix gyms (top left). Under it: the
  // name when the boulder has one, the holds, and the sector with how it is rated and how many sent it.
  const name = boulder.name?.trim() || null;
  const sends = boulder.completions ?? 0;
  const tries = boulder.viewer && !boulder.viewer.completed ? boulder.viewer.attempts : 0;
  const projectMark = showOwnState && !selectable && showProjectMark && Boolean(boulder.isProject) && !boulder.viewer?.completed;
  const body = (
    <>
      <div className="boulder-card__photo">
        {boulder.photoUrl
          ? <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" draggable={false} />
          : <span className="boulder-card__gone" aria-hidden><MountainSnow /></span>}
        {/* Top left: the gym's logo where a list mixes gyms, whether the boulder is still on the wall, and whether it is
            one of the climber's open projects (a completed project is just a send). */}
        {(showGym || boulder.status !== "ACTIVE" || projectMark) && (
          <span className="boulder-card__lead">
            {showGym && (
              <span className="boulder-card__gym" title={boulder.gymName}>
                <GymAvatar name={boulder.gymName} logoUrl={boulder.gymLogoUrl ?? null} size={30} />
                <span className="sr-only">{t("At {gym}", { gym: boulder.gymName })}</span>
              </span>
            )}
            {boulder.status === "REMOVED" && <span className="boulder-card__ribbon">{t("Removed")}</span>}
            {boulder.status === "DELETED" && <span className="boulder-card__ribbon">{t("Deleted")}</span>}
            {projectMark && (
              <span className="boulder-card__project" title={t("Your project")}><Target aria-hidden /><span className="sr-only">{t("Your project")}</span></span>
            )}
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
      {...pressHandlers}>
      {body}
    </Link>
  );
}
