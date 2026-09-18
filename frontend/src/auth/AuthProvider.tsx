import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { Session, User } from "@supabase/supabase-js";
import { useQueryClient } from "@tanstack/react-query";
import { supabase } from "@/lib/supabase";
import { t } from "@/i18n/i18n";

interface AuthContextValue {
  session: Session | null;
  user: User | null;
  /** True until the persisted session has been read on startup. */
  initializing: boolean;
  signInWithPassword(email: string, password: string): Promise<void>;
  signUpWithPassword(email: string, password: string, displayName: string): Promise<{ needsConfirmation: boolean }>;
  signInWithGoogle(redirectTo?: string): Promise<void>;
  signOut(): Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [initializing, setInitializing] = useState(true);
  const queryClient = useQueryClient();

  useEffect(() => {
    let active = true;
    supabase.auth.getSession().then(({ data }) => {
      if (!active) return;
      setSession(data.session);
      setInitializing(false);
    });
    const { data: sub } = supabase.auth.onAuthStateChange((event, next) => {
      setSession(next);
      // Anything cached under a previous identity must not leak into the next one.
      if (event === "SIGNED_OUT" || event === "SIGNED_IN") queryClient.clear();
    });
    return () => {
      active = false;
      sub.subscription.unsubscribe();
    };
  }, [queryClient]);

  const signInWithPassword = useCallback(async (email: string, password: string) => {
    const { error } = await supabase.auth.signInWithPassword({ email, password });
    if (error) throw new Error(friendlyAuthError(error.message));
  }, []);

  const signUpWithPassword = useCallback(async (email: string, password: string, displayName: string) => {
    const { data, error } = await supabase.auth.signUp({
      email,
      password,
      options: {
        data: { display_name: displayName },
        emailRedirectTo: `${window.location.origin}/auth/callback`,
      },
    });
    if (error) throw new Error(friendlyAuthError(error.message));
    return { needsConfirmation: !data.session };
  }, []);

  const signInWithGoogle = useCallback(async (redirectTo?: string) => {
    const next = redirectTo ? `?next=${encodeURIComponent(redirectTo)}` : "";
    const { error } = await supabase.auth.signInWithOAuth({
      provider: "google",
      options: { redirectTo: `${window.location.origin}/auth/callback${next}` },
    });
    if (error) throw new Error(friendlyAuthError(error.message));
  }, []);

  const signOut = useCallback(async () => {
    const { error } = await supabase.auth.signOut();
    if (error) throw new Error(error.message);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ session, user: session?.user ?? null, initializing, signInWithPassword, signUpWithPassword, signInWithGoogle, signOut }),
    [session, initializing, signInWithPassword, signUpWithPassword, signInWithGoogle, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}

export function friendlyAuthError(message: string): string {
  const m = message.toLowerCase();
  if (m.includes("invalid login credentials")) return t("That email and password don't match an account.");
  if (m.includes("email not confirmed")) return t("Confirm your email first — check your inbox for the link.");
  if (m.includes("already registered")) return t("An account with this email already exists. Sign in instead.");
  if (m.includes("password should be")) return t("Use a password with at least 8 characters.");
  if (m.includes("rate limit")) return t("Too many attempts. Wait a minute and try again.");
  return message;
}
