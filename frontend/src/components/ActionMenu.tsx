import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { MoreHorizontal } from "lucide-react";
import { t } from "@/i18n/i18n";

export type ActionMenuItem =
  | { kind?: "button"; label: string; icon?: ReactNode; onSelect: () => void; danger?: boolean }
  | { kind: "link"; label: string; icon?: ReactNode; to: string };

/**
 * The "…" menu of a page: the actions that don't deserve a button of their own (share, copy the link, block,
 * report, manage). Closes on choosing, on Escape and on a tap outside.
 */
export function ActionMenu({ items, label = t("More actions"), className = "" }: { items: ActionMenuItem[]; label?: string; className?: string }) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const id = useId();

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => { if (!root.current?.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("pointerdown", onDown); document.removeEventListener("keydown", onKey); };
  }, [open]);

  if (items.length === 0) return null;
  return (
    <div className={`action-menu ${className}`} ref={root}>
      <button type="button" className="round-btn" aria-label={label} title={label} aria-haspopup="menu" aria-expanded={open} aria-controls={id}
        onClick={() => setOpen((v) => !v)}>
        <MoreHorizontal aria-hidden />
      </button>
      {open && (
        <div className="action-menu__list" role="menu" id={id}>
          {items.map((item) => item.kind === "link"
            ? <Link key={item.label} role="menuitem" className="action-menu__item" to={item.to} onClick={() => setOpen(false)}>{item.icon}<span>{item.label}</span></Link>
            : (
              <button key={item.label} type="button" role="menuitem" className={`action-menu__item ${item.danger ? "is-danger" : ""}`}
                onClick={() => { setOpen(false); item.onSelect(); }}>
                {item.icon}<span>{item.label}</span>
              </button>
            ))}
        </div>
      )}
    </div>
  );
}
