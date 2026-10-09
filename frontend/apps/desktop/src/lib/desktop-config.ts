/**
 * Where the desktop app finds its server and its updates (TK-184).
 *
 * Three layers, later ones winning: what the installer was built with
 * (`desktop-config.json` beside `main.js`, written from the build's
 * environment), what an operator put in the user's own `desktop-config.json`
 * (so one installer serves several sites), and environment variables at
 * launch (development). Nothing here is secret.
 */
export interface DesktopConfig {
  /** Origin the `/api` calls are forwarded to, e.g. `https://books.example.com`. */
  readonly serverUrl: string | null;
  /** Folder electron-updater reads `latest.yml` and the installers from. */
  readonly updateUrl: string | null;
  /** The `version.json` the app checks; `{updateUrl}/version.json` unless set. */
  readonly updateManifestUrl: string | null;
  /** Development only: load the Angular dev server instead of the bundled build. */
  readonly devUrl: string | null;
}

export interface DesktopConfigLayer {
  readonly serverUrl?: unknown;
  readonly updateUrl?: unknown;
  readonly updateManifestUrl?: unknown;
}

export const ENV_KEYS = {
  serverUrl: 'BILLBOOK_SERVER_URL',
  updateUrl: 'BILLBOOK_UPDATE_URL',
  updateManifestUrl: 'BILLBOOK_UPDATE_MANIFEST_URL',
  devUrl: 'BILLBOOK_DEV_URL',
} as const;

export function resolveDesktopConfig(
  baked: DesktopConfigLayer | null,
  user: DesktopConfigLayer | null,
  env: Readonly<Record<string, string | undefined>>,
): DesktopConfig {
  const pick = (key: 'serverUrl' | 'updateUrl' | 'updateManifestUrl'): string | null =>
    httpUrl(env[ENV_KEYS[key]]) ?? httpUrl(user?.[key]) ?? httpUrl(baked?.[key]);

  const updateUrl = pick('updateUrl');

  return {
    serverUrl: pick('serverUrl'),
    updateUrl,
    updateManifestUrl: pick('updateManifestUrl') ?? (updateUrl ? `${updateUrl}/version.json` : null),
    devUrl: httpUrl(env[ENV_KEYS.devUrl]),
  };
}

/** An http(s) address without its trailing slash, or null. */
export function httpUrl(value: unknown): string | null {
  if (typeof value !== 'string' || value.trim() === '') {
    return null;
  }

  try {
    const url = new URL(value.trim());
    return url.protocol === 'http:' || url.protocol === 'https:' ? url.toString().replace(/\/+$/, '') : null;
  } catch {
    return null;
  }
}

/** A JSON file's contents as a layer, or null when it is missing or not JSON. */
export function parseConfigLayer(text: string | null): DesktopConfigLayer | null {
  if (text === null) {
    return null;
  }

  try {
    const value: unknown = JSON.parse(text);
    return value !== null && typeof value === 'object' ? (value as DesktopConfigLayer) : null;
  } catch {
    return null;
  }
}
