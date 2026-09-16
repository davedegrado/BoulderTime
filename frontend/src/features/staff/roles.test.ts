import { atLeast, canGrant, canManage, grantableRoles } from "@/features/staff/roles";

describe("gym role rules (mirror of server GymRoleRules)", () => {
  it("orders roles by authority", () => {
    expect(atLeast("OWNER", "ADMIN")).toBe(true);
    expect(atLeast("STAFF", "ADMIN")).toBe(false);
    expect(atLeast(null, "STAFF")).toBe(false);
  });

  it("admins grant staff and admin but never owner", () => {
    expect(grantableRoles("ADMIN")).toEqual(["STAFF", "ADMIN"]);
    expect(grantableRoles("OWNER")).toEqual(["STAFF", "ADMIN", "OWNER"]);
    expect(grantableRoles("STAFF")).toEqual([]);
    expect(canGrant("ADMIN", "OWNER")).toBe(false);
  });

  it("admins can't manage owners", () => {
    expect(canManage("ADMIN", "OWNER")).toBe(false);
    expect(canManage("ADMIN", "STAFF")).toBe(true);
    expect(canManage("STAFF", "STAFF")).toBe(false);
  });
});
