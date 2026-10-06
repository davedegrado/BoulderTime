import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { MailCheck } from "lucide-react";
import { AuthLayout } from "@/pages/AuthLayout";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { supabase } from "@/lib/supabase";
import { t } from "@/i18n/i18n";
import { publicOrigin } from "@/lib/native";

/**
 * Asks Supabase to email a password-reset link. The answer is the same whether or not an account exists for the
 * address, so the page can't be used to find out who is registered.
 */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const address = email.trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) {
      setError(t("Enter a valid email address."));
      return;
    }
    setBusy(true);
    setError(null);
    const { error: failure } = await supabase.auth.resetPasswordForEmail(address, {
      redirectTo: `${publicOrigin()}/reset-password`,
    });
    setBusy(false);
    // Only a rate limit is worth reporting; anything else would reveal whether the account exists.
    if (failure && /rate limit/i.test(failure.message)) {
      setError(t("Too many attempts. Wait a minute and try again."));
      return;
    }
    setSent(true);
  }

  if (sent) {
    return (
      <AuthLayout title={t("Check your inbox")} footer={<Link to="/sign-in">{t("Back to sign in")}</Link>}>
        <div className="state">
          <div className="state__icon" aria-hidden><MailCheck /></div>
          <p className="state__text">{t("If an account exists for {email}, we've sent a link to choose a new password. It expires in an hour.", { email: email.trim() })}</p>
          <p className="field__hint">{t("Nothing arrived? Check your spam folder.")}</p>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("Forgot your password?")} footer={<Link to="/sign-in">{t("Back to sign in")}</Link>}>
      <form className="form" onSubmit={onSubmit} noValidate>
        <p className="field__hint">{t("Enter your email and we'll send you a link to choose a new password.")}</p>
        <TextField label={t("Email")} type="email" autoComplete="email" required value={email}
          onChange={(e) => setEmail(e.target.value)} error={error ?? undefined} />
        <Button type="submit" loading={busy} block>{t("Send reset link")}</Button>
      </form>
    </AuthLayout>
  );
}
