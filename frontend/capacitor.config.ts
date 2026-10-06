import type { CapacitorConfig } from "@capacitor/cli";

/**
 * The store apps wrap the same build as the website: `npm run build` produces `dist`, Capacitor copies it into the
 * native projects. There is no second frontend.
 */
const config: CapacitorConfig = {
  appId: "com.bouldertime.app",
  appName: "BoulderTime",
  webDir: "dist",
  android: {
    // Served from https://localhost inside the app, so secure-context APIs (geolocation, crypto) behave as on the web.
    // The API allows this origin explicitly (Cors__AllowedOrigins on Railway).
  },
  server: { androidScheme: "https" },
  plugins: {
    SplashScreen: {
      // Hidden by the app once React has rendered, so nobody sees a blank page between splash and content.
      launchAutoHide: false,
      backgroundColor: "#F8F8F7",
      showSpinner: false,
    },
  },
};

export default config;
