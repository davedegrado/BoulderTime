import { Link } from "react-router-dom";
import { EyeOff, Layers, MailPlus, Users } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { useGymInvitations, useStaff } from "@/features/staff/api";

export function ManageOverview() {
  const { gym } = useManagedGym();
  const sectors = useSectors(gym.id);
  const staff = useStaff(gym.id);
  const invitations = useGymInvitations(gym.id);
  const base = `/manage/${gym.slug}`;
  const count = (n: number | undefined) => (n === undefined ? "–" : n);
  const activeSectors = sectors.data?.filter((s) => s.isActive).length;

  return (
    <div className="stack">
      {gym.status !== "ACTIVE" && (
        <div className="notice" role="note">
          <EyeOff aria-hidden />
          <p>{gym.status === "DRAFT"
            ? "This gym isn't public yet. Set up sectors and staff; the BoulderTime team will publish it."
            : "This gym is archived and hidden from climbers."}</p>
        </div>
      )}
      <div className="stats">
        <Link to={`${base}/sectors`} className="stat">
          <Layers className="stat__icon" aria-hidden />
          <span className="stat__value">{count(activeSectors)}</span>
          <span className="stat__label">Active sectors</span>
        </Link>
        <Link to={`${base}/staff`} className="stat">
          <Users className="stat__icon" aria-hidden />
          <span className="stat__value">{count(staff.data?.length)}</span>
          <span className="stat__label">Staff members</span>
        </Link>
        <Link to={`${base}/staff`} className="stat">
          <MailPlus className="stat__icon" aria-hidden />
          <span className="stat__value">{count(invitations.data?.length)}</span>
          <span className="stat__label">Open invitations</span>
        </Link>
      </div>
      <p className="section__footnote">Boulders, video moderation and reports appear here as those features arrive.</p>
    </div>
  );
}
