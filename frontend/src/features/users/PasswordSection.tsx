import { useState, type FormEvent } from "react";
import { PasswordField } from "@/components/PasswordField";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { supabase } from "@/lib/supabase";
import { t } from "@/i18n/i18n";

/**
 * Sets or changes the account password. It matters most for people who signed in with Google: their account has no
 * password at all, so without this they are locked out the day that button isn't there.
 */
/** `embedded`: just the form, for a place that already has its own title (the profile's account section). */
export function PasswordSection({ hasPassword, embedded = false }: { hasPassword: boolean; embedded?: boolean }) {
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

  const form = (
      <form className={embedded ? "form" : "card form"} onSubmit={onSubmit} noValidate>
        {!hasPassword && (
          <p className="field__hint">{t("Your account was created with Google. Set a password to be able to sign in with your email too.")}</p>
        )}
        <PasswordField label={t("New password")} autoComplete="new-password" value={password}
          onChange={(e) => setPassword(e.target.value)} hint={t("At least 8 characters.")} />
        <PasswordField label={t("Repeat the password")} autoComplete="new-password" value={confirm}
          onChange={(e) => setConfirm(e.target.value)} error={error ?? undefined} />
        <Button type="submit" loading={busy} disabled={password.length === 0}>
          {hasPassword ? t("Change password") : t("Set the password")}
        </Button>
      </form>
  );
  if (embedded) return form;
  return (
    <section className="section" aria-labelledby="password-title">
      <h2 id="password-title" className="section__title">{hasPassword ? t("Change password") : t("Set a password")}</h2>
      {form}
    </section>
  );
}
