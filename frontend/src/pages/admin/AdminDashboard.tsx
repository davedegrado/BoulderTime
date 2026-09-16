import { Link } from "react-router-dom";
import { Building2, Inbox, MapPinned, Users } from "lucide-react";
import { useAdminDashboard } from "@/features/admin/api";
import { ErrorState, LoadingState } from "@/components/States";

export function AdminDashboard() {
  const dash = useAdminDashboard();
  if (dash.isPending) return <LoadingState label="Loading dashboard" />;
  if (dash.isError) return <ErrorState error={dash.error} onRetry={() => dash.refetch()} />;
  const d = dash.data;
  return (
    <div className="stack">
      <div className="stats">
        <Link to="/admin/candidates" className={`stat ${d.pendingGymCandidates > 0 ? "stat--attention" : ""}`}>
          <Inbox className="stat__icon" aria-hidden />
          <span className="stat__value">{d.pendingGymCandidates}</span>
          <span className="stat__label">Pending gym suggestions</span>
        </Link>
        <Link to="/admin/gyms" className="stat">
          <Building2 className="stat__icon" aria-hidden />
          <span className="stat__value">{d.totalGyms}</span>
          <span className="stat__label">Gyms</span>
        </Link>
        <Link to="/admin/gyms?status=ACTIVE" className="stat">
          <MapPinned className="stat__icon" aria-hidden />
          <span className="stat__value">{d.activeGyms}</span>
          <span className="stat__label">Live gyms</span>
        </Link>
        <Link to="/admin/users" className="stat">
          <Users className="stat__icon" aria-hidden />
          <span className="stat__value">{d.users}</span>
          <span className="stat__label">Users</span>
        </Link>
      </div>
      <p className="section__footnote">Pending reports join this dashboard with moderation (Phase 5).</p>
    </div>
  );
}
