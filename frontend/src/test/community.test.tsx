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

    expect(await screen.findByText("5 votes · consensus 6A+")).toBeInTheDocument();
    const officialRow = screen.getByText("Official").closest("li")!;
    expect(within(officialRow).getByText("6A")).toBeInTheDocument();
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

describe("Community videos", () => {
  it("tells uploaders their video is pending or why it was rejected", async () => {
    reply("GET", "/api/boulders/b1/videos", [
      { id: "v1", boulderId: "b1", author: person("u1", "Me"), videoUrl: "/v1.mp4", caption: null, status: "PENDING", rejectionReason: null, createdAt: "2026-09-10T10:00:00Z", isMine: true },
      { id: "v2", boulderId: "b1", author: person("u1", "Me"), videoUrl: "/v2.mp4", caption: null, status: "REJECTED", rejectionReason: "Wrong boulder", createdAt: "2026-09-09T10:00:00Z", isMine: true },
    ]);
    renderAt("/b", "/b", <CommunityVideosSection boulderId="b1" />);

    expect(await screen.findByText(/waiting for the gym's approval/i)).toBeInTheDocument();
    expect(screen.getByText("Not approved: Wrong boulder")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Choose a video" })).toBeInTheDocument();
  });
});

describe("Moderation queue", () => {
  const gym = { id: "g1", slug: "crimp", name: "Crimp", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: "STAFF", follow: null, followerCount: 0 };
  const boulder = { id: "b1", gymId: "g1", gymName: "Crimp", sectorId: "s1", sectorName: "Cave", photoUrl: "/b.jpg", holdColor: "BLUE", grades: [], status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, rating: { average: null, count: 0 }, viewer: null };
  const video = (id: string, authorId: string) => ({ video: { id, boulderId: "b1", author: person(authorId, authorId === "me" ? "Me" : "Anna"), videoUrl: `/${id}.mp4`, caption: null, status: "PENDING", rejectionReason: null, createdAt: "2026-09-10T10:00:00Z", isMine: authorId === "me" }, boulder });

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
