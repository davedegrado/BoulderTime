export type GymStatus = "DRAFT" | "ACTIVE" | "ARCHIVED";
export type GymRole = "STAFF" | "ADMIN" | "OWNER";
export type CandidateStatus = "PENDING" | "CONTACTED" | "ACCEPTED" | "REJECTED";

export const gymStatusLabel: Record<GymStatus, string> = { DRAFT: "Draft", ACTIVE: "Active", ARCHIVED: "Archived" };
export const roleLabel: Record<GymRole, string> = { STAFF: "Staff", ADMIN: "Admin", OWNER: "Owner" };
export const candidateStatusLabel: Record<CandidateStatus, string> = {
  PENDING: "Pending",
  CONTACTED: "Contacted",
  ACCEPTED: "Accepted",
  REJECTED: "Rejected",
};

export function formatDate(iso: string, opts: Intl.DateTimeFormatOptions = { day: "numeric", month: "short", year: "numeric" }) {
  return new Date(iso).toLocaleDateString(undefined, opts);
}

/** "in 3 days", "tomorrow" — for invitation expiry. */
export function relativeDays(iso: string, now = Date.now()): string {
  const days = Math.round((new Date(iso).getTime() - now) / 86_400_000);
  return new Intl.RelativeTimeFormat(undefined, { numeric: "auto" }).format(days, "day");
}

export function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase()).join("") || "?";
}
