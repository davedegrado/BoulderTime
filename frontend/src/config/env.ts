/**
 * Validated, typed access to public build-time configuration.
 * Fails fast with a readable message instead of a blank screen when a variable is missing.
 *
 * Values may be relative paths (e.g. "/supabase", "/"). They are resolved against the page origin, which
 * lets the dev server proxy the API and Supabase through a single forwarded port (see scripts/dev.sh).
 */
function required(name: keyof ImportMetaEnv): string {
  const value = import.meta.env[name];
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`Missing ${String(name)}. Copy frontend/.env.example to frontend/.env.local and fill it in.`);
  }
  return value.trim();
}

function absolute(value: string): string {
  return value.startsWith("/") ? `${window.location.origin}${value}` : value;
}

export const env = {
  supabaseUrl: absolute(required("VITE_SUPABASE_URL")).replace(/\/+$/, ""),
  supabaseAnonKey: required("VITE_SUPABASE_ANON_KEY"),
  /** "" means same origin. */
  apiBaseUrl: required("VITE_API_BASE_URL").replace(/\/+$/, ""),
  /** Map tiles. OpenStreetMap's tiles are for light use only; set a tile provider for production (see docs). */
  mapTileUrl: import.meta.env.VITE_MAP_TILE_URL?.trim() || "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
  mapAttribution: import.meta.env.VITE_MAP_ATTRIBUTION?.trim() || '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
} as const;
