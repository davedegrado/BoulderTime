import type { GymRole } from "@/lib/format";

/**
 * Mirrors the server's permission rules (GymRoleRules / ADR-007) to decide which controls to show.
 * The API remains the authority; these only avoid offering actions that would be refused.
 */
const rank: Record<GymRole, number> = { STAFF: 1, ADMIN: 2, OWNER: 3 };

export const atLeast = (role: GymRole | null | undefined, minimum: GymRole) => !!role && rank[role] >= rank[minimum];

export const canGrant = (actor: GymRole | null | undefined, target: GymRole) =>
  actor === "OWNER" || (actor === "ADMIN" && target !== "OWNER");

export const canManage = (actor: GymRole | null | undefined, member: GymRole) =>
  actor === "OWNER" || (actor === "ADMIN" && member !== "OWNER");

export const grantableRoles = (actor: GymRole | null | undefined): GymRole[] =>
  (["STAFF", "ADMIN", "OWNER"] as GymRole[]).filter((r) => canGrant(actor, r));
