import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nProvider } from "@/i18n/i18n";
import { ToastProvider } from "@/components/Toast";
import { PasswordSection } from "@/features/users/PasswordSection";

const updateUser = vi.fn();
vi.mock("@/lib/supabase", () => ({ supabase: { auth: { updateUser: (...a: unknown[]) => updateUser(...a) } } }));

const show = (hasPassword: boolean) =>
  render(<I18nProvider initial="it"><ToastProvider><PasswordSection hasPassword={hasPassword} /></ToastProvider></I18nProvider>);

beforeEach(() => updateUser.mockReset());

describe("Setting an account password", () => {
  it("explains itself to someone who signed in with Google, and sets the password", async () => {
    updateUser.mockResolvedValue({ error: null });
    show(false);

    expect(screen.getByText(/creato con Google/)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("Nuova password"), "climbing-2026");
    await userEvent.type(screen.getByLabelText("Ripeti la password"), "climbing-2026");
    await userEvent.click(screen.getByRole("button", { name: "Imposta la password" }));

    await waitFor(() => expect(updateUser).toHaveBeenCalledWith({ password: "climbing-2026" }));
  });

  it("checks the two passwords before sending anything", async () => {
    show(true);
    expect(screen.queryByText(/creato con Google/)).not.toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Nuova password"), "corta");
    await userEvent.click(screen.getByRole("button", { name: "Cambia password" }));
    expect(screen.getByText("Usa almeno 8 caratteri.")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Ripeti la password"), "climbing-2027");
    await userEvent.click(screen.getByRole("button", { name: "Cambia password" }));
    expect(updateUser).not.toHaveBeenCalled();
  });
});
