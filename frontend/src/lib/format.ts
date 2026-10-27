import { localeTag, t } from "@/i18n/i18n";

export type GymStatus = "DRAFT" | "ACTIVE" | "ARCHIVED";
export type GymRole = "STAFF" | "ADMIN" | "OWNER";
export type CandidateStatus = "PENDING" | "CONTACTED" | "ACCEPTED" | "REJECTED";

const GYM_STATUS: Record<GymStatus, string> = { DRAFT: "Draft", ACTIVE: "Active", ARCHIVED: "Archived" };
// English here, translated when read (labels): a t() at load time would stay in the language the app started in.
const ROLES: Record<GymRole, string> = { STAFF: "Staff", ADMIN: "Admin", OWNER: "Owner" };

/** Label maps are read at render time, so they follow the language in use. */
export function labels<T extends string>(source: Record<T, string>): Record<T, string> {
  return new Proxy({} as Record<T, string>, {
    get: (_, key: string) => t(source[key as T] ?? key),
    ownKeys: () => Object.keys(source),
    getOwnPropertyDescriptor: () => ({ enumerable: true, configurable: true }),
  });
}

export const gymStatusLabel = labels(GYM_STATUS);
export const roleLabel = labels(ROLES);
export const candidateStatusLabel = labels<CandidateStatus>({
  PENDING: "Pending",
  CONTACTED: "Contacted",
  ACCEPTED: "Accepted",
  REJECTED: "Rejected",
});

export function formatDate(iso: string, opts: Intl.DateTimeFormatOptions = { day: "numeric", month: "short", year: "numeric" }) {
  return new Date(iso).toLocaleDateString(localeTag(), opts);
}

export function formatDateTime(iso: string, opts: Intl.DateTimeFormatOptions = { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) {
  return new Date(iso).toLocaleString(localeTag(), opts);
}

/** "in 3 days", "tomorrow" — for invitation expiry. */
export function relativeDays(iso: string, now = Date.now()): string {
  const days = Math.round((new Date(iso).getTime() - now) / 86_400_000);
  return new Intl.RelativeTimeFormat(localeTag(), { numeric: "auto" }).format(days, "day");
}

/** "today", "yesterday", "3 days ago", "2 wk. ago", "4 mo. ago": how long ago, by calendar day, short to fit a line. */
export function timeAgo(iso: string, now = new Date()): string {
  const day = (d: Date) => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate());
  const days = Math.max(0, Math.round((day(now) - day(new Date(iso))) / 86_400_000));
  const rtf = new Intl.RelativeTimeFormat(localeTag(), { numeric: "auto", style: "short" });
  if (days < 7) return rtf.format(-days, "day");
  if (days < 60) return rtf.format(-Math.floor(days / 7), "week");
  return rtf.format(-Math.floor(days / 30), "month");
}

export function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase()).join("") || "?";
}
