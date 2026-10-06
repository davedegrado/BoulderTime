import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { AlertTriangle } from "lucide-react";
import type { EmailOtpType } from "@supabase/supabase-js";
import { supabase } from "@/lib/supabase";
import { safeNext } from "@/auth/RequireAuth";
import { EmptyState, LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

const TYPES: readonly EmailOtpType[] = ["email", "signup", "recovery", "invite", "email_change", "magiclink"];

/**
 * Where the links in our emails land: sign-up confirmation, password reset, email change.
 *
 * The link carries a one-time token that the server checks, so it works on any device and in any browser — unlike
 * the code-based links Supabase sends by default, which only work in the browser where sign-up started. That matters
 * as soon as the app exists: someone signs up in the app, opens the email, and the link may open in the browser.
 * It also goes straight to bouldertime.com, which is what lets the phone open it in the app.
 */
export function ConfirmEmailPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [failed, setFailed] = useState(false);
  const started = useRef(false);

  const tokenHash = params.get("token_hash") ?? "";
  const rawType = params.get("type") ?? "";
  const type = TYPES.find((x) => x === rawType);
  const next = safeNext(params.get("next") ?? (type === "recovery" ? "/reset-password" : "/"));

  useEffect(() => {
    // A token can be used once: guard against the double effect of development mode and a repeated deep link.
    if (started.current) return;
    started.current = true;
    if (!tokenHash || !type) { setFailed(true); return; }

    void (async () => {
      const { error } = await supabase.auth.verifyOtp({ token_hash: tokenHash, type });
      if (!error) { navigate(next, { replace: true }); return; }
      // The same link opened twice: the first time already signed the person in.
      const { data } = await supabase.auth.getSession();
      if (data.session) navigate(next, { replace: true });
      else setFailed(true);
    })();
  }, [tokenHash, type, next, navigate]);

  if (failed) {
    return (
      <EmptyState
        icon={<AlertTriangle />}
        title={t("This link no longer works")}
        body={t("It may have expired or already been used. If you've already confirmed your email, just sign in.")}
        action={<Link to="/sign-in" className="btn btn--primary"><span>{t("Sign in")}</span></Link>}
      />
    );
  }
  return <LoadingState label={t("Checking the link")} />;
}
