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
/**
 * Only same-origin paths may be used as a post-sign-in destination.
 * Browsers treat a backslash like a slash, so "/\\evil.example" would leave the site: everything that isn't a plain
 * "/path" is refused, and so are control characters that could smuggle a scheme.
 */
export function safeNext(raw: string | null, fallback = "/"): string {
  if (!raw) return fallback;
  const value = raw.trim();
  if (!value.startsWith("/")) return fallback;
  if (/[\\]/.test(value)) return fallback;          // "/\evil.example" behaves like "//evil.example"
  if (value.startsWith("//")) return fallback;      // protocol-relative
  if (/[\u0000-\u001f\u007f]/.test(value)) return fallback;
  return value;
}
