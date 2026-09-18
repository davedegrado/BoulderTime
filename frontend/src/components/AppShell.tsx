import { NavLink, Outlet, Link } from "react-router-dom";
import { Home, Compass, Activity, Bell, UserRound, LogIn, ShieldCheck } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Logo } from "@/components/Logo";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { GymAvatar } from "@/components/GymAvatar";
import { useUnreadCount } from "@/features/notifications/api";
import { OfflineBanner, RouteAnnouncer } from "@/components/AppChrome";
import { Suspense } from "react";
import { LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

interface NavItem { to: string; label: string; icon: LucideIcon; requiresAuth: boolean }

/**
 * "Gyms" from the product brief lives inside Explore (discovery) and Home (followed gyms)
 * to keep the phone tab bar at five thumb-reachable targets.
 */
const NAV: NavItem[] = [
  { to: "/", label: "Home", icon: Home, requiresAuth: false },
  { to: "/explore", label: "Explore", icon: Compass, requiresAuth: false },
  { to: "/activity", label: "Activity", icon: Activity, requiresAuth: true },
  { to: "/notifications", label: "Alerts", icon: Bell, requiresAuth: true },
  { to: "/profile", label: "Profile", icon: UserRound, requiresAuth: true },
];

export function AppShell() {
  const { session } = useAuth();
  const me = useCurrentUser();
  const items = NAV.filter((i) => session || !i.requiresAuth);
  const staffGyms = me.data?.staffGyms ?? [];
  const isAdmin = me.data?.isPlatformAdmin ?? false;
  const unread = useUnreadCount().data?.unread ?? 0;
  const badge = (to: string) => to === "/notifications" && unread > 0
    ? <span className="nav-badge" aria-label={`${unread} unread`}>{unread > 99 ? "99+" : unread}</span>
    : null;

  return (
    <div className="shell">
      <a className="skip-link" href="#main">{t("Skip to content")}</a>

      <aside className="sidebar" aria-label={t("Primary")}>
        <Link to="/" className="sidebar__brand" aria-label={t("BoulderTime home")}>
          <Logo variant="horizontal" onDark height={40} />
        </Link>
        <nav className="sidebar__nav">
          {items.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} end={to === "/"} className="sidebar__link">
              <Icon aria-hidden />
              <span>{t(label === "Alerts" ? "Notifications" : label)}</span>
              {badge(to)}
            </NavLink>
          ))}
        </nav>
        {(staffGyms.length > 0 || isAdmin) && (
          <nav className="sidebar__nav" aria-label={t("Manage")}>
            <p className="sidebar__heading">{t("Manage")}</p>
            {staffGyms.map((g) => (
              <NavLink key={g.gymId} to={`/manage/${g.slug}`} className="sidebar__link">
                <GymAvatar name={g.name} logoUrl={g.logoUrl} size={24} />
                <span className="sidebar__text">{g.name}</span>
              </NavLink>
            ))}
            {isAdmin && (
              <NavLink to="/admin" className="sidebar__link">
                <ShieldCheck aria-hidden />
                <span>{t("Admin")}</span>
              </NavLink>
            )}
          </nav>
        )}
        {!session && (
          <Link to="/sign-in" className="btn btn--primary btn--block sidebar__cta">
            <LogIn aria-hidden /><span>{t("Sign in")}</span>
          </Link>
        )}
      </aside>

      <header className="topbar">
        <Link to="/" aria-label={t("BoulderTime home")}><Logo variant="horizontal" height={30} /></Link>
        {!session && <Link to="/sign-in" className="topbar__signin">{t("Sign in")}</Link>}
      </header>

      <RouteAnnouncer />
      <OfflineBanner />
      <main id="main" className="main" tabIndex={-1}>
        <Suspense fallback={<LoadingState />}>
          <Outlet />
        </Suspense>
      </main>

      <nav className="tabbar" aria-label={t("Primary")}>
        {items.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === "/"} className="tabbar__link">
            <span className="tabbar__icon"><Icon aria-hidden />{badge(to)}</span>
            <span>{t(label)}</span>
          </NavLink>
        ))}
        {!session && (
          <NavLink to="/sign-in" className="tabbar__link">
            <LogIn aria-hidden /><span>{t("Sign in")}</span>
          </NavLink>
        )}
      </nav>
    </div>
  );
}
