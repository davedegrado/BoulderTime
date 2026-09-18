import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/auth/AuthProvider";
import { safeNext } from "@/auth/RequireAuth";
import { EmptyState, LoadingState } from "@/components/States";
import { AlertTriangle } from "lucide-react";
import { t } from "@/i18n/i18n";

/**
 * Landing page for OAuth and email-confirmation redirects.
 * supabase-js (detectSessionInUrl + PKCE) exchanges the code automatically; we wait for the session.
 */
export function AuthCallbackPage() {
  const { session, initializing } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [timedOut, setTimedOut] = useState(false);
  const providerError = params.get("error_description");

  useEffect(() => {
    if (session) navigate(safeNext(params.get("next")), { replace: true });
  }, [session, navigate, params]);

  useEffect(() => {
    const t = window.setTimeout(() => setTimedOut(true), 10_000);
    return () => window.clearTimeout(t);
  }, []);

  if (providerError || (timedOut && !session && !initializing)) {
    return (
      <EmptyState
        icon={<AlertTriangle />}
        title={t("Sign-in didn't complete")}
        body={providerError ?? t("The sign-in link may have expired or been opened in a different browser.")}
        action={<Link to="/sign-in" className="btn btn--primary"><span>Back to sign in</span></Link>}
      />
    );
  }
  return <LoadingState label={t("Signing you in")} />;
}
