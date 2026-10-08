import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "@/i18n/i18n";
import { ToastProvider } from "@/components/Toast";
import { SuspendedPage } from "@/pages/SuspendedPage";
import { ReportPersonAction } from "@/features/users/ReportPersonAction";

const posted: { path: string; body: unknown }[] = [];
const signOut = vi.fn(async () => {});
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "x" }, signOut }) }));
vi.mock("@/lib/api", () => ({
  api: {
    get: async (path: string) => path === "/api/users/me/deletion"
      ? { pendingDeletion: false, requestedAt: null, erasedAfter: null }
      : { id: "me", email: "climber@example.com", displayName: "Climber", isSuspended: true, staffGyms: [], language: "it" },
    post: async (path: string, body: unknown) => { posted.push({ path, body }); return undefined; },
  },
}));

const show = (ui: React.ReactElement) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider initial="it">
      <QueryClientProvider client={client}>
        <ToastProvider><MemoryRouter>{ui}</MemoryRouter></ToastProvider>
      </QueryClientProvider>
    </I18nProvider>,
  );
};

beforeEach(() => { posted.length = 0; signOut.mockClear(); });

describe("A suspended account", () => {
  it("is told why, where to object, and can still sign out and delete itself", async () => {
    show(<SuspendedPage />);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Il tuo account è sospeso");
    expect(screen.getByRole("link", { name: "support@bouldertime.com" })).toHaveAttribute("href", "mailto:support@bouldertime.com");
    await userEvent.click(screen.getByRole("button", { name: "Esci" }));
    expect(signOut).toHaveBeenCalled();
    // The deletion section needs the account's address, which arrives with the profile.
    await waitFor(() => expect(screen.getAllByRole("button").length).toBeGreaterThan(1));
  });
});

describe("Reporting a person", () => {
  it("sends the reason to BoulderTime, from the person's profile", async () => {
    show(<ReportPersonAction userId="u-1" displayName="Marco" />);

    await userEvent.click(screen.getByRole("button", { name: /Segnala Marco/ }));
    expect(screen.getByText(/non saprà chi l'ha segnalato/)).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole("combobox"), "IMPERSONATION");
    await userEvent.click(screen.getByRole("button", { name: "Invia la segnalazione" }));

    await waitFor(() => expect(posted).toEqual([{ path: "/api/users/u-1/reports", body: { reason: "IMPERSONATION", description: undefined } }]));
  });
});
