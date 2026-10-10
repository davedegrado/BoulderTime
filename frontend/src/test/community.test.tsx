import { Route } from "react-router-dom";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
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

import { CommentsSection } from "@/features/community/CommentsSection";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";
import { CommunityVideosSection } from "@/features/community/VideoSections";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageModeration } from "@/pages/manage/ManageModeration";

const person = (id: string, name: string) => ({ userId: id, displayName: name, avatarUrl: null });
const comment = (id: string, over: object = {}) => ({
  id, boulderId: "b1", author: person("u2", "Anna"), content: "Heel hook at the top", status: "VISIBLE",
  createdAt: "2026-09-10T10:00:00Z", editedAt: null, likes: 2, likedByViewer: false, isMine: false, canModerate: false, ...over,
});
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 30, total: items.length, hasMore: false });

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("Comments", () => {
  it("likes a comment, shows author tools on your own, and requires details when reporting 'Other'", async () => {
    reply("GET", "/api/boulders/b1/comments", page([comment("c1"), comment("c2", { author: person("u1", "Me"), isMine: true, content: "My tip" })]));
    reply("PUT", "/api/comments/c1/like", comment("c1", { likes: 3, likedByViewer: true }));
    renderAt("/b", "/b", <CommentsSection boulderId="b1" />);

    const anna = (await screen.findByText("Heel hook at the top")).closest("li")!;
    await userEvent.click(within(anna).getByRole("button", { name: /like/i }));
    expect(calls.some((c) => c.method === "PUT" && c.path === "/api/comments/c1/like")).toBe(true);
    expect(within(anna).queryByRole("button", { name: /edit/i })).not.toBeInTheDocument();

    const mine = screen.getByText("My tip").closest("li")!;
    expect(within(mine).getByRole("button", { name: /edit/i })).toBeInTheDocument();
    expect(within(mine).queryByRole("button", { name: /report/i })).not.toBeInTheDocument();

    await userEvent.click(within(anna).getByRole("button", { name: /report/i }));
    await userEvent.selectOptions(within(anna).getByRole("combobox", { name: "What's wrong?" }), "OTHER");
    expect(within(anna).getByText("Required for “Other”.")).toBeInTheDocument();
  });

  it("offers Hide to staff and marks hidden comments", async () => {
    reply("GET", "/api/boulders/b1/comments", page([comment("c1", { canModerate: true, status: "HIDDEN" })]));
    renderAt("/b", "/b", <CommentsSection boulderId="b1" />);
    expect(await screen.findByText("Hidden by staff")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /unhide/i })).toBeInTheDocument();
  });
});

