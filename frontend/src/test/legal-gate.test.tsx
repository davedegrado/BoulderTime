import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "@/i18n/i18n";
import { ToastProvider } from "@/components/Toast";
import { AcceptLegalPage } from "@/pages/legal/AcceptLegalPage";
import { LEGAL_VERSION } from "@/pages/legal/version";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, signOut: async () => {} }) }));

const posted: { path: string; body: unknown }[] = [];
vi.mock("@/lib/api", () => ({
  api: { post: async (path: string, body: unknown) => { posted.push({ path, body }); return {}; } },
}));

const show = (returning: boolean) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider initial="it">
      <QueryClientProvider client={client}>
        <ToastProvider><MemoryRouter><AcceptLegalPage returning={returning} /></MemoryRouter></ToastProvider>
      </QueryClientProvider>
    </I18nProvider>,
  );
};

beforeEach(() => { posted.length = 0; });

describe("Accepting the terms", () => {
  it("won't let anyone through without ticking the box", async () => {
    show(false);

    expect(screen.getByRole("button", { name: "Continua" })).toBeDisabled();
    expect(screen.getByRole("link", { name: "Termini d'uso" })).toHaveAttribute("href", "/termini");
    expect(screen.getByRole("link", { name: "Informativa sulla privacy" })).toHaveAttribute("href", "/privacy");

    await userEvent.click(screen.getByRole("checkbox"));
    await userEvent.click(screen.getByRole("button", { name: "Continua" }));

    // The version is part of the record: knowing someone agreed is useless without knowing to what.
    await waitFor(() => expect(posted).toEqual([{ path: "/api/users/me/legal-acceptance", body: { version: LEGAL_VERSION } }]));
  });

  it("says plainly when it is asking again because the documents changed", () => {
    show(true);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Abbiamo aggiornato i termini");
  });
});
