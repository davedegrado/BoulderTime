/// <reference types="vitest" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  server: {
    port: 5173,
    host: true,
    // Allow GitHub Codespaces forwarded URLs in development.
    allowedHosts: [".app.github.dev", "localhost", "127.0.0.1"],
    // Dev-only: expose the API and local Supabase on the same origin as the app, so one forwarded port is enough.
    proxy: {
      "/api": { target: "http://127.0.0.1:5080", changeOrigin: true },
      // Local Supabase Auth (GoTrue) started by scripts/dev.sh. supabase-js calls {url}/auth/v1/*; GoTrue serves those at its root.
      "/supabase/auth/v1": { target: "http://127.0.0.1:9999", changeOrigin: true, rewrite: (path) => path.replace(/^\/supabase\/auth\/v1/, "") },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    env: {
      VITE_SUPABASE_URL: "http://localhost:54321",
      VITE_SUPABASE_ANON_KEY: "test-anon-key",
      VITE_API_BASE_URL: "http://localhost:5080",
    },
  },
});
