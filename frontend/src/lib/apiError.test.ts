import { ApiError, toApiError } from "@/lib/apiError";

describe("toApiError", () => {
  it("maps ASP.NET validation problem details to lower-cased field errors", async () => {
    const res = new Response(JSON.stringify({ title: "One or more validation errors occurred.", status: 400, errors: { DisplayName: ["Too short."] } }), { status: 400 });
    const err = await toApiError(res);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.isValidation).toBe(true);
    expect(err.fieldError("displayName")).toBe("Too short.");
  });

  it("prefers detail over title and falls back to a friendly message for empty bodies", async () => {
    expect((await toApiError(new Response(JSON.stringify({ title: "Forbidden", detail: "Only gym staff can do this." }), { status: 403 }))).message).toBe("Only gym staff can do this.");
    const empty = await toApiError(new Response("", { status: 503 }));
    expect(empty.message).toMatch(/our side/);
  });
});
