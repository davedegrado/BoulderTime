import type { KeyboardEvent } from "react";
import { Bell } from "lucide-react";
import type { Sector } from "@/features/gyms/api";
import { plural, t } from "@/i18n/i18n";

export interface FloorPlan { url: string; width: number; height: number }

/** The floor plan of a gym that has one, or null. */
export function floorPlanOf(gym: { floorPlanUrl?: string | null; floorPlanWidth?: number | null; floorPlanHeight?: number | null }): FloorPlan | null {
  return gym.floorPlanUrl && gym.floorPlanWidth && gym.floorPlanHeight
    ? { url: gym.floorPlanUrl, width: gym.floorPlanWidth, height: gym.floorPlanHeight }
    : null;
}

interface Props {
  plan: FloorPlan;
  sectors: Sector[];
  /** Tapping a sector (its area or its label). Without it, the map is a picture. */
  onSelect?: (sector: Sector) => void;
  /** The sector to stand out, the others fading back (the boulder page's "where is it"). */
  highlightId?: string | null;
  className?: string;
}

/**
 * The gym's floor plan with its sectors drawn on it. The outlines are kept in fractions of the plan's size, so one
 * drawing fits the plan at any width. A green dot marks sectors with new boulders this week, a bell the ones the
 * climber follows.
 */
export function SectorMap({ plan, sectors, onSelect, highlightId, className = "" }: Props) {
  const drawn = sectors.filter((s) => s.isActive && s.zone && s.zone.points.length >= 3);
  const { width: w, height: h } = plan;

  function keyed(e: KeyboardEvent, s: Sector) {
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onSelect?.(s); }
  }

  return (
    <div className={`sector-map ${highlightId ? "has-highlight" : ""} ${className}`} style={{ aspectRatio: `${w} / ${h}` }}>
      <img src={plan.url} alt="" className="sector-map__plan" draggable={false} />
      <svg className="sector-map__zones" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" aria-hidden={!onSelect}>
        {drawn.map((s) => (
          <polygon key={s.id}
            className={`sector-map__zone ${s.id === highlightId ? "is-highlighted" : ""} ${(s.newThisWeek ?? 0) > 0 ? "is-new" : ""}`}
            points={s.zone!.points.map((p) => `${p.x * w},${p.y * h}`).join(" ")}
            {...(onSelect ? { role: "button", tabIndex: 0, "aria-label": t("Sector {name}", { name: s.name }), onClick: () => onSelect(s), onKeyDown: (e: KeyboardEvent) => keyed(e, s) } : {})} />
        ))}
      </svg>
      {drawn.map((s) => {
        const highlighted = s.id === highlightId;
        const isNew = (s.newThisWeek ?? 0) > 0;
        // A sector drawn without its name keeps only its markers (new boulders, followed), unless it is the one
        // being pointed at.
        const named = !s.zone!.hideLabel || highlighted;
        if (!named && !isNew && !s.isFollowing) return null;
        return (
          <span key={s.id} className={`sector-map__label ${highlighted ? "is-highlighted" : ""} ${named ? edge(s.zone!.label.x) : "is-bare"}`}
            style={{ left: `${s.zone!.label.x * 100}%`, top: `${s.zone!.label.y * 100}%` }}
            onClick={onSelect ? () => onSelect(s) : undefined} aria-hidden>
            {isNew && <i className="sector-map__new" title={plural(s.newThisWeek ?? 0, "1 new", "{count} new")} />}
            {named && s.name}
            {s.isFollowing && <Bell className="sector-map__bell" />}
          </span>
        );
      })}
    </div>
  );
}

/** Near the sides, a label hangs inwards instead of centred on its point, so it never runs off the plan. */
const edge = (x: number) => (x < 0.18 ? "is-start" : x > 0.82 ? "is-end" : "");
