import { Link } from "react-router-dom";
import { ChevronRight, MapPin } from "lucide-react";
import type { FavoriteSector } from "@/features/climbing/api";
import type { Sector } from "@/features/gyms/api";
import { SectorMap } from "@/features/gyms/SectorMap";
import { plural, t } from "@/i18n/i18n";

/**
 * The sector where someone completed the most boulders. When the gym drew its plan, the sector is shown on it,
 * picked out as on a boulder's "where it is"; tapping opens the gym's boulders in that sector.
 */
export function FavoriteSectorCard({ sector, isMe }: { sector: FavoriteSector; isMe: boolean }) {
  const plan = sector.floorPlanUrl && sector.floorPlanWidth && sector.floorPlanHeight && sector.zone
    ? { url: sector.floorPlanUrl, width: sector.floorPlanWidth, height: sector.floorPlanHeight }
    : null;
  // The map needs only this one sector: the rest of the plan stays quiet behind it.
  const onPlan = [{ id: sector.sectorId, name: sector.sectorName, zone: sector.zone, isActive: true, newThisWeek: 0, isFollowing: false } as Sector];
  const maxWidth = plan ? `min(100%, ${Math.round(200 * plan.width / plan.height)}px)` : undefined;

  return (
    <section className="section card favorite-sector" aria-labelledby="favorite-sector-title">
      <h2 id="favorite-sector-title" className="section__title">{isMe ? t("Your sector") : t("Favourite sector")}</h2>
      <Link to={`/gyms/${sector.gymSlug}?sector=${sector.sectorId}`} className="favorite-sector__link">
        <span className="favorite-sector__icon" aria-hidden><MapPin /></span>
        <span className="favorite-sector__text">
          <span className="favorite-sector__name">{sector.sectorName}</span>
          <span className="list__sub">{sector.gymName} · {plural(sector.completed, "1 boulder completed here", "{count} boulders completed here")}</span>
        </span>
        <ChevronRight className="list__chevron" aria-hidden />
      </Link>
      {plan && (
        <div className="favorite-sector__map" style={{ maxWidth }}>
          <SectorMap plan={plan} sectors={onPlan} highlightId={sector.sectorId} labels={false} className="sector-map--mini" />
        </div>
      )}
    </section>
  );
}
