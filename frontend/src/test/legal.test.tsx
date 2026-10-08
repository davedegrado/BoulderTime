import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import userEvent from "@testing-library/user-event";
import { LegalPage } from "@/pages/legal/LegalPage";
import { DeletionPage } from "@/pages/legal/DeletionPage";
import { DELETION_EN, DELETION_IT } from "@/pages/legal/deletion";
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
    for (const processor of ["Supabase", "Railway", "Cloudflare", "Resend", "MapTiler", "Firebase Cloud Messaging", "Apple Push Notification service"]) {
      expect(PRIVACY_IT).toContain(processor);
    }
  });

  it("lets the reader move between the two documents", () => {
    show("terms");
    expect(screen.getByRole("link", { name: "Leggi l'informativa sulla privacy" })).toHaveAttribute("href", "/privacy");
  });

});

describe("Account deletion page", () => {
  const open = (url: string, initial: "it" | "en" = "it") =>
    render(<I18nProvider initial={initial}><MemoryRouter initialEntries={[url]}><DeletionPage /></MemoryRouter></I18nProvider>);

  it("explains it to someone who isn't signed in, in the app's language, as Google Play asks", () => {
    open("/delete-account");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Cancellare l'account BoulderTime");
    expect(screen.getByRole("button", { name: /Italiano/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("link", { name: "Vai al tuo profilo" })).toHaveAttribute("href", "/profile");
  });

  it("switches to English with the flag, and back", async () => {
    open("/delete-account");
    await userEvent.click(screen.getByRole("button", { name: /English/ }));
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Delete your BoulderTime account");
    expect(screen.getByRole("link", { name: "Go to your profile" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Italiano/ }));
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Cancellare l'account BoulderTime");
  });

  it("opens in the language a link asks for", () => {
    open("/delete-account?lang=en");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Delete your BoulderTime account");
  });

  it("says the same in both languages, and the same as the privacy notice", () => {
    for (const [text, path, days] of [[DELETION_IT, "Profilo → Cancellazione dell'account", "7 giorni"], [DELETION_EN, "Profile → Account deletion", "7 days"]] as const) {
      expect(text).toContain(path);
      expect(text).toContain(days);
      expect(text).toContain("support@bouldertime.com");
    }
    // One section for one section, so a change to one language can't go unnoticed in the other.
    const headings = (text: string) => text.split("\n").filter((l) => l.startsWith("## ")).length;
    expect(headings(DELETION_EN)).toBe(headings(DELETION_IT));
    expect(PRIVACY_IT).toContain("bouldertime.com/delete-account");
  });
});

