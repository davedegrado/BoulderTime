import { validateSignUp } from "@/pages/SignUpPage";

describe("validateSignUp", () => {
  it("accepts a valid registration", () => expect(validateSignUp("Marco", "marco@example.com", "longenough")).toEqual({}));
  it("reports each invalid field", () => {
    const e = validateSignUp("M", "not-an-email", "short");
    expect(Object.keys(e).sort()).toEqual(["displayName", "email", "password"]);
  });
});
