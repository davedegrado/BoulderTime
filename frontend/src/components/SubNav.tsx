import { NavLink } from "react-router-dom";

export interface SubNavItem { to: string; label: string; end?: boolean }

/** Horizontally scrollable section tabs; the active tab uses the brand's slanted orange marker. */
export function SubNav({ items, label }: { items: SubNavItem[]; label: string }) {
  return (
    <nav className="subnav" aria-label={label}>
      {items.map((i) => (
        <NavLink key={i.to} to={i.to} end={i.end} className="subnav__link">{i.label}</NavLink>
      ))}
    </nav>
  );
}
