import { useState } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) =>
  routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    const body = method === "GET" || method === "DELETE" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(body);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { BetaSection } from "@/features/community/VideoSections";
import { BetaEditor } from "@/features/community/BetaEditor";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";
import { ProgressTracker } from "@/features/climbing/ProgressTracker";
import { BoulderRow } from "@/features/boulders/listView";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { DeleteBoulderButton } from "@/features/boulders/DeleteBoulderButton";
import { HistoryRow } from "@/features/climbing/ClimbingBits";

const person = { userId: "u2", displayName: "Anna", avatarUrl: null };
const beta = (over: object = {}) => ({
  id: "beta1", boulderId: "b1", videoUrl: "https://files/beta.mp4", thumbnailUrl: null,
  caption: null, uploadedBy: person, updatedAt: "2026-10-01T10:00:00Z", externalUrl: null, ...over,
});

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("The official beta", () => {
  it("is only watched on the climber's page — nothing there publishes it", async () => {
    reply("GET", "/api/boulders/b1/beta", beta());
    renderAt("/b", "/b", <BetaSection boulderId="b1" />);

    expect(await screen.findByRole("heading", { name: "Beta" })).toBeInTheDocument();
    // Staff who open a boulder as a climber are reading, not working: the controls live in the editor.
    for (const name of [/publish/i, /replace/i, /delete/i, /link a video/i, /choose a video/i]) {
      expect(screen.queryByRole("button", { name })).not.toBeInTheDocument();
    }
  });

  it("shows a linked beta as a link out, not as a player we cannot fill", async () => {
    reply("GET", "/api/boulders/b1/beta", beta({ videoUrl: "", externalUrl: "https://www.instagram.com/reel/ABC" }));
    renderAt("/b", "/b", <BetaSection boulderId="b1" gymName="Rock'n Fire" />);

    const link = await screen.findByRole("link", { name: "Watch on Instagram" });
    expect(link).toHaveAttribute("href", "https://www.instagram.com/reel/ABC");
    expect(link).toHaveAttribute("target", "_blank");
    expect(document.querySelector("video")).toBeNull();
    // It is the gym's beta: the gym is named, and the staff member who pasted the link is not (the server leaves them out).
    expect(screen.getByText("Official beta from Rock'n Fire")).toBeInTheDocument();
  });

  it("is published from the editor, and a gym out of uploads is offered the link instead", async () => {
    reply("GET", "/api/settings/videos", { uploadsEnabled: true, maxUploadMegabytes: 50 });
    reply("GET", "/api/boulders/b1/beta", null);
    reply("PUT", "/api/boulders/b1/beta", (body?: unknown) => beta({ externalUrl: (body as { externalUrl: string }).externalUrl }));
    renderAt("/b", "/b", <BetaEditor boulderId="b1" canAdd={false} />);

    // Told before filming rather than at the last step, and told what it can still do.
    expect(await screen.findByText(/used all its official beta videos/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /choose a video/i })).not.toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Address of the video"), "youtu.be/abc123");
    await userEvent.click(screen.getByRole("button", { name: /link this video/i }));

    // The refetch that follows is the last call, so look for the write itself rather than the most recent one.
    await waitFor(() => expect(calls.find((c) => c.method === "PUT" && c.path === "/api/boulders/b1/beta")).toBeDefined());
    expect(calls.find((c) => c.method === "PUT")!.body).toMatchObject({ externalUrl: "youtu.be/abc123" });
  });
});

const summary = {
  id: "b1", gymId: "g1", gymName: "Rock", sectorId: "s1", sectorName: "Strapiombo",
  photoUrl: "https://files/p.jpg", holdColor: "BLUE",
  grades: [{ gradeSystemId: "s1", systemName: "French", gradeValueId: "g1", label: "6A", rank: 10, colorHex: null }],
  status: "ACTIVE", createdAt: "2026-10-01T10:00:00Z", removedAt: null,
  rating: { average: null, count: 0 }, viewer: null,
};

