/// <reference types="vite/client" />
interface ImportMetaEnv {
  readonly VITE_SUPABASE_URL: string;
  readonly VITE_SUPABASE_ANON_KEY: string;
  readonly VITE_API_BASE_URL: string;
  readonly VITE_MAP_TILE_URL?: string;
  readonly VITE_MAP_ATTRIBUTION?: string;
  /**
   * "1" when the Android build carries its Firebase settings. Set by CI from the same condition that writes
   * google-services.json, so it can never claim notifications the build cannot do.
   */
  readonly VITE_PUSH_NATIVE?: string;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}
