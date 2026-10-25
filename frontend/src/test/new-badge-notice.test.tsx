import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";

const calls: { method: string; path: string }[] = [];
let fresh: unknown[] = [];
vi.mock("@/lib/api", () => ({
  api: {
    get: async (path: string) => {
      calls.push({ method: "GET", path });
      if (path === "/api/users/me/badges/new") return fresh;
      throw new Error(`No mock for GET ${path}`);
    },
    post: async (path: string) => { calls.push({ method: "POST", path }); },
  },
}));

import { NewBadgeNotice } from "@/features/badges/NewBadgeNotice";

const badge = (name: string) => ({ id: name, badgeId: "X", category: "PROGRESSION", name, description: `About ${name}`, art: "progression-1", earnedAt: "2026-10-25T00:00:00Z" });

function show() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={qc}><MemoryRouter><I18nProvider initial="en"><NewBadgeNotice userId="u1" /></I18nProvider></MemoryRouter></QueryClientProvider>);
}

beforeEach(() => { calls.length = 0; });

describe("New badge notice", () => {
  it("names a single new badge, links to the badges and marks it seen when closed", async () => {
    fresh = [badge("First boulder")];
    show();
    expect(await screen.findByText("New badge: First boulder")).toBeInTheDocument();
    expect(screen.getByRole("link")).toHaveAttribute("href", "/users/u1/badges");
    await userEvent.click(screen.getByRole("button", { name: "Close" }));
    await waitFor(() => expect(calls).toContainEqual({ method: "POST", path: "/api/users/me/badges/seen" }));
    expect(screen.queryByText("New badge: First boulder")).not.toBeInTheDocument();
  });

  it("counts several", async () => {
    fresh = [badge("A"), badge("B")];
    show();
    expect(await screen.findByText("2 new badges")).toBeInTheDocument();
  });

  it("shows nothing when there is nothing new", async () => {
    fresh = [];
    show();
    await waitFor(() => expect(calls).toContainEqual({ method: "GET", path: "/api/users/me/badges/new" }));
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
