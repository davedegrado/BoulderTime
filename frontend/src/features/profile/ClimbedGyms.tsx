import { Link } from "react-router-dom";
import { ChevronRight, MapPin } from "lucide-react";
import type { ClimbedGym, HighestGrade } from "@/features/climbing/api";
import { ColorGrade } from "@/features/boulders/BoulderBits";
import { GymAvatar } from "@/components/GymAvatar";
import { formatDate } from "@/lib/format";
import { dataLabel } from "@/i18n/data";
import { plural, t } from "@/i18n/i18n";

/** A grade as the profile's highest grades show it: the colour for a colour scale, the label otherwise. */
function Grade({ grade: g }: { grade: HighestGrade }) {
  return g.systemType === "COLOR" && g.colorHex
    ? <ColorGrade label={g.label} colorHex={g.colorHex} colorHex2={g.colorHex2} size="sm" />
    : <span className="grade grade--text grade--sm">{dataLabel(g.label)}</span>;
}

/**
 * The gyms where the person completed at least one boulder (ADR-064), latest send first: logo, name, town, how many
 * distinct boulders, the latest send, and the highest grade in each of the gym's scales, side by side and never
 * compared with each other. Each card opens the gym.
 */
export function ClimbedGymList({ gyms }: { gyms: ClimbedGym[] }) {
  return (
    <ul className="climbed-gyms">
      {gyms.map((c) => {
        const highest = c.highest.map((h) => `${dataLabel(h.label)} (${dataLabel(h.systemName)})`).join(", ");
        return (
          <li key={c.gym.id}>
            <Link to={`/gyms/${c.gym.slug}`} className="climbed-gym">
              <GymAvatar name={c.gym.name} logoUrl={c.gym.logoUrl} size={48} />
              <span className="climbed-gym__main">
                <span className="climbed-gym__name">{c.gym.name}</span>
                {/* The town is short and can give way; the highest grades sit beside it, the count gets its own line. */}
                <span className="climbed-gym__facts">
                  <span className="climbed-gym__where"><MapPin aria-hidden /> {c.gym.city}</span>
                  {c.highest.length > 0 && (
                    <span className="climbed-gym__grades">
                      <span className="sr-only">{t("Highest grade: {grades}", { grades: highest })}</span>
                      <span className="climbed-gym__grades-row" aria-hidden>{c.highest.map((h) => <Grade key={h.gradeSystemId} grade={h} />)}</span>
                    </span>
                  )}
                </span>
                <span className="climbed-gym__count">{plural(c.completed, "1 boulder completed", "{count} boulders completed")}</span>
                {c.lastCompletedAt && <span className="climbed-gym__last">{t("Last send {date}", { date: formatDate(c.lastCompletedAt) })}</span>}
              </span>
              <ChevronRight className="list__chevron" aria-hidden />
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
