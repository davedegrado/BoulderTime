import { safeNext } from "@/auth/RequireAuth";

describe("safeNext", () => {
  it("allows same-origin relative paths", () => expect(safeNext("/profile?tab=1")).toBe("/profile?tab=1"));
  it("blocks absolute and protocol-relative URLs (open redirect)", () => {
    expect(safeNext("https://evil.example")).toBe("/");
    expect(safeNext("//evil.example")).toBe("/");
  });
  it("falls back when missing", () => expect(safeNext(null, "/home")).toBe("/home"));
});
