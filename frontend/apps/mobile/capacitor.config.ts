import type { CapacitorConfig } from '@capacitor/cli';

/**
 * The Android and iOS apps (TK-184): the `apps/web` build in a native shell.
 * `scripts/prepare-web.mjs` copies it into `www` with a `config.js` naming the
 * API and the update manifest, since a phone has no server of its own to
 * forward `/api` through.
 *
 * The app id is permanent once the app is in a store.
 */
const config: CapacitorConfig = {
  appId: 'com.billbook.app',
  appName: 'Bill Book',
  webDir: 'www',
  // The page's origin is https://localhost on Android and capacitor://localhost
  // on iOS; both go in the gateway's Cors:AllowedOrigins.
  server: { androidScheme: 'https' },
  android: { allowMixedContent: false },
  ios: { contentInset: 'never' },
  plugins: {
    // Angular's HttpClient stays on the web view's own fetch, so uploads and
    // downloads behave exactly as in a browser.
    CapacitorHttp: { enabled: false },
  },
};

export default config;
