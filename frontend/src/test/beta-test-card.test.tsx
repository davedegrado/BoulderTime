import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

const calls: { method: string; path: string; body?: unknown }[] = [];
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, body?: unknown) => {
    calls.push({ method, path, body });
    return path.endsWith("/undo") ? { betaTestBoulders: 0, changed: 12, betaTesters: 0 } : { betaTestBoulders: 12, changed: 12, betaTesters: 5 };
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { BetaTestCard } from "@/features/admin/BetaTestCard";

describe("Closing a gym's trial period from the admin panel", () => {
  it("waits for the gym's slug, then marks the boulders off the wall, and undoes it", async () => {
    calls.length = 0;
    renderAt("/", "/", <BetaTestCard gymId="g1" gymSlug="rock-n-fire" />);
    const mark = screen.getByRole("button", { name: "Mark the boulders off the wall as trial ones" });
    const undo = screen.getByRole("button", { name: "Undo: take every trial mark off" });
    expect(mark).toBeDisabled();
    expect(undo).toBeDisabled();
    await userEvent.type(screen.getByRole("textbox", { name: /Type the gym's slug/ }), "rock-n-fir");
    expect(mark).toBeDisabled();
    await userEvent.type(screen.getByRole("textbox", { name: /Type the gym's slug/ }), "e");
    await userEvent.click(mark);
    await waitFor(() => expect(calls.at(-1)).toEqual({ method: "POST", path: "/api/admin/gyms/g1/beta-test", body: { gymSlug: "rock-n-fire" } }));
    expect(await screen.findByText(/12 trial boulders in this gym now/)).toHaveTextContent("12 trial boulders in this gym now · 5 beta testers");
    await userEvent.click(undo);
    await waitFor(() => expect(calls.at(-1)!.path).toBe("/api/admin/gyms/g1/beta-test/undo"));
  });
});
