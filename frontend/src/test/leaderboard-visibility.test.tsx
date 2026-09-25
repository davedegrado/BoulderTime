import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { LeaderboardTab } from "@/features/leaderboards/LeaderboardTab";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "@/components/Toast";

const calls: { method: string; path: string; body?: unknown }[] = [];
const routes = new Map<string, unknown>();
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    const body = method === "GET" || method === "DELETE" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (handler === undefined) throw new Error(`No mock for ${method} ${path}`);
    return handler;
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

const entry = (id: string, name: string, position: number) => ({
  position, climber: { userId: id, displayName: name, avatarUrl: null }, points: 100, completed: 5, highest: null, isViewer: false,
});

function renderBoard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider initial="it">
      <QueryClientProvider client={client}>
        <ToastProvider>
          <MemoryRouter><LeaderboardTab gymId="g1" /></MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

beforeEach(() => {
  calls.length = 0;
  routes.clear();
  reply("GET", "/api/gyms/g1/leaderboard", {
    gymId: "g1", metric: "POINTS", period: "MONTH", from: null, gradeSystemId: null, gradeSystemName: "Fontainebleau",
    gradeSystemType: "FONTAINEBLEAU", scoringExplanation: "", climbers: 2,
    entries: [entry("u1", "Giulia Ferrari", 1), entry("u2", "Marco Bianchi", 2)], viewer: null,
  });
});

describe("Reporting a climber from the leaderboard", () => {
  it("is offered to gym staff only", async () => {
    reply("GET", "/api/users/me", { id: "me", staffGyms: [], isPlatformAdmin: false, leaderboardOptOut: false, leaderboardExcluded: false });
    renderBoard();

    expect(await screen.findByText("Giulia Ferrari")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Segnala/ })).not.toBeInTheDocument();
  });

  it("sends the reason to BoulderTime and tells the staff member it was received", async () => {
    reply("GET", "/api/users/me", { id: "me", staffGyms: [{ gymId: "g1", role: "STAFF" }], isPlatformAdmin: false, leaderboardOptOut: false, leaderboardExcluded: false });
    reply("POST", "/api/gyms/g1/leaderboard-reports", { id: "r1" });
    renderBoard();

    await userEvent.click(await screen.findByRole("button", { name: "Segnala Giulia Ferrari a BoulderTime" }));
    await userEvent.type(screen.getByRole("textbox", { name: "Cosa non torna?" }), "Trenta blocchi in dieci minuti");
    await userEvent.click(screen.getByRole("button", { name: "Invia la segnalazione" }));

    await waitFor(() => expect(calls.find((c) => c.method === "POST")).toEqual({
      method: "POST", path: "/api/gyms/g1/leaderboard-reports",
      body: { userId: "u1", reason: "Trenta blocchi in dieci minuti" },
    }));
    // The board itself doesn't change: reporting is not excluding.
    expect(within(await screen.findByRole("list", { name: /classifica/i })).getByText("Giulia Ferrari")).toBeInTheDocument();
  });
});
