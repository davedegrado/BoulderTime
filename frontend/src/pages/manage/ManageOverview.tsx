import { Link } from "react-router-dom";
import { EyeOff, Flag, History, Layers, MailPlus, Mountain, Users, Video } from "lucide-react";
import { useModerationSummary } from "@/features/community/api";
import { useBoulders } from "@/features/boulders/api";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { useGymInvitations, useStaff } from "@/features/staff/api";
import { t } from "@/i18n/i18n";

export function ManageOverview() {
  const { gym } = useManagedGym();
  const sectors = useSectors(gym.id);
  const staff = useStaff(gym.id);
  const invitations = useGymInvitations(gym.id);
  const active = useBoulders(gym.id, { status: "ACTIVE" });
  const removed = useBoulders(gym.id, { status: "REMOVED" });
  const moderation = useModerationSummary(gym.id);
  const base = `/manage/${gym.slug}`;
  const count = (n: number | undefined) => (n === undefined ? "–" : n);
  const activeSectors = sectors.data?.filter((s) => s.isActive).length;

  return (
    <div className="stack">
      {gym.status !== "ACTIVE" && (
        <div className="notice" role="note">
          <EyeOff aria-hidden />
          <p>{gym.status === "DRAFT"
            ? t("This gym isn't public yet. Set up sectors and staff; the BoulderTime team will publish it.")
            : t("This gym is archived and hidden from climbers.")}</p>
        </div>
      )}
      <div className="stats">
        <Link to={`${base}/moderation`} className={`stat ${moderation.data?.pendingVideos ? "stat--attention" : ""}`}>
          <Video className="stat__icon" aria-hidden />
          <span className="stat__value">{count(moderation.data?.pendingVideos)}</span>
          <span className="stat__label">{t("Videos awaiting approval")}</span>
        </Link>
        <Link to={`${base}/moderation`} className={`stat ${moderation.data?.pendingReports ? "stat--attention" : ""}`}>
          <Flag className="stat__icon" aria-hidden />
          <span className="stat__value">{count(moderation.data?.pendingReports)}</span>
          <span className="stat__label">{t("Open reports")}</span>
        </Link>
        <Link to={`${base}/boulders`} className="stat">
          <Mountain className="stat__icon" aria-hidden />
          <span className="stat__value">{count(active.data?.pages[0]?.total)}</span>
          <span className="stat__label">{t("Boulders on the wall")}</span>
        </Link>
        <Link to={`${base}/boulders`} className="stat">
          <History className="stat__icon" aria-hidden />
          <span className="stat__value">{count(removed.data?.pages[0]?.total)}</span>
          <span className="stat__label">{t("Removed boulders")}</span>
        </Link>
        <Link to={`${base}/sectors`} className="stat">
          <Layers className="stat__icon" aria-hidden />
          <span className="stat__value">{count(activeSectors)}</span>
          <span className="stat__label">{t("Active sectors")}</span>
        </Link>
        <Link to={`${base}/staff`} className="stat">
          <Users className="stat__icon" aria-hidden />
          <span className="stat__value">{count(staff.data?.length)}</span>
          <span className="stat__label">{t("Staff members")}</span>
        </Link>
        <Link to={`${base}/staff`} className="stat">
          <MailPlus className="stat__icon" aria-hidden />
          <span className="stat__value">{count(invitations.data?.length)}</span>
          <span className="stat__label">{t("Open invitations")}</span>
        </Link>
      </div>
    </div>
  );
}
