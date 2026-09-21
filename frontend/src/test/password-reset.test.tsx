import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

const resetPasswordForEmail = vi.fn();
const updateUser = vi.fn();
let recoverySession: object | null = null;
vi.mock("@/lib/supabase", () => ({
  supabase: {
    auth: {
      resetPasswordForEmail: (...a: unknown[]) => resetPasswordForEmail(...a),
      updateUser: (...a: unknown[]) => updateUser(...a),
      getSession: async () => ({ data: { session: recoverySession } }),
      onAuthStateChange: () => ({ data: { subscription: { unsubscribe() {} } } }),
    },
  },
}));

import { ForgotPasswordPage } from "@/pages/ForgotPasswordPage";
import { ResetPasswordPage } from "@/pages/ResetPasswordPage";

beforeEach(() => { resetPasswordForEmail.mockReset(); updateUser.mockReset(); recoverySession = null; });

describe("Forgot password", () => {
  it("sends the reset link back to this site and answers the same whether or not the account exists", async () => {
    resetPasswordForEmail.mockResolvedValue({ error: { message: "User not found" } });
    renderAt("/forgot-password", "/forgot-password", <ForgotPasswordPage />);

    await userEvent.type(screen.getByLabelText("Email"), "someone@example.com");
    await userEvent.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(resetPasswordForEmail).toHaveBeenCalledWith("someone@example.com", { redirectTo: `${window.location.origin}/reset-password` });
    // A missing account is not revealed.
    expect(await screen.findByText(/If an account exists for someone@example.com/)).toBeInTheDocument();
  });

  it("rejects an address that isn't an email before calling anything", async () => {
    renderAt("/forgot-password", "/forgot-password", <ForgotPasswordPage />);
    await userEvent.type(screen.getByLabelText("Email"), "not-an-email");
    await userEvent.click(screen.getByRole("button", { name: "Send reset link" }));
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();
    expect(resetPasswordForEmail).not.toHaveBeenCalled();
  });
});

describe("Choose a new password", () => {
  it("saves the new password when the link opened a recovery session", async () => {
    recoverySession = { access_token: "recovery" };
    updateUser.mockResolvedValue({ error: null });
    renderAt("/reset-password", "/reset-password", <ResetPasswordPage />);

    await userEvent.type(await screen.findByLabelText("New password"), "climbing-2026");
    await userEvent.type(screen.getByLabelText("Repeat the password"), "climbing-2026");
    await userEvent.click(screen.getByRole("button", { name: "Save the new password" }));

    await waitFor(() => expect(updateUser).toHaveBeenCalledWith({ password: "climbing-2026" }));
  });

  it("checks length and that both passwords match before saving", async () => {
    recoverySession = { access_token: "recovery" };
    renderAt("/reset-password", "/reset-password", <ResetPasswordPage />);

    await userEvent.type(await screen.findByLabelText("New password"), "short");
    await userEvent.click(screen.getByRole("button", { name: "Save the new password" }));
    expect(screen.getByText("Use at least 8 characters.")).toBeInTheDocument();

    await userEvent.clear(screen.getByLabelText("New password"));
    await userEvent.type(screen.getByLabelText("New password"), "climbing-2026");
    await userEvent.type(screen.getByLabelText("Repeat the password"), "climbing-2027");
    await userEvent.click(screen.getByRole("button", { name: "Save the new password" }));
    expect(screen.getByText("The two passwords don't match.")).toBeInTheDocument();
    expect(updateUser).not.toHaveBeenCalled();
  });
});
