import { Outlet } from "react-router-dom";
import { ShieldAlert } from "lucide-react";
import { useCurrentUser } from "@/features/users/api";
import { SubNav } from "@/components/SubNav";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";

/** Platform-admin shell. Hiding it for non-admins is cosmetic; every admin endpoint re-checks the flag server-side. */
export function AdminLayout() {
  const me = useCurrentUser();
  if (me.isPending) return <LoadingState />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  if (!me.data.isPlatformAdmin) {
    return <EmptyState icon={<ShieldAlert />} title="BoulderTime administrators only" body="This area manages the platform itself." />;
  }
  return (
    <div className="page">
      <header className="page__header">
        <p className="manage-head__eyebrow">BoulderTime admin</p>
        <h1 className="page__title">Platform</h1>
      </header>
      <SubNav label="Admin sections" items={[
        { to: "/admin", label: "Dashboard", end: true },
        { to: "/admin/candidates", label: "Suggestions" },
        { to: "/admin/reports", label: "Reports" },
        { to: "/admin/gyms", label: "Gyms" },
        { to: "/admin/users", label: "Users" },
      ]} />
      <Outlet />
    </div>
  );
}
