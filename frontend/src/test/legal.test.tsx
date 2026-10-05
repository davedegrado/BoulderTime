import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { LegalPage } from "@/pages/legal/LegalPage";
import { PRIVACY_IT } from "@/pages/legal/privacy.it";
import { TERMS_IT } from "@/pages/legal/terms.it";

const show = (document: "privacy" | "terms") =>
  render(<I18nProvider initial="it"><MemoryRouter><LegalPage document={document} /></MemoryRouter></I18nProvider>);

describe("Privacy notice and terms", () => {
  it("name the controller and a working way to reach him", () => {
    show("privacy");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Informativa sulla privacy");
    expect(screen.getByText(/Davide Luisi/)).toBeInTheDocument();
    expect(PRIVACY_IT).toContain("support@bouldertime.com");
  });

  it("state the promises the app actually keeps", () => {
    // Each of these is implemented: deletion with a grace period, an age limit, consent for other people in videos.
    expect(PRIVACY_IT).toContain("7 giorni");
    expect(PRIVACY_IT).toContain("14 anni");
    expect(PRIVACY_IT).toContain("Profilo → Cancellazione dell'account");
    expect(TERMS_IT).toContain("consenso delle persone riconoscibili");
    expect(TERMS_IT).toContain("Profilo → Classifiche");
  });

  it("list every service that handles the data", () => {
    for (const processor of ["Supabase", "Railway", "Cloudflare", "Resend", "MapTiler"]) {
      expect(PRIVACY_IT).toContain(processor);
    }
  });

  it("lets the reader move between the two documents", () => {
    show("terms");
    expect(screen.getByRole("link", { name: "Leggi l'informativa sulla privacy" })).toHaveAttribute("href", "/privacy");
  });
});
