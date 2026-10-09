import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { LEGAL_VERSION } from "@/pages/legal/version";

const signUp = vi.fn(async () => ({ needsConfirmation: true }));
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: null, signUpWithPassword: signUp }) }));

import { SignUpPage } from "@/pages/SignUpPage";

const show = () => render(<I18nProvider initial="it"><MemoryRouter><SignUpPage /></MemoryRouter></I18nProvider>);

async function fillIn() {
  await userEvent.type(screen.getByLabelText("Nome visualizzato"), "MatteoClimbs");
  await userEvent.type(screen.getByLabelText("Email"), "matteo@example.com");
  await userEvent.type(screen.getByLabelText("Password"), "unaPasswordLunga");
}

beforeEach(() => signUp.mockClear());

describe("Creating an account", () => {
  it("needs both boxes ticked, and neither starts ticked", async () => {
    show();
    const age = screen.getByRole("checkbox", { name: "Ho almeno 14 anni." });
    const legal = screen.getByRole("checkbox", { name: /Accetto i Termini d'uso/ });
    expect(age).not.toBeChecked();
    expect(legal).not.toBeChecked();

    await fillIn();
    await userEvent.click(screen.getByRole("button", { name: "Crea l'account" }));

    expect(signUp).not.toHaveBeenCalled();
    expect(screen.getByText("Conferma di avere almeno 14 anni.")).toBeInTheDocument();
    expect(screen.getByText("Accetta i termini d'uso per creare l'account.")).toBeInTheDocument();
  });

  it("sends the two answers with the account, so the next screen doesn't ask again", async () => {
    show();
    await fillIn();
    await userEvent.click(screen.getByRole("checkbox", { name: "Ho almeno 14 anni." }));
    await userEvent.click(screen.getByRole("checkbox", { name: /Accetto i Termini d'uso/ }));
    await userEvent.click(screen.getByRole("button", { name: "Crea l'account" }));

    expect(signUp).toHaveBeenCalledWith("matteo@example.com", "unaPasswordLunga", "MatteoClimbs",
      { legalVersion: LEGAL_VERSION, minimumAgeConfirmed: true });
    expect(await screen.findByText(/Ti abbiamo inviato un link di conferma|matteo@example.com/)).toBeInTheDocument();
  });

  it("shows the password on request", async () => {
    show();
    const password = screen.getByLabelText("Password");
    expect(password).toHaveAttribute("type", "password");
    await userEvent.click(screen.getByRole("button", { name: "Mostra la password" }));
    expect(password).toHaveAttribute("type", "text");
    await userEvent.click(screen.getByRole("button", { name: "Nascondi la password" }));
    expect(password).toHaveAttribute("type", "password");
  });

  it("opens the terms over the form, without losing what was typed", async () => {
    show();
    await userEvent.type(screen.getByLabelText("Nome visualizzato"), "MatteoClimbs");
    await userEvent.click(screen.getByRole("button", { name: "Termini d'uso" }));

    const sheet = screen.getByRole("dialog", { name: "Termini d'uso" });
    expect(sheet).toHaveTextContent("14 anni");
    // Opening the document must not tick the box for the reader.
    expect(screen.getByRole("checkbox", { name: /Accetto i Termini d'uso/ })).not.toBeChecked();
    await userEvent.click(screen.getByRole("button", { name: "Chiudi" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Nome visualizzato")).toHaveValue("MatteoClimbs");
  });
});
