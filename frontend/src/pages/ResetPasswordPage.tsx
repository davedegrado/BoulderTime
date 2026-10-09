import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { AuthLayout } from "@/pages/AuthLayout";
import { PasswordField } from "@/components/PasswordField";
import { Button } from "@/components/Button";
import { LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { supabase } from "@/lib/supabase";
import { t } from "@/i18n/i18n";

type Stage = "checking" | "ready" | "invalid";

/**
 * Landing page of the reset link. Supabase exchanges the link's code for a short recovery session (the client does
 * it on load); with that session the person sets a new password.
 */
export function ResetPasswordPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const [stage, setStage] = useState<Stage>("checking");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let settled = false;
    const { data } = supabase.auth.onAuthStateChange((event, session) => {
      if (event === "PASSWORD_RECOVERY" || (session && event === "SIGNED_IN")) { settled = true; setStage("ready"); }
    });
    // The code may already have been exchanged before this page subscribed.
    supabase.auth.getSession().then(({ data: current }) => {
      if (current.session) { settled = true; setStage("ready"); }
    });
    const timer = window.setTimeout(() => { if (!settled) setStage("invalid"); }, 4000);
    return () => { data.subscription.unsubscribe(); window.clearTimeout(timer); };
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (password.length < 8) { setError(t("Use at least 8 characters.")); return; }
    if (password !== confirm) { setError(t("The two passwords don't match.")); return; }
    setBusy(true);
    setError(null);
    const { error: failure } = await supabase.auth.updateUser({ password });
    setBusy(false);
    if (failure) {
      setError(/same/i.test(failure.message) ? t("Choose a password different from the old one.") : t("The link has expired. Ask for a new one."));
      return;
    }
    toast.success(t("Password changed"));
    navigate("/", { replace: true });
  }

  if (stage === "checking") return <LoadingState label={t("Checking your link")} />;

  if (stage === "invalid") {
    return (
      <AuthLayout title={t("This link doesn't work anymore")} footer={<Link to="/sign-in">{t("Back to sign in")}</Link>}>
        <p className="state__text">{t("Reset links expire after an hour and can be used once. Ask for a new one.")}</p>
        <Link to="/forgot-password" className="btn btn--primary btn--block"><span>{t("Send a new link")}</span></Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("Choose a new password")}>
      <form className="form" onSubmit={onSubmit} noValidate>
        <PasswordField label={t("New password")} autoComplete="new-password" value={password}
          onChange={(e) => setPassword(e.target.value)} hint={t("At least 8 characters.")} />
        <PasswordField label={t("Repeat the password")} autoComplete="new-password" value={confirm}
          onChange={(e) => setConfirm(e.target.value)} error={error ?? undefined} />
        <Button type="submit" loading={busy} block>{t("Save the new password")}</Button>
      </form>
    </AuthLayout>
  );
}
