/// <reference types="vitest" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [
    react(),
    // Installable app + offline shell. Only active in production builds (never in the dev server, to avoid stale caches).
    VitePWA({
      registerType: "autoUpdate",
      manifest: false, // public/manifest.webmanifest is the source of truth
      injectRegister: null, // registered in main.tsx
      workbox: {
        globPatterns: ["**/*.{js,css,html,svg,png,ico,webmanifest}"],
        globIgnores: ["**/app-icon-1024.png", "**/app-icon-512.png"], // store/maskable sizes: not needed offline
        navigateFallback: "/index.html",
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            // Public images (boulder photos, gym images, avatars): immutable paths, so cache-first is safe.
            urlPattern: ({ url }) => url.pathname.startsWith("/api/storage/files/") || url.pathname.includes("/storage/v1/object/public/"),
            handler: "CacheFirst",
            options: { cacheName: "bt-images", expiration: { maxEntries: 400, maxAgeSeconds: 60 * 60 * 24 * 30 }, cacheableResponse: { statuses: [0, 200] } },
          },
          {
            urlPattern: ({ url }) => url.origin === "https://fonts.googleapis.com" || url.origin === "https://fonts.gstatic.com",
            handler: "StaleWhileRevalidate",
            options: { cacheName: "bt-fonts", expiration: { maxEntries: 20 } },
          },
          // API data (personal, per account) is deliberately NOT cached by the service worker.
        ],
      },
    }),
  ],
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
