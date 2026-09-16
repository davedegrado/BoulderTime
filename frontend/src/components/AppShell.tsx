import { NavLink, Outlet, Link } from "react-router-dom";
import { Home, Compass, Activity, Bell, UserRound, LogIn } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Logo } from "@/components/Logo";
import { useAuth } from "@/auth/AuthProvider";

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
  const items = NAV.filter((i) => session || !i.requiresAuth);

  return (
    <div className="shell">
      <a className="skip-link" href="#main">Skip to content</a>

      <aside className="sidebar" aria-label="Primary">
        <Link to="/" className="sidebar__brand" aria-label="BoulderTime home">
          <Logo variant="horizontal" onDark height={40} />
        </Link>
        <nav className="sidebar__nav">
          {items.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} end={to === "/"} className="sidebar__link">
              <Icon aria-hidden />
              <span>{label === "Alerts" ? "Notifications" : label}</span>
            </NavLink>
          ))}
        </nav>
        {!session && (
          <Link to="/sign-in" className="btn btn--primary btn--block sidebar__cta">
            <LogIn aria-hidden /><span>Sign in</span>
          </Link>
        )}
      </aside>

      <header className="topbar">
        <Link to="/" aria-label="BoulderTime home"><Logo variant="horizontal" height={30} /></Link>
        {!session && <Link to="/sign-in" className="topbar__signin">Sign in</Link>}
      </header>

      <main id="main" className="main" tabIndex={-1}>
        <Outlet />
      </main>

      <nav className="tabbar" aria-label="Primary">
        {items.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === "/"} className="tabbar__link">
            <Icon aria-hidden />
            <span>{label}</span>
          </NavLink>
        ))}
        {!session && (
          <NavLink to="/sign-in" className="tabbar__link">
            <LogIn aria-hidden /><span>Sign in</span>
          </NavLink>
        )}
      </nav>
    </div>
  );
}
