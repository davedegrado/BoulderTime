import { useState, type FormEvent } from "react";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/auth/AuthProvider";
import { safeNext } from "@/auth/RequireAuth";
import { AuthLayout, GoogleButton, OrDivider } from "@/pages/AuthLayout";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";

export function SignInPage() {
  const { session, signInWithPassword, signInWithGoogle } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const next = safeNext(params.get("next"));
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<"password" | "google" | null>(null);

  if (session) return <Navigate to={next} replace />;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy("password");
    try {
      await signInWithPassword(email.trim(), password);
      navigate(next, { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(null);
    }
  }

  async function onGoogle() {
    setError(null);
    setBusy("google");
    try {
      await signInWithGoogle(next);
    } catch (err) {
      setError((err as Error).message);
      setBusy(null);
    }
  }

  return (
    <AuthLayout title="Sign in" footer={<>New to BoulderTime? <Link to={`/sign-up${next !== "/" ? `?next=${encodeURIComponent(next)}` : ""}`}>Create an account</Link></>}>
      <GoogleButton onClick={onGoogle} loading={busy === "google"} disabled={busy !== null} />
      <OrDivider />
      <form onSubmit={onSubmit} className="form" noValidate>
        {error && <p className="form__error" role="alert">{error}</p>}
        <TextField label="Email" type="email" autoComplete="email" inputMode="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        <TextField label="Password" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        <Button type="submit" block loading={busy === "password"} disabled={busy !== null || !email || !password}>Sign in</Button>
      </form>
    </AuthLayout>
  );
}