describe("Suggesting a grade", () => {
  const scale = [{ gradeValueId: "g1", label: "6A", rank: 10, colorHex: null, votes: 0 }];
  const consensus = (viewerCanSuggest: boolean) => ({
    viewerCanSuggest,
    systems: [{
      gradeSystemId: "s1", systemName: "French", systemType: "FRENCH", totalVotes: 0,
      consensusValueId: null, officialValueId: null, viewerValueId: null, buckets: [], scale,
    }],
  });

  it("opens to a climber as soon as they log an attempt, without reloading the page", async () => {
    let tried = false;
    reply("GET", "/api/boulders/b1/grade-consensus", () => consensus(tried));
    reply("PUT", "/api/boulders/b1/attempt", () => { tried = true; return { attempts: 1, completed: true, completedAt: "2026-10-07T10:00:00Z", rating: null }; });
    const boulder = { ...summary, gymSlug: "rock", photoPath: "p", setter: null, viewerRole: null, isFollowing: false, communityVideosEnabled: false, canAddOfficialBeta: true };
    renderAt("/b", "/b", <><ProgressTracker boulder={boulder as never} /><CommunityGradeSection boulderId="b1" /></>);

    expect(await screen.findByText("Log an attempt to suggest a grade.")).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: /your French grade/i })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Mark as completed" }));

    // The answer to "may I suggest?" lives in a different query, which has to be refetched or the vote stays hidden.
    expect(await screen.findByRole("combobox", { name: /your French grade/i })).toBeInTheDocument();
  });
});

describe("Removing boulders", () => {
  const grid = (props: object) => (
    <Routes>
      <Route path="/m" element={<BoulderCard boulder={summary as never} to="/edit/b1" {...props} />} />
      <Route path="/edit/b1" element={<p>Boulder editor</p>} />
    </Routes>
  );

  it("starts selection when a card is held down, and the same press does not also open the boulder", async () => {
    const started = vi.fn();
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}>{grid({ onLongPress: started })}</MemoryRouter>);

    const card = screen.getByRole("link");
    await userEvent.pointer([{ keys: "[MouseLeft>]", target: card }]);   // press and hold
    await waitFor(() => expect(started).toHaveBeenCalledTimes(1), { timeout: 2000 });
    await userEvent.pointer([{ keys: "[/MouseLeft]", target: card }]);

    // The hold already selected it; letting the tap through would open the editor at the same time.
    expect(screen.queryByText("Boulder editor")).not.toBeInTheDocument();
  });

  it("never turns the hold into a text selection", async () => {
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}>{grid({ onLongPress: vi.fn() })}</MemoryRouter>);
    const card = screen.getByRole("link");
    // The card can't be selected as text (nor show iOS's callout): its class carries that.
    expect(card).toHaveClass("boulder-card--pressable");
    // And whatever the press had begun to select is let go once the hold selects the boulder.
    const range = document.createRange();
    range.selectNodeContents(card);
    window.getSelection()!.addRange(range);
    await userEvent.pointer([{ keys: "[MouseLeft>]", target: card }]);
    await waitFor(() => expect(window.getSelection()!.rangeCount).toBe(0), { timeout: 2000 });
    await userEvent.pointer([{ keys: "[/MouseLeft]", target: card }]);
  });

  it("keeps the boulder the hold selected when the finger lifts, and toggles on the taps after", async () => {
    function Selecting() {
      const [selecting, setSelecting] = useState(false);
      const [selected, setSelected] = useState(false);
      return selecting
        ? <BoulderCard boulder={summary as never} selectable selected={selected} onToggle={() => setSelected((v) => !v)} />
        : <BoulderCard boulder={summary as never} to="/edit/b1" onLongPress={() => { setSelecting(true); setSelected(true); }} />;
    }
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}><Selecting /></MemoryRouter>);
    await userEvent.pointer([{ keys: "[MouseLeft>]", target: screen.getByRole("link") }]);
    const card = await screen.findByRole("button", {}, { timeout: 2000 });
    // The same finger lifts over the card that now stands in its place, and the browser sends that card the release and
    // a click (as Chromium does on a phone): that is not a tap on it.
    fireEvent.pointerUp(card);
    fireEvent.click(card);
    expect(card).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(card);
    expect(card).toHaveAttribute("aria-pressed", "false");
    await userEvent.click(card);
    expect(card).toHaveAttribute("aria-pressed", "true");
  });

  it("leaves cards that can't be held as they were", () => {
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}>{grid({})}</MemoryRouter>);
    expect(screen.getByRole("link")).not.toHaveClass("boulder-card--pressable");
  });

  it("still opens the boulder on an ordinary tap", async () => {
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}>{grid({ onLongPress: vi.fn() })}</MemoryRouter>);
    await userEvent.click(screen.getByRole("link"));
    expect(await screen.findByText("Boulder editor")).toBeInTheDocument();
  });
});

