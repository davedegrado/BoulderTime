/**
 * Validated, typed access to public build-time configuration.
 * Fails fast with a readable message instead of a blank screen when a variable is missing.
 */
function required(name: keyof ImportMetaEnv): string {
  const value = import.meta.env[name];
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`Missing ${String(name)}. Copy frontend/.env.example to frontend/.env.local and fill it in.`);
  }
  return value.trim();
}

export const env = {
  supabaseUrl: required("VITE_SUPABASE_URL"),
  supabaseAnonKey: required("VITE_SUPABASE_ANON_KEY"),
  apiBaseUrl: required("VITE_API_BASE_URL").replace(/\/+$/, ""),
} as const;
