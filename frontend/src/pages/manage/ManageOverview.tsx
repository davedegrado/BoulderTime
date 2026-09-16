import { Link } from "react-router-dom";
import { EyeOff, History, Layers, MailPlus, Mountain, Users } from "lucide-react";
import { useBoulders } from "@/features/boulders/api";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { useGymInvitations, useStaff } from "@/features/staff/api";

export function ManageOverview() {
  const { gym } = useManagedGym();
  const sectors = useSectors(gym.id);
  const staff = useStaff(gym.id);
  const invitations = useGymInvitations(gym.id);
  const active = useBoulders(gym.id, { status: "ACTIVE" });
  const removed = useBoulders(gym.id, { status: "REMOVED" });
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
        <Link to={`${base}/boulders`} className="stat">
          <Mountain className="stat__icon" aria-hidden />
          <span className="stat__value">{count(active.data?.pages[0]?.total)}</span>
          <span className="stat__label">Boulders on the wall</span>
        </Link>
        <Link to={`${base}/boulders`} className="stat">
          <History className="stat__icon" aria-hidden />
          <span className="stat__value">{count(removed.data?.pages[0]?.total)}</span>
          <span className="stat__label">Removed boulders</span>
        </Link>
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
      <p className="section__footnote">Video moderation and reports appear here as those features arrive.</p>
    </div>
  );
}
