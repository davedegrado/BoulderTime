import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { BellRing } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { enablePush, needsInstallFirst, pushStatus, pushSupportedHere } from "@/features/notifications/push";
import { errorMessage } from "@/lib/apiError";
import { pushWording } from "@/features/notifications/pushWording";
import { t } from "@/i18n/i18n";

const ASKED_KEY = "bt:push-asked-session";

/** The sign-in a token belongs to: the same across refreshes, new at every sign-in. */
export function signInIdOf(accessToken: string | undefined): string | null {
  try {
    const part = accessToken?.split(".")[1];
    if (!part) return null;
    const base64 = part.replace(/-/g, "+").replace(/_/g, "/").padEnd(part.length + ((4 - (part.length % 4)) % 4), "=");
    const claims = JSON.parse(atob(base64)) as { session_id?: string };
    return claims.session_id ?? null;
  } catch {
    return null;
  }
}

const readAsked = () => { try { return localStorage.getItem(ASKED_KEY); } catch { return null; } };
const writeAsked = (id: string) => { try { localStorage.setItem(ASKED_KEY, id); } catch { /* asked again next start: harmless */ } };

/**
 * After each sign-in, once: if this device doesn't receive notifications, ask whether it should — before the phone's
 * own prompt, which can only be shown once. Not again while the person stays signed in, whatever they answered; the
 * switch in Profile → Notifications is always there. Signing out drops this device's subscription, so the next
 * sign-in asks again.
 */
export function PushPrompt() {
  const { session } = useAuth();
  const toast = useToast();
  const queryClient = useQueryClient();
  const signIn = signInIdOf(session?.access_token);
  const [asked, setAsked] = useState(readAsked);
  const [busy, setBusy] = useState(false);
  const eligible = Boolean(signIn) && signIn !== asked && pushSupportedHere() && !needsInstallFirst();
  const status = useQuery({ queryKey: ["push", "status"], queryFn: pushStatus, enabled: eligible });

  const done = () => { if (signIn) { writeAsked(signIn); setAsked(signIn); } };

  // Nothing to ask when they already arrive here, or when the server can't send any.
  useEffect(() => {
    if (eligible && status.data && (status.data.subscribedOnThisDevice || !status.data.available)) done();
  });

  if (!eligible || !status.data || status.data.subscribedOnThisDevice || !status.data.available) return null;

  async function turnOn() {
    setBusy(true);
    try {
      const outcome = await enablePush(status.data?.publicKey ?? "");
      const words = pushWording();
      if (outcome === "denied") toast.error(words.denied);
      else if (outcome === "unsupported") toast.error(t("This browser can't do notifications."));
      else if (outcome === "misconfigured") toast.error(t("Notifications aren't set up correctly on the server."));
      else toast.success(words.on);
    } catch (e) {
      toast.error(errorMessage(e));
    } finally {
      setBusy(false);
      done();
      await queryClient.invalidateQueries({ queryKey: ["push", "status"] });
    }
  }

  return (
    <div className="sheet-backdrop">
      <div className="sheet push-prompt" role="dialog" aria-modal="true" aria-labelledby="push-prompt-title">
        <BellRing aria-hidden className="push-prompt__icon" />
        <h2 id="push-prompt-title" className="section__title">{t("Turn on notifications?")}</h2>
        <p className="push-prompt__text">
          {t("Know when new boulders go up at the gyms and sectors you follow, and when your gym posts news. You can change this any time in Profile → Notifications.")}
        </p>
        <Button onClick={turnOn} loading={busy}>{t("Turn on")}</Button>
        <button type="button" className="sheet__cancel" onClick={done} disabled={busy}>{t("Not now")}</button>
      </div>
    </div>
  );
}
