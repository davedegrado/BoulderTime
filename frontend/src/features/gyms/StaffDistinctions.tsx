import { Link } from "react-router-dom";
import { Handshake, Trophy } from "lucide-react";
import type { StaffDistinction } from "@/features/climbing/api";
import { t } from "@/i18n/i18n";
import { roleLabel } from "@/lib/format";

/**
 * Distinctions shown on a climber's profile because they are staff of that gym.
 * Derived from the staff role, so they vanish on their own when the role or the gym's status ends.
 */
export function StaffDistinctions({ distinctions }: { distinctions: StaffDistinction[] }) {
  if (distinctions.length === 0) return null;
  return (
    <ul className="staff-distinctions">
      {distinctions.map((d) => (
        <li key={d.gymId}>
          <Link to={`/gyms/${d.gymSlug}`} className={`gym-badge ${d.isFoundingGym ? "gym-badge--founding" : "gym-badge--partner"}`}>
            {d.isFoundingGym ? <Trophy aria-hidden /> : <Handshake aria-hidden />}
            {d.isFoundingGym ? t("Founding") : t("Early Partner")} · {roleLabel[d.role]}
            <span className="gym-badge__gym">{d.gymName}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