describe("Selecting from the list view", () => {
  it("starts selecting when a row is held down, keeps it selected when the finger lifts, and selects no text", async () => {
    function Selecting() {
      const [selecting, setSelecting] = useState(false);
      const [selected, setSelected] = useState(false);
      return (
        <Routes>
          <Route path="/m" element={<BoulderRow boulder={summary as never} to="/edit/b1" selectable={selecting} selected={selected}
            onToggle={() => setSelected((v) => !v)} onLongPress={() => { setSelecting(true); setSelected(true); }} />} />
          <Route path="/edit/b1" element={<p>Boulder editor</p>} />
        </Routes>
      );
    }
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}><Selecting /></MemoryRouter>);
    const link = screen.getByRole("link");
    expect(link).toHaveClass("boulder-row--pressable");
    expect(link).toHaveAttribute("draggable", "false");
    expect(link.querySelector("img")).toHaveAttribute("draggable", "false");
    const range = document.createRange();
    range.selectNodeContents(link);
    window.getSelection()!.addRange(range);

    await userEvent.pointer([{ keys: "[MouseLeft>]", target: link }]);
    const row = await screen.findByRole("button", {}, { timeout: 2000 });
    expect(window.getSelection()!.rangeCount).toBe(0);
    fireEvent.pointerUp(row);
    fireEvent.click(row);
    expect(row).toHaveAttribute("aria-pressed", "true");
    expect(screen.queryByText("Boulder editor")).not.toBeInTheDocument();
    await userEvent.click(row);
    expect(row).toHaveAttribute("aria-pressed", "false");
  });

  it("still opens the boulder on an ordinary tap", async () => {
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }} initialEntries={["/m"]}>
      <Routes>
        <Route path="/m" element={<BoulderRow boulder={summary as never} to="/edit/b1" onLongPress={vi.fn()} />} />
        <Route path="/edit/b1" element={<p>Boulder editor</p>} />
      </Routes>
    </MemoryRouter>);
    await userEvent.click(screen.getByRole("link"));
    expect(await screen.findByText("Boulder editor")).toBeInTheDocument();
  });
});

describe("Boulders that are no longer on the wall", () => {
  const deleted = {
    ...summary, status: "DELETED", photoUrl: null, removedAt: "2026-10-02T10:00:00Z",
  };

  it("keeps a deleted boulder in the history, without a photo and without a page to open", () => {
    renderAt("/p", "/p", <HistoryRow item={{
      boulder: deleted as never, attempts: 3, completed: true,
      completedAt: "2026-10-01T10:00:00Z", updatedAt: "2026-10-01T10:00:00Z", rating: null,
    }} />);

    // The send is still theirs: grade, sector and attempts are all there.
    expect(screen.getByText("6A")).toBeInTheDocument();
    expect(screen.getByText(/3 attempts/)).toBeInTheDocument();
    expect(screen.getByText("No longer at the gym")).toBeInTheDocument();
    // What is gone is the photo and the way in — the page would be a 404.
    expect(document.querySelector("img")).toBeNull();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("tells staff what deletion destroys and what it leaves alone, before the second tap", async () => {
    reply("GET", "/api/boulders/b1/deletion-impact", { sends: 7, comments: 2, videos: 0, hasBeta: true });
    reply("DELETE", "/api/boulders/b1", null);
    renderAt("/m", "/m", <DeleteBoulderButton boulderId="b1" />);

    // Nothing is asked of the server until staff reach for the button.
    expect(calls).toHaveLength(0);
    await userEvent.click(screen.getByRole("button", { name: "Delete for good" }));

    expect(await screen.findByText(/Sparisce la foto|Its photo goes/)).toBeInTheDocument();
    expect(screen.getByText(/2 comments/)).toBeInTheDocument();
    expect(screen.getByText(/the official beta/)).toBeInTheDocument();
    // The reassurance matters as much as the warning: a gym tidying its wall is not taking people's points.
    expect(screen.getByText(/7 climbers keep this in their history/)).toBeInTheDocument();

    // The trigger is replaced by the confirmation, so the second tap is inside it, not a second button beside it.
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Delete for good" }));
    await waitFor(() => expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/boulders/b1")).toBe(true));
  });

  it("offers only the link while video uploads are switched off on the server, without blaming the gym's allowance", async () => {
    reply("GET", "/api/settings/videos", { uploadsEnabled: false, maxUploadMegabytes: 50 });
    reply("GET", "/api/boulders/b1/beta", null);
    renderAt("/b", "/b", <BetaEditor boulderId="b1" canAdd />);

    expect(await screen.findByLabelText("Address of the video")).toBeInTheDocument();
    expect(screen.queryByText(/used all its official beta videos/i)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /upload a video instead/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /choose a video/i })).not.toBeInTheDocument();
  });
});
