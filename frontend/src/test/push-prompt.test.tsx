import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";
import { PushPrompt, signInIdOf } from "@/features/notifications/PushPrompt";

const token = (sessionId: string) => `h.${btoa(JSON.stringify({ sub: "u1", session_id: sessionId })).replace(/=+$/, "")}.s`;
let accessToken = token("s1");
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: accessToken }, initializing: false }) }));

let subscribed = false;
const enable = vi.fn(async () => "enabled" as const);
vi.mock("@/features/notifications/push", () => ({
  pushSupportedHere: () => true,
  needsInstallFirst: () => false,
  pushStatus: async () => ({ available: true, publicKey: "k", subscribedOnThisDevice: subscribed }),
  enablePush: () => enable(),
}));

beforeEach(() => { localStorage.clear(); subscribed = false; accessToken = token("s1"); enable.mockClear(); });

describe("Asking for notifications after signing in", () => {
  it("reads the sign-in from the token", () => {
    expect(signInIdOf(token("abc"))).toBe("abc");
    expect(signInIdOf("not-a-token")).toBeNull();
    expect(signInIdOf(undefined)).toBeNull();
  });

  it("asks once per sign-in, and turns them on when accepted", async () => {
    const first = renderAt("/", "/", <PushPrompt />);
    await userEvent.click(await screen.findByRole("button", { name: "Turn on" }));
    expect(enable).toHaveBeenCalledTimes(1);
    first.unmount();

    // Same sign-in, app opened again: not asked.
    const again = renderAt("/", "/", <PushPrompt />);
    await new Promise((r) => setTimeout(r, 20));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    again.unmount();

    // A new sign-in asks again.
    accessToken = token("s2");
    renderAt("/", "/", <PushPrompt />);
    expect(await screen.findByRole("dialog", { name: "Turn on notifications?" })).toBeInTheDocument();
  });

  it("'Not now' is remembered for this sign-in, and nothing is asked where they already arrive", async () => {
    const view = renderAt("/", "/", <PushPrompt />);
    await userEvent.click(await screen.findByRole("button", { name: "Not now" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(enable).not.toHaveBeenCalled();
    view.unmount();

    subscribed = true;
    accessToken = token("s3");
    renderAt("/", "/", <PushPrompt />);
    await waitFor(() => expect(localStorage.getItem("bt:push-asked-session")).toBe("s3"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
