import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));
vi.mock("@/lib/api", () => ({
  api: {
    get: async () => ({
      viewerCanSuggest: true,
      systems: [{
        gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", totalVotes: 1,
        consensusValueId: "v1", officialValueId: "v1", viewerValueId: null,
        buckets: [{ gradeValueId: "v1", label: "6A", rank: 5, colorHex: null, votes: 1 }],
        scale: [{ gradeValueId: "v1", label: "6A", rank: 5, colorHex: null, votes: 1 }],
      }],
    }),
  },
}));

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "@/components/Toast";

function renderItalian(ui: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<I18nProvider initial="it"><QueryClientProvider client={client}><ToastProvider><MemoryRouter>{ui}</MemoryRouter></ToastProvider></QueryClientProvider></I18nProvider>);
}

describe("Italian", () => {
  it("renders a screen in Italian, including values and plurals", async () => {
    renderItalian(<CommunityGradeSection boulderId="b1" />);

    expect(await screen.findByRole("heading", { name: "Grado della community" })).toBeInTheDocument();
    expect(screen.getByText(/Cosa ne pensano gli arrampicatori/)).toBeInTheDocument();
    expect(screen.getByText("1 voto · grado più votato 6A")).toBeInTheDocument();
    expect(screen.getByText("Ufficiale")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Il tuo grado Fontainebleau" })).toBeInTheDocument();
  });
});
