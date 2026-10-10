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
  ios: {
    // Served from capacitor://localhost: iOS doesn't let an app serve its own pages over https. The API allows this
    // origin explicitly too (Cors__AllowedOrigins on Railway). The page draws under the status bar and the home
    // indicator and keeps clear of them with the CSS safe-area insets, as the installed PWA already does.
    contentInset: "never",
  },
  server: { androidScheme: "https" },
  plugins: {
    SplashScreen: {
      // Hidden by the app once React has rendered, so nobody sees a blank page between splash and content.
      // The launch screen itself is native (LaunchScreen.storyboard on iOS, the launch theme on Android); this colour
      // matches it.
      launchAutoHide: false,
      backgroundColor: "#0C0D0F",
      showSpinner: false,
    },
    StatusBar: {
      // Light text while the dark launch screen is up; the app switches to dark text once it shows its first screen.
      style: "DARK",
    },
  },
};

export default config;
