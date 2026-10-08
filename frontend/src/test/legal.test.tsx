import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { LegalPage } from "@/pages/legal/LegalPage";
import { PRIVACY_IT } from "@/pages/legal/privacy.it";
import { TERMS_IT } from "@/pages/legal/terms.it";
import { DELETION_IT } from "@/pages/legal/deletion.it";

const show = (document: "privacy" | "terms" | "deletion") =>
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
    for (const processor of ["Supabase", "Railway", "Cloudflare", "Resend", "MapTiler", "Firebase Cloud Messaging", "Apple Push Notification service"]) {
      expect(PRIVACY_IT).toContain(processor);
    }
  });

  it("lets the reader move between the two documents", () => {
    show("terms");
    expect(screen.getByRole("link", { name: "Leggi l'informativa sulla privacy" })).toHaveAttribute("href", "/privacy");
  });

  it("explain account deletion to someone who isn't signed in, as Google Play asks", () => {
    show("deletion");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Cancellare l'account BoulderTime");
    // The same path, grace period and contact as the privacy notice, so the two never disagree.
    expect(DELETION_IT).toContain("Profilo → Cancellazione dell'account");
    expect(DELETION_IT).toContain("7 giorni");
    expect(DELETION_IT).toContain("support@bouldertime.com");
    expect(PRIVACY_IT).toContain("bouldertime.com/cancella-account");
    expect(screen.getByRole("link", { name: "Vai al tuo profilo" })).toHaveAttribute("href", "/profile");
  });
});