describe("Community grade", () => {
  it("still shows the official grade on the scale when nobody voted for it", async () => {
    reply("GET", "/api/boulders/b1/grade-consensus", {
      viewerCanSuggest: true,
      systems: [{
        gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", totalVotes: 2,
        consensusValueId: "v6b", officialValueId: "v6a", viewerValueId: "v6b",
        buckets: [{ gradeValueId: "v6b", label: "6B", rank: 7, colorHex: null, votes: 2 }],
        scale: [{ gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null, votes: 0 }, { gradeValueId: "v6b", label: "6B", rank: 7, colorHex: null, votes: 2 }],
      }],
    });
    renderAt("/b", "/b", <CommunityGradeSection boulderId="b1" />);

    await userEvent.click(await screen.findByText("See the votes"));
    const rows = screen.getAllByRole("listitem");
    expect(rows.map((r) => r.getAttribute("aria-label"))).toEqual(["6A: 0 of 2", "6B: 2 of 2"]);
    // The climber's own vote is in the selector, always in view.
    expect(screen.getByRole("combobox", { name: "Your Fontainebleau grade" })).toHaveValue("v6b");
  });

  const consensus = (viewerCanSuggest: boolean) => ({
    viewerCanSuggest,
    systems: [{
      gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", totalVotes: 5,
      consensusValueId: "v6a+", officialValueId: "v6a", viewerValueId: null,
      buckets: [
        { gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null, votes: 1 },
        { gradeValueId: "v6a+", label: "6A+", rank: 6, colorHex: null, votes: 3 },
        { gradeValueId: "v6b", label: "6B", rank: 7, colorHex: null, votes: 1 },
      ],
      scale: [
        { gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null, votes: 1 },
        { gradeValueId: "v6a+", label: "6A+", rank: 6, colorHex: null, votes: 3 },
      ],
    }],
  });

  it("shows the vote distribution next to, not instead of, the official grade", async () => {
    reply("GET", "/api/boulders/b1/grade-consensus", consensus(false));
    renderAt("/b", "/b", <CommunityGradeSection boulderId="b1" />);

    expect(await screen.findByText("5 votes")).toBeInTheDocument();
    // The two answers side by side: the gym's grade, and the most voted with how many votes out of how many.
    const summary = screen.getByText("Most voted").closest("dl")!;
    expect(within(summary).getByText("Official").nextElementSibling).toHaveTextContent("6A");
    expect(within(summary).getByText("Most voted").nextElementSibling).toHaveTextContent("6A+ 3 of 5");
    // The distribution is folded away, and opens with every grade's count and share.
    await userEvent.click(screen.getByText("See the votes"));
    expect(screen.getByRole("listitem", { name: "6A+: 3 of 5" })).toHaveTextContent("60%");
    expect(screen.getByRole("listitem", { name: "6A: 1 of 5" })).toHaveTextContent("20%");
    expect(screen.getByText("Log an attempt to suggest a grade.")).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("lets climbers who tried it suggest a grade", async () => {
    reply("GET", "/api/boulders/b1/grade-consensus", consensus(true));
    reply("PUT", "/api/boulders/b1/grade-suggestions", consensus(true));
    renderAt("/b", "/b", <CommunityGradeSection boulderId="b1" />);

    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Your Fontainebleau grade" }), "v6a+");
    await waitFor(() => expect(calls.find((c) => c.method === "PUT")?.body).toEqual({ gradeSystemId: "font", gradeValueId: "v6a+" }));
  });
});

describe("Community grade, several systems", () => {
  const sys = (id: string, name: string, type: string, usedByGym: boolean, official: string | null) => ({
    gradeSystemId: id, systemName: name, systemType: type, totalVotes: 1, consensusValueId: `${id}-1`, officialValueId: official, viewerValueId: null,
    buckets: [{ gradeValueId: `${id}-1`, label: `${name} 1`, rank: 1, colorHex: null, votes: 1 }],
    scale: [{ gradeValueId: `${id}-1`, label: `${name} 1`, rank: 1, colorHex: null, votes: 1 }], usedByGym,
  });

  it("shows one system at a time, in the server's order, the standard scales the gym doesn't use last", async () => {
    reply("GET", "/api/boulders/b1/grade-consensus", {
      viewerCanSuggest: true,
      systems: [sys("font", "Fontainebleau", "FONTAINEBLEAU", true, "font-1"), sys("col", "Colour", "COLOR", true, null), sys("v", "V-scale", "V_SCALE", false, null)],
    });
    renderAt("/b", "/b", <CommunityGradeSection boulderId="b1" />);

    const tabs = await screen.findAllByRole("tab");
    expect(tabs.map((tab) => tab.textContent)).toEqual(["Fontainebleau", "Colour", "V-scale"]);
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
    // Every system can be voted, the ones the gym doesn't use too.
    expect(screen.getByRole("combobox", { name: "Your V-scale grade" })).toBeInTheDocument();
    const vPanel = screen.getByRole("tabpanel", { name: "V-scale" });
    expect(within(vPanel).getByText("Not used at this gym")).toBeInTheDocument();

    await userEvent.click(tabs[2]!);
    expect(tabs[2]).toHaveAttribute("aria-selected", "true");
    expect(tabs[0]).toHaveAttribute("aria-selected", "false");
  });
});

describe("Community videos", () => {
  const vid = (id: string, over: object = {}) => ({ id, boulderId: "b1", author: person("u9", `Climber ${id}`), videoUrl: `/${id}.mp4`, thumbnailUrl: `/${id}.jpg`, caption: null, status: "APPROVED", rejectionReason: null, createdAt: "2026-09-10T10:00:00Z", isMine: false, ...over });

  it("shows your videos in review separately with their status", async () => {
    reply("GET", "/api/boulders/b1/videos", {
      approved: { items: [], page: 1, pageSize: 12, total: 0, hasMore: false },
      mineInReview: [
        vid("v1", { author: person("u1", "Me"), status: "PENDING", isMine: true }),
        vid("v2", { author: person("u1", "Me"), status: "REJECTED", rejectionReason: "Wrong boulder", isMine: true }),
      ],
    });
    renderAt("/b", "/b", <CommunityVideosSection boulderId="b1" />);

    expect(await screen.findByText("Your videos in review")).toBeInTheDocument();
    expect(screen.getByText("In review")).toBeInTheDocument();
    expect(screen.getByText("Not approved")).toBeInTheDocument();
    expect(screen.getByText("No community videos yet.")).toBeInTheDocument();

    await userEvent.click(screen.getAllByRole("button", { name: /play video by me/i })[1]!);
    expect(within(screen.getByRole("dialog")).getByText("Not approved: Wrong boulder")).toBeInTheDocument();
  });

  it("lists approved videos as thumbnails, opens one at a time and pages in more when you reach the end", async () => {
    const page1 = Array.from({ length: 12 }, (_, i) => vid(`a${i}`));
    reply("GET", "/api/boulders/b1/videos", (() => {
      let call = 0;
      return () => (++call === 1
        ? { approved: { items: page1, page: 1, pageSize: 12, total: 13, hasMore: true }, mineInReview: [] }
        : { approved: { items: [vid("a12")], page: 2, pageSize: 12, total: 13, hasMore: false }, mineInReview: [] });
    })());
    renderAt("/b", "/b", <CommunityVideosSection boulderId="b1" />);

    expect(await screen.findByRole("heading", { name: "Community videos (13)" })).toBeInTheDocument();
    const rail = screen.getByLabelText("Approved videos");
    expect(within(rail).getAllByRole("button", { name: /play video/i })).toHaveLength(12);
    expect(within(rail).getByRole("button", { name: "Show 1 more" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Scroll videos right" })).toBeInTheDocument();
    expect(document.querySelectorAll("video")).toHaveLength(0); // no players until you open one

    await userEvent.click(within(rail).getByRole("button", { name: /climber a11/i }));
    const dialog = screen.getByRole("dialog", { name: "Video 12 of 12" });
    expect(dialog.querySelectorAll("video")).toHaveLength(1);

    await userEvent.click(within(dialog).getByRole("button", { name: "Next video" }));
    expect(await screen.findByRole("dialog", { name: "Video 13 of 13" })).toBeInTheDocument();
    expect(screen.getByText("Climber a12", { selector: ".list__title" })).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

describe("Moderation queue", () => {
  const gym = { id: "g1", slug: "crimp", name: "Crimp", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: "STAFF", follow: null, followerCount: 0 };
  const boulder = { id: "b1", gymId: "g1", gymName: "Crimp", sectorId: "s1", sectorName: "Cave", photoUrl: "/b.jpg", holdColor: "BLUE", grades: [], status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, rating: { average: null, count: 0 }, viewer: null };
  const video = (id: string, authorId: string) => ({ video: { id, boulderId: "b1", author: person(authorId, authorId === "me" ? "Me" : "Anna"), videoUrl: `/${id}.mp4`, thumbnailUrl: null, caption: null, status: "PENDING", rejectionReason: null, createdAt: "2026-09-10T10:00:00Z", isMine: authorId === "me" }, boulder });

  it("approves others' videos, never your own, and asks for a reason to reject", async () => {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/users/me", { id: "me", email: "me@x.com", displayName: "Me", avatarUrl: null, isPlatformAdmin: false, createdAt: "2026-01-01T00:00:00Z", staffGyms: [], pendingInvitations: 0 });
    reply("GET", "/api/gyms/g1/moderation/summary", { pendingVideos: 2, pendingReports: 0 });
    reply("GET", "/api/gyms/g1/moderation/videos", [video("v1", "anna"), video("v2", "me")]);
    reply("POST", "/api/videos/v1/approve", {});
    renderAt("/manage/crimp/moderation", "/manage/:slug", <ManageLayout />, <Route path="moderation" element={<ManageModeration />} />);

    expect(await screen.findByRole("radio", { name: "Videos (2)" })).toBeInTheDocument();
    const cards = await screen.findAllByRole("article");
    const mine = cards.find((c) => within(c).queryByText("@Me"))!;
    expect(within(mine).getByText(/another staff member has to review it/i)).toBeInTheDocument();
    expect(within(mine).queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();

    const annas = cards.find((c) => within(c).queryByText("@Anna"))!;
    await userEvent.click(within(annas).getByRole("button", { name: "Reject" }));
    expect(within(annas).getByRole("textbox", { name: /reason/i })).toBeInTheDocument();
    await userEvent.click(within(annas).getByRole("button", { name: "Cancel" }));
    await userEvent.click(within(annas).getByRole("button", { name: "Approve" }));
    expect(calls.some((c) => c.method === "POST" && c.path === "/api/videos/v1/approve")).toBe(true);
  });
});

describe("Video previews", () => {
  it("shows a frame of the video when there is no captured thumbnail", async () => {
    reply("GET", "/api/boulders/b1/videos", {
      approved: { items: [{ id: "old", boulderId: "b1", author: person("u9", "Anna"), videoUrl: "/old.mov", thumbnailUrl: null, caption: null, status: "APPROVED", rejectionReason: null, createdAt: "2026-09-10T10:00:00Z", isMine: false }], page: 1, pageSize: 12, total: 1, hasMore: false },
      mineInReview: [],
    });
    renderAt("/b", "/b", <CommunityVideosSection boulderId="b1" />);
    const tile = await screen.findByRole("button", { name: /play video by anna/i });
    const frame = tile.querySelector("video")!;
    expect(frame.getAttribute("src")).toBe("/old.mov#t=0.5");
    expect(frame.muted).toBe(true);
  });

  it("previews the chosen video before sending it", async () => {
    reply("GET", "/api/boulders/b1/videos", { approved: { items: [], page: 1, pageSize: 12, total: 0, hasMore: false }, mineInReview: [] });
    URL.createObjectURL = vi.fn(() => "blob:preview");
    URL.revokeObjectURL = vi.fn();
    renderAt("/b", "/b", <CommunityVideosSection boulderId="b1" />);
    await screen.findByRole("button", { name: "Choose a video" });

    const file = new File([new Uint8Array(10)], "send.mov", { type: "video/quicktime" });
    await userEvent.upload(document.querySelector("input[type=file]") as HTMLInputElement, file);

    const preview = screen.getByLabelText("Preview of the selected video");
    expect(preview).toHaveAttribute("src", "blob:preview");
    expect(screen.getByRole("button", { name: "Send for review" })).toBeInTheDocument();
  });
});

