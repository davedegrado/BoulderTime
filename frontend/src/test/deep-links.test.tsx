import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { appPathFromUrl } from "@/lib/native";
import { ConfirmEmailPage } from "@/pages/ConfirmEmailPage";

const verifyOtp = vi.fn();
const getSession = vi.fn();
vi.mock("@/lib/supabase", () => ({
  supabase: { auth: { verifyOtp: (...a: unknown[]) => verifyOtp(...a), getSession: () => getSession() } },
}));

beforeEach(() => {
  verifyOtp.mockReset();
  getSession.mockReset().mockResolvedValue({ data: { session: null } });
});

describe("Links the phone hands to the app", () => {
  it("open the same page in the app, query and all", () => {
    expect(appPathFromUrl("https://bouldertime.com/boulders/b1")).toBe("/boulders/b1");
    expect(appPathFromUrl("https://www.bouldertime.com/auth/confirm?token_hash=x&type=email")).toBe("/auth/confirm?token_hash=x&type=email");
    expect(appPathFromUrl("https://bouldertime.com")).toBe("/");
  });

  it("are ignored when they aren't ours", () => {
    expect(appPathFromUrl("https://bouldertime.com.evil.example/boulders/b1")).toBeNull();
    expect(appPathFromUrl("http://bouldertime.com/boulders/b1")).toBeNull();
    expect(appPathFromUrl("javascript:alert(1)")).toBeNull();
    expect(appPathFromUrl("not a url")).toBeNull();
  });
});

const showAt = (url: string) =>
  render(
    <I18nProvider initial="it">
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/auth/confirm" element={<ConfirmEmailPage />} />
          <Route path="/reset-password" element={<p>Pagina nuova password</p>} />
          <Route path="/" element={<p>Home</p>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );

describe("Email links", () => {
  it("check the token with the server and go where the email meant", async () => {
    verifyOtp.mockResolvedValue({ error: null });
    showAt("/auth/confirm?token_hash=abc&type=recovery&next=/reset-password");

    expect(await screen.findByText("Pagina nuova password")).toBeInTheDocument();
    expect(verifyOtp).toHaveBeenCalledTimes(1);
    expect(verifyOtp).toHaveBeenCalledWith({ token_hash: "abc", type: "recovery" });
  });

  it("never follow a 'next' that leaves the site", async () => {
    verifyOtp.mockResolvedValue({ error: null });
    showAt("/auth/confirm?token_hash=abc&type=email&next=//evil.example");
    expect(await screen.findByText("Home")).toBeInTheDocument();
  });

  it("say plainly when the link is spent, and point to sign in", async () => {
    verifyOtp.mockResolvedValue({ error: new Error("expired") });
    showAt("/auth/confirm?token_hash=old&type=email");
    expect(await screen.findByText("Questo link non funziona più")).toBeInTheDocument();
  });

  it("let a link opened twice through, since the first time already signed the person in", async () => {
    verifyOtp.mockResolvedValue({ error: new Error("already used") });
    getSession.mockResolvedValue({ data: { session: { user: {} } } });
    showAt("/auth/confirm?token_hash=abc&type=email");
    expect(await screen.findByText("Home")).toBeInTheDocument();
  });

  it("refuse a link without a token or with an unknown type", async () => {
    showAt("/auth/confirm?type=email");
    await waitFor(() => expect(screen.getByText("Questo link non funziona più")).toBeInTheDocument());
    expect(verifyOtp).not.toHaveBeenCalled();
  });
});
