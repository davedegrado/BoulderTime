import { ERASED_NAME, parseBody } from "@/lib/api";

describe("API bodies", () => {
  it("turns the server's name for an erased account into the reader's language, wherever it sits", () => {
    const body = parseBody(JSON.stringify({ items: [{ author: { displayName: ERASED_NAME } }, { author: { displayName: "Anna" } }], displayName: ERASED_NAME }));
    expect(body).toEqual({ items: [{ author: { displayName: "Deleted user" } }, { author: { displayName: "Anna" } }], displayName: "Deleted user" });
  });

  it("leaves other fields alone", () => {
    expect(parseBody(JSON.stringify({ title: ERASED_NAME }))).toEqual({ title: ERASED_NAME });
  });
});
