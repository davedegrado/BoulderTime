import { Outlet } from "react-router-dom";
import { ShieldAlert } from "lucide-react";
import { useCurrentUser } from "@/features/users/api";
import { SubNav } from "@/components/SubNav";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

/** Platform-admin shell. Hiding it for non-admins is cosmetic; every admin endpoint re-checks the flag server-side. */
export function AdminLayout() {
  const me = useCurrentUser();
  if (me.isPending) return <LoadingState />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  if (!me.data.isPlatformAdmin) {
    return <EmptyState icon={<ShieldAlert />} title={t(t("BoulderTime administrators only"))} body={t(t("This area manages the platform itself."))} />;
  }
  return (
    <div className="page">
      <header className="page__header">
        <p className="manage-head__eyebrow">{t("BoulderTime admin")}</p>
        <h1 className="page__title">{t("Platform")}</h1>
      </header>
      <SubNav label={t(t("Admin sections"))} items={[
        { to: "/admin", label: t("Dashboard"), end: true },
        { to: "/admin/candidates", label: t("Suggestions") },
        { to: "/admin/reports", label: t("Reports") },
        { to: "/admin/gyms", label: t("Gyms") },
        { to: "/admin/users", label: t("Users") },
      ]} />
      <Outlet />
    </div>
  );
}
