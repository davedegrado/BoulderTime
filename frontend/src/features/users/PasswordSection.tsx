import { useState, type FormEvent } from "react";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { supabase } from "@/lib/supabase";
import { t } from "@/i18n/i18n";

/**
 * Sets or changes the account password. It matters most for people who signed in with Google: their account has no
 * password at all, so without this they are locked out the day that button isn't there.
 */
export function PasswordSection({ hasPassword }: { hasPassword: boolean }) {
  const toast = useToast();
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (password.length < 8) { setError(t("Use at least 8 characters.")); return; }
    if (password !== confirm) { setError(t("The two passwords don't match.")); return; }
    setBusy(true);
    setError(null);
    const { error: failure } = await supabase.auth.updateUser({ password });
    setBusy(false);
    if (failure) {
      setError(/same/i.test(failure.message)
        ? t("Choose a password different from the old one.")
        : t("The password couldn't be changed. Sign out, sign in again and retry."));
      return;
    }
    setPassword("");
    setConfirm("");
    toast.success(hasPassword ? t("Password changed") : t("Password set"));
  }

  return (
    <section className="section" aria-labelledby="password-title">
      <h2 id="password-title" className="section__title">{hasPassword ? t("Change password") : t("Set a password")}</h2>
      <form className="card form" onSubmit={onSubmit} noValidate>
        {!hasPassword && (
          <p className="field__hint">{t("Your account was created with Google. Set a password to be able to sign in with your email too.")}</p>
        )}
        <TextField label={t("New password")} type="password" autoComplete="new-password" value={password}
          onChange={(e) => setPassword(e.target.value)} hint={t("At least 8 characters.")} />
        <TextField label={t("Repeat the password")} type="password" autoComplete="new-password" value={confirm}
          onChange={(e) => setConfirm(e.target.value)} error={error ?? undefined} />
        <Button type="submit" loading={busy} disabled={password.length === 0}>
          {hasPassword ? t("Change password") : t("Set the password")}
        </Button>
      </form>
    </section>
  );
}
