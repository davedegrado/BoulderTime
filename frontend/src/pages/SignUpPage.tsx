import { useState, type FormEvent } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { MailCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { safeNext } from "@/auth/RequireAuth";
import { AuthLayout, GoogleButton, OrDivider } from "@/pages/AuthLayout";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { EmptyState } from "@/components/States";

interface Errors { displayName?: string; email?: string; password?: string; form?: string }

export function validateSignUp(displayName: string, email: string, password: string): Errors {
  const errors: Errors = {};
  if (displayName.trim().length < 2) errors.displayName = "Use at least 2 characters.";
  else if (displayName.trim().length > 40) errors.displayName = "Keep it to 40 characters or fewer.";
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) errors.email = "Enter a valid email address.";
  if (password.length < 8) errors.password = "Use at least 8 characters.";
  return errors;
}

export function SignUpPage() {
  const { session, signUpWithPassword, signInWithGoogle } = useAuth();
  const [params] = useSearchParams();
  const next = safeNext(params.get("next"));
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<Errors>({});
  const [busy, setBusy] = useState<"password" | "google" | null>(null);
  const [sentTo, setSentTo] = useState<string | null>(null);

  if (session) return <Navigate to={next} replace />;

  if (sentTo) {
    return (
      <AuthLayout title="Check your inbox">
        <EmptyState icon={<MailCheck />} title={`We sent a confirmation link to ${sentTo}.`} body="Open it on this device to finish creating your account." action={<Link to="/sign-in" className="btn btn--secondary"><span>Back to sign in</span></Link>} />
      </AuthLayout>
    );
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const found = validateSignUp(displayName, email, password);
    setErrors(found);
    if (Object.keys(found).length) return;
    setBusy("password");
    try {
      const { needsConfirmation } = await signUpWithPassword(email.trim(), password, displayName.trim());
      if (needsConfirmation) setSentTo(email.trim());
    } catch (err) {
      setErrors({ form: (err as Error).message });
    } finally {
      setBusy(null);
    }
  }

  async function onGoogle() {
    setBusy("google");
    try {
      await signInWithGoogle(next);
    } catch (err) {
      setErrors({ form: (err as Error).message });
      setBusy(null);
    }
  }

  return (
    <AuthLayout title="Create your account" footer={<>Already climbing with us? <Link to="/sign-in">Sign in</Link></>}>
      <GoogleButton onClick={onGoogle} loading={busy === "google"} disabled={busy !== null} />
      <OrDivider />
      <form onSubmit={onSubmit} className="form" noValidate>
        {errors.form && <p className="form__error" role="alert">{errors.form}</p>}
        <TextField label="Display name" autoComplete="nickname" value={displayName} onChange={(e) => setDisplayName(e.target.value)} error={errors.displayName} hint="Shown on comments and leaderboards." />
        <TextField label="Email" type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
        <TextField label="Password" type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} error={errors.password} hint="At least 8 characters." />
        <Button type="submit" block loading={busy === "password"} disabled={busy !== null}>Create account</Button>
      </form>
    </AuthLayout>
  );
}
