import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@/auth/AuthProvider";
import { LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

/** Client-side gate for signed-in screens. The API still enforces authorization on every request. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { session, initializing } = useAuth();
  const location = useLocation();
  if (initializing) return <LoadingState label={t("Checking your session")} />;
  if (!session) {
    const next = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/sign-in?next=${next}`} replace />;
  }
  return <>{children}</>;
}

/** Only allow same-origin relative redirects to avoid open-redirects via ?next=. */
export function safeNext(raw: string | null, fallback = "/"): string {
  if (!raw) return fallback;
  return raw.startsWith("/") && !raw.startsWith("//") ? raw : fallback;
}
