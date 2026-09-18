import type { ReactNode } from "react";
import { Bell, CalendarDays, CheckCircle2, Clapperboard, Flag, Megaphone, MessageSquare, Mountain, Pencil, RefreshCw, Sparkles, Wrench, XCircle } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { Announcement, NotificationType } from "@/features/notifications/api";
import { announcementTypeLabel } from "@/features/notifications/api";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

export const notificationIcon: Record<NotificationType, LucideIcon> = {
  GYM_ANNOUNCEMENT: Megaphone, SECTOR_RETRACED: RefreshCw, BOULDER_UPDATED: Pencil, OFFICIAL_BETA: Clapperboard,
  BOULDER_COMMENTS: MessageSquare, VIDEO_APPROVED: CheckCircle2, VIDEO_REJECTED: XCircle, REPORT_REVIEWED: Flag,
  NEW_BOULDERS_IN_SECTOR: Sparkles, NEW_BOULDERS_AT_GYM: Mountain,
};

export function relativeTime(iso: string, now = Date.now()): string {
  const minutes = Math.round((now - new Date(iso).getTime()) / 60_000);
  if (minutes < 1) return t("just now");
  if (minutes < 60) return t("{minutes} min ago", { minutes });
  const hours = Math.round(minutes / 60);
  if (hours < 24) return t("{hours} h ago", { hours });
  return formatDate(iso, { day: "numeric", month: "short" });
}

/** Accessible on/off switch. */
export function Toggle({ checked, onChange, label, description, disabled }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string; disabled?: boolean }) {
  return (
    <div className="toggle-row">
      <div className="toggle-row__text">
        <span className="list__title">{label}</span>
        {description && <span className="list__sub">{description}</span>}
      </div>
      <button type="button" role="switch" aria-checked={checked} aria-label={label} disabled={disabled}
        className={`switch ${checked ? "is-on" : ""}`} onClick={() => onChange(!checked)}>
        <span className="switch__thumb" />
      </button>
    </div>
  );
}

const TYPE_ICON = { EVENT: CalendarDays, COMPETITION: CalendarDays, MAINTENANCE: Wrench, SCHEDULE_CHANGE: CalendarDays, ANNOUNCEMENT: Megaphone, OTHER: Bell } as const;

export function AnnouncementCard({ a, showGym = false, actions }: { a: Announcement; showGym?: boolean; actions?: ReactNode }) {
  const Icon = TYPE_ICON[a.type];
  return (
    <article className={`card announcement announcement--${a.type.toLowerCase()}`}>
      {a.imageUrl && <img className="announcement__image" src={a.imageUrl} alt="" loading="lazy" />}
      <div className="announcement__head">
        <span className="announcement__type"><Icon aria-hidden /> {announcementTypeLabel[a.type]}</span>
        <span className="list__sub">{showGym ? `${a.gymName} · ` : ""}{relativeTime(a.createdAt)}</span>
      </div>
      <h3 className="announcement__title">{a.title}</h3>
      {a.eventDate && (
        <p className="announcement__when">
          <CalendarDays aria-hidden /> {new Date(a.eventDate).toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
        </p>
      )}
      {a.sectorName && <p className="list__sub">Sector: {a.sectorName}</p>}
      <p className="prose">{a.content}</p>
      {actions && <div className="form__actions">{actions}</div>}
    </article>
  );
}
