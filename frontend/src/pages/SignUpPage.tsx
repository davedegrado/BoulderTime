import { useState, type FormEvent } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { MailCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { safeNext } from "@/auth/RequireAuth";
import { AuthLayout } from "@/pages/AuthLayout";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { PasswordField } from "@/components/PasswordField";
import { EmptyState } from "@/components/States";
import { LegalSheet, type LegalDocument } from "@/features/users/LegalSheet";
import { LEGAL_VERSION } from "@/pages/legal/version";
import { t } from "@/i18n/i18n";
import { tRich } from "@/i18n/rich";

interface Errors { displayName?: string; email?: string; password?: string; age?: string; legal?: string; form?: string }

export function validateSignUp(displayName: string, email: string, password: string, minimumAge = true, legal = true): Errors {
  const errors: Errors = {};
  if (displayName.trim().length < 2) errors.displayName = t("Use at least 2 characters.");
  else if (displayName.trim().length > 40) errors.displayName = t("Keep it to 40 characters or fewer.");
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) errors.email = t("Enter a valid email address.");
  if (password.length < 8) errors.password = t("Use at least 8 characters.");
  if (!minimumAge) errors.age = t("Confirm that you're at least 14.");
  if (!legal) errors.legal = t("Accept the terms of use to create an account.");
  return errors;
}

/**
 * Creating an account. Both boxes start unticked and both are needed: being at least 14 (the age Italian law sets for
 * signing up alone) and accepting the terms. The privacy notice is read, not agreed to — consent to anything optional
 * would be a separate, optional box, and there is none. The server records both answers (ADR-041).
 */
export function SignUpPage() {
  const { session, signUpWithPassword } = useAuth();
  const [params] = useSearchParams();
  const next = safeNext(params.get("next"));
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [minimumAge, setMinimumAge] = useState(false);
  const [legal, setLegal] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const [busy, setBusy] = useState(false);
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [reading, setReading] = useState<LegalDocument | null>(null);

  if (session) return <Navigate to={next} replace />;

  if (sentTo) {
    return (
      <AuthLayout title={t("Check your inbox")}>
        <EmptyState icon={<MailCheck />} title={t("We sent a confirmation link to {email}.", { email: sentTo })} body={t("Open it on this device to finish creating your account.")} action={<Link to="/sign-in" className="btn btn--secondary"><span>{t("Back to sign in")}</span></Link>} />
      </AuthLayout>
    );
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const found = validateSignUp(displayName, email, password, minimumAge, legal);
    setErrors(found);
    if (Object.keys(found).length) return;
    setBusy(true);
    try {
      const { needsConfirmation } = await signUpWithPassword(email.trim(), password, displayName.trim(),
        { legalVersion: LEGAL_VERSION, minimumAgeConfirmed: minimumAge });
      if (needsConfirmation) setSentTo(email.trim());
    } catch (err) {
      setErrors({ form: (err as Error).message });
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout title={t("Create your account")}
      subtitle={t("Join the BoulderTime community to log your sends, comment on boulders and climb the leaderboard.")}
      footer={<>{t("Already climbing with us?")} <Link to="/sign-in">{t("Sign in")}</Link></>}>
      <form onSubmit={onSubmit} className="form" noValidate>
        {errors.form && <p className="form__error" role="alert">{errors.form}</p>}
        <TextField label={t("Display name")} autoComplete="nickname" placeholder={t("E.g. MatteoClimbs")} value={displayName}
          onChange={(e) => setDisplayName(e.target.value)} error={errors.displayName}
          hint={t("Shown on leaderboards and comments. A nickname is fine.")} />
        <TextField label={t("Email")} type="email" autoComplete="email" inputMode="email" placeholder={t("E.g. name@email.com")}
          value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
        <PasswordField label={t("Password")} autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)}
          error={errors.password} hint={t("At least 8 characters.")} />

        <div className="agree">
          <label className={`check check--plain ${errors.age ? "check--invalid" : ""}`}>
            <input type="checkbox" checked={minimumAge} onChange={(e) => setMinimumAge(e.target.checked)}
              aria-invalid={!!errors.age || undefined} aria-describedby={errors.age ? "signup-age-error" : undefined} />
            <span>{t("I'm at least 14 years old.")}</span>
          </label>
          {errors.age && <p id="signup-age-error" className="field__error">{errors.age}</p>}
          <label className={`check check--plain ${errors.legal ? "check--invalid" : ""}`}>
            <input type="checkbox" checked={legal} onChange={(e) => setLegal(e.target.checked)}
              aria-invalid={!!errors.legal || undefined} aria-describedby={errors.legal ? "signup-legal-error" : undefined} />
            <span>
              {tRich("I accept the {terms} and I have read the {privacy}.", {
                terms: <button type="button" className="link-btn" onClick={(e) => { e.preventDefault(); setReading("terms"); }}>{t("Terms of use")}</button>,
                privacy: <button type="button" className="link-btn" onClick={(e) => { e.preventDefault(); setReading("privacy"); }}>{t("Privacy notice")}</button>,
              })}
            </span>
          </label>
          {errors.legal && <p id="signup-legal-error" className="field__error">{errors.legal}</p>}
        </div>

        <Button type="submit" block className="btn--lg" loading={busy} disabled={busy}>{t("Create account")}</Button>
      </form>
      {reading && <LegalSheet document={reading} onClose={() => setReading(null)} />}
    </AuthLayout>
  );
}
