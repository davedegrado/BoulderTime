import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

const calls: string[] = [];
let respond: (path: string) => unknown = () => ({});
vi.mock("@/lib/api", () => ({ api: { get: async (path: string) => { calls.push(path); return respond(path); } } }));

import { LeaderboardTab } from "@/features/leaderboards/LeaderboardTab";

const person = (id: string, name: string) => ({ userId: id, displayName: name, avatarUrl: null });
const entry = (pos: number, id: string, name: string, over: object = {}) => ({ position: pos, climber: person(id, name), points: 100 - pos, completed: 5, highest: { label: "7A", rank: 11, colorHex: null }, isViewer: false, ...over });
const board = (over: object = {}) => ({
  gymId: "g1", metric: "POINTS", period: "MONTH", from: "2026-09-01T00:00:00Z", gradeSystemId: "font", gradeSystemName: "Fontainebleau", gradeSystemType: "FONTAINEBLEAU",
  scoringExplanation: "Each completed boulder scores by its Fontainebleau grade: the easiest grade is worth 10 points and the hardest 100, evenly spaced in between.",
  climbers: 60, entries: [entry(1, "a", "Anna"), entry(2, "b", "Ben"), entry(2, "c", "Cleo")], viewer: entry(57, "me", "Me", { isViewer: true, points: 12 }), ...over,
});

beforeEach(() => { calls.length = 0; respond = () => board(); });

describe("Leaderboard", () => {
  it("shows shared positions, pins your own position when you're outside the top, and explains scoring", async () => {
    renderAt("/g", "/g", <LeaderboardTab gymId="g1" />);

    const list = await screen.findByRole("list", { name: "Points leaderboard" });
    expect(within(list).getAllByLabelText("Position 2")).toHaveLength(2);
    const mine = screen.getByRole("list", { name: "Your position" });
    expect(within(mine).getByLabelText("Position 57")).toBeInTheDocument();
    expect(within(mine).getByText("· you")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /how it works/i }));
    expect(screen.getByText(/easiest grade is worth 10 points/)).toBeInTheDocument();
    expect(calls[0]).toBe("/api/gyms/g1/leaderboard?metric=POINTS&period=MONTH");
  });

  it("switches metric and period", async () => {
    respond = (path) => (path.includes("HIGHEST") ? board({ metric: "HIGHEST", period: "ALL", viewer: null }) : board());
    renderAt("/g", "/g", <LeaderboardTab gymId="g1" />);
    await screen.findByRole("list", { name: "Points leaderboard" });

    await userEvent.click(screen.getByRole("radio", { name: "Highest grade" }));
    await userEvent.click(screen.getByRole("radio", { name: "All time" }));
    await waitFor(() => expect(calls).toContain("/api/gyms/g1/leaderboard?metric=HIGHEST&period=ALL"));
    expect(await screen.findByRole("list", { name: "Highest grade leaderboard" })).toBeInTheDocument();
    expect(screen.getAllByText("7A").length).toBeGreaterThan(0);
  });

  it("invites climbers to log sends when the board is empty", async () => {
    respond = () => board({ entries: [], viewer: null, climbers: 0 });
    renderAt("/g", "/g", <LeaderboardTab gymId="g1" />);
    expect(await screen.findByText("No sends yet for this period")).toBeInTheDocument();
  });
});
