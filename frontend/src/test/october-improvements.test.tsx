import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderAt } from "@/test/renderApp";
import { I18nProvider } from "@/i18n/i18n";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string) => {
    calls.push({ method, path });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler();
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { linkParts, LinkifiedText } from "@/components/LinkifiedText";
import { openStatus, type OpeningHours } from "@/features/gyms/openingHours";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import type { Announcement } from "@/features/notifications/api";
import { scoringText } from "@/features/leaderboards/LeaderboardTab";
import type { Leaderboard } from "@/features/leaderboards/api";
import { GradeBadge } from "@/features/boulders/BoulderBits";
import { EnlargeableAvatar } from "@/components/EnlargeableAvatar";
import { dataLabel } from "@/i18n/data";
import { automaticPoints } from "@/features/grading/api";

beforeEach(() => { routes.clear(); calls.length = 0; });
// The language in use is the app's own, shared by every render: tests that switch to Italian switch back.
afterEach(() => { render(<I18nProvider initial="en"><span /></I18nProvider>); });

describe("Links in announcements", () => {
  it("turns only http and https addresses into links, without the full stop after them", () => {
    expect(linkParts("Iscriviti su https://esempio.it/gara. Info: javascript:alert(1) o www.esempio.it")).toEqual([
      { text: "Iscriviti su " },
      { text: "https://esempio.it/gara", href: "https://esempio.it/gara" },
      { text: ". Info: javascript:alert(1) o www.esempio.it" },
    ]);
  });

  it("opens them outside the app, with no way back to the page", () => {
    render(<LinkifiedText text="Vedi http://esempio.it" />);
    const link = screen.getByRole("link", { name: "http://esempio.it" });
    expect(link).toHaveAttribute("target", "_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
  });
});

describe("Opening hours", () => {
  const hours: OpeningHours = {
    week: [{ day: 1, intervals: [{ opens: "12:00", closes: "14:00" }, { opens: "18:00", closes: "23:00" }] }, { day: 2, intervals: [{ opens: "10:00", closes: "24:00" }] }],
    closures: [{ from: "2026-10-20", to: "2026-10-20", note: "Lavori" }],
  };
  // 12 October 2026 is a Monday; Rome is two hours ahead of UTC in October.
  const at = (iso: string) => openStatus(hours, new Date(iso));

  it("says whether the gym is open on Italian time", () => {
    expect(at("2026-10-12T11:00:00Z")).toEqual({ open: true, text: "Open now · closes at 14:00" });
    expect(at("2026-10-12T13:00:00Z")).toEqual({ open: false, text: "Closed · opens at 18:00" });
    expect(at("2026-10-12T21:30:00Z")).toEqual({ open: false, text: "Closed · opens tomorrow at 10:00" });
    expect(at("2026-10-12T22:30:00Z")).toEqual({ open: false, text: "Closed · opens at 10:00" }); // past midnight in Rome
    expect(at("2026-10-13T21:30:00Z")?.text).toBe("Open now · closes at 00:00");
  });

  it("knows a closure from a usual day", () => {
    // Tuesday 20 October is closed for works; the next open day is Monday 26.
    expect(at("2026-10-20T09:00:00Z")?.text).toBe("Closed today: Lavori · opens Monday at 12:00");
  });
});

const event = (over: Partial<Announcement> = {}): Announcement => ({
  id: "a1", gymId: "g1", gymName: "Crimp", gymSlug: "crimp", type: "EVENT", title: "Serata", content: "Vi aspettiamo",
  imageUrl: null, imagePath: null, eventDate: new Date(Date.now() + 3 * 86_400_000).toISOString(), sectorId: null, sectorName: null,
  notifiedFollowers: false, author: { userId: "s", displayName: "Staff", avatarUrl: null }, createdAt: "2026-10-01T10:00:00Z", updatedAt: "2026-10-01T10:00:00Z",
  takesAttendance: true, attendees: 2, viewerAttending: false, canSeeAttendees: true, ...over,
});

describe("I'll be there", () => {
  it("says it with one tap and shows who is coming to the gym's followers", async () => {
    reply("PUT", "/api/announcements/a1/attendance", { attendees: 3, viewerAttending: true });
    reply("GET", "/api/announcements/a1/attendees", [{ userId: "u1", displayName: "Giulia", avatarUrl: null }]);
    renderAt("/", "/", <AnnouncementCard a={event()} />);
    expect(screen.getByText("2 people going")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "I'll be there" }));
    expect(calls).toContainEqual({ method: "PUT", path: "/api/announcements/a1/attendance" });
    await userEvent.click(screen.getByRole("button", { name: /See who is going/ }));
    expect(await screen.findByRole("link", { name: /Giulia/ })).toHaveAttribute("href", "/users/u1");
  });

  it("is not offered for competitions or news, and names stay hidden from people who don't follow the gym", () => {
    renderAt("/", "/", <AnnouncementCard a={event({ type: "COMPETITION", takesAttendance: false })} />);
    expect(screen.queryByRole("button", { name: "I'll be there" })).not.toBeInTheDocument();
    renderAt("/", "/", <AnnouncementCard a={event({ canSeeAttendees: false })} />);
    expect(screen.queryByRole("button", { name: /See who is going/ })).not.toBeInTheDocument();
  });

  it("shows the image whole and opens it big", async () => {
    renderAt("/", "/", <AnnouncementCard a={event({ imageUrl: "/poster.jpg" })} />);
    await userEvent.click(screen.getByRole("button", { name: "Enlarge the image" }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});

describe("Points and grades in the reader's language", () => {
  const board = { gradeSystemName: "Colour", scoring: { minPoints: 10, maxPoints: 100, customised: true } } as Leaderboard;

  it("explains points in Italian from the numbers, never from the server's English sentence", () => {
    let text = "";
    function Probe() { text = scoringText(board); return null; }
    render(<I18nProvider initial="it"><Probe /></I18nProvider>);
    expect(text).toContain("il grado più facile vale 10 punti e il più difficile 100");
    expect(text).toContain("Colori");
    expect(text).toContain("impostato a mano");
  });

  it("names two-colour preset grades in Italian and draws both colours", () => {
    let label = "";
    function Probe() { label = dataLabel("Green/Blue"); return null; }
    render(<I18nProvider initial="it"><Probe /></I18nProvider>);
    expect(label).toBe("Verde/Blu");

    render(<I18nProvider initial="en"><span /></I18nProvider>);
    render(<GradeBadge grade={{ gradeSystemId: "c", systemName: "Colour", systemType: "COLOR", gradeValueId: "v", label: "Green/Blue", rank: 3, colorHex: "#2E9E4F", colorHex2: "#2F6FDB" }} />);
    const badge = screen.getByText("Green/Blue");
    expect(badge).toHaveClass("grade--duo");
    expect(badge.getAttribute("style")).toContain("linear-gradient");
  });

  it("previews the automatic points the way the server counts them", () => {
    expect([0, 1, 2, 3, 4, 5].map((i) => automaticPoints(i, 6))).toEqual([10, 28, 46, 64, 82, 100]);
    expect([0, 1, 2, 3, 4].map((i) => automaticPoints(i, 5))).toEqual([10, 33, 55, 78, 100]);
  });
});

describe("Profile photo", () => {
  it("opens big when there is one, and is just initials when there isn't", async () => {
    const { unmount } = render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><EnlargeableAvatar name="Giulia" url="/g.jpg" /></MemoryRouter>);
    await userEvent.click(screen.getByRole("button", { name: "Enlarge Giulia's photo" }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    unmount();
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><EnlargeableAvatar name="Giulia" url={null} /></MemoryRouter>);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});

import { SectorMap } from "@/features/gyms/SectorMap";

describe("Where it is, on the boulder's page", () => {
  it("picks out the sector on the plan without writing names on it", () => {
    const sector = { id: "s1", gymId: "g1", name: "Strapiombo", description: null, imageUrl: null, sortOrder: 0, isActive: true, isFollowing: false,
      zone: { points: [{ x: 0.1, y: 0.1 }, { x: 0.5, y: 0.1 }, { x: 0.3, y: 0.5 }], label: { x: 0.3, y: 0.3 }, hideLabel: false }, activeBoulders: 3, newThisWeek: 0 };
    const plan = { url: "/plan.png", width: 1000, height: 600 };
    const { container, rerender } = render(<SectorMap plan={plan} sectors={[sector]} highlightId="s1" labels={false} />);
    expect(container.querySelector(".sector-map__zone.is-highlighted")).not.toBeNull();
    expect(screen.queryByText("Strapiombo")).not.toBeInTheDocument();
    rerender(<SectorMap plan={plan} sectors={[sector]} highlightId="s1" />);
    expect(screen.getByText("Strapiombo")).toBeInTheDocument();
  });
});

import { pushWording } from "@/features/notifications/pushWording";

describe("Notification messages on the website", () => {
  it("talk about the browser, not a phone", () => {
    const words = pushWording();
    expect(words.on).toBe("Browser notifications on");
    expect(words.denied).toContain("site's settings");
    expect(Object.values(words).join(" ")).not.toMatch(/phone/i);
  });
});

import { LeaderboardTab } from "@/features/leaderboards/LeaderboardTab";

describe("Leaderboards are opt-in", () => {
  it("tells a climber who isn't taking part, with the switch right there", async () => {
    reply("GET", "/api/users/me", { id: "u1", displayName: "Me", staffGyms: [], isPlatformAdmin: false, leaderboardOptOut: true, leaderboardExcluded: false });
    reply("GET", "/api/gyms/g1/leaderboard", { gymId: "g1", metric: "POINTS", period: "MONTH", from: null, gradeSystemId: null, gradeSystemName: null,
      gradeSystemType: null, scoringExplanation: "", climbers: 0, entries: [], viewer: null });
    reply("PUT", "/api/users/me/leaderboard-visibility", undefined);
    renderAt("/g", "/g", <LeaderboardTab gymId="g1" />);
    await userEvent.click(await screen.findByRole("button", { name: "Show me in leaderboards" }));
    expect(calls).toContainEqual({ method: "PUT", path: "/api/users/me/leaderboard-visibility" });
  });
});
