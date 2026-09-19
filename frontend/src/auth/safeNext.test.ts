import { safeNext } from "@/auth/RequireAuth";

describe("safeNext", () => {
  it("allows same-origin relative paths", () => expect(safeNext("/profile?tab=1")).toBe("/profile?tab=1"));
  it("blocks absolute and protocol-relative URLs (open redirect)", () => {
    expect(safeNext("https://evil.example")).toBe("/");
    expect(safeNext("//evil.example")).toBe("/");
  });
  it("blocks backslash paths, which browsers treat as protocol-relative", () => {
    expect(safeNext("/\\evil.example")).toBe("/");
    expect(safeNext("\\\\evil.example")).toBe("/");
    expect(safeNext("/path\\..\\x")).toBe("/");
  });
  it("blocks control characters that could smuggle a scheme", () => {
    expect(safeNext("/\u0009/evil.example")).toBe("/");
    expect(safeNext(" /profile")).toBe("/profile"); // surrounding spaces are harmless
  });
  it("falls back when missing", () => expect(safeNext(null, "/home")).toBe("/home"));
});
