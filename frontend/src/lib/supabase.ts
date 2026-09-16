import { createClient } from "@supabase/supabase-js";
import { env } from "@/config/env";

/**
 * Supabase is used on the client ONLY for authentication (and, from Phase 3, for uploading
 * to storage via short-lived signed URLs issued by the API). All application data flows
 * through the ASP.NET Core API, which owns business rules and authorization.
 */
export const supabase = createClient(env.supabaseUrl, env.supabaseAnonKey, {
  auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true, flowType: "pkce" },
});
