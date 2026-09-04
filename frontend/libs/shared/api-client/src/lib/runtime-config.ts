/**
 * Deployment settings a built bundle can be told at run time.
 *
 * **Why this exists.** The API origin used to be baked in at build time, one
 * environment file per environment, each carrying a TODO where the hostname
 * should be. That shape has two faults and this fixes both: it needs one build
 * per environment, so the artifact that passed UAT is not the artifact that
 * ships; and it puts a hostname in the repository, where changing it is a
 * commit and a release rather than a configuration change.
 *
 * The build-time files stay as the default — a development bundle needs no
 * ceremony, and the dev server proxies `/api` — but a deployment can override
 * them without a rebuild.
 *
 * **How a deployment sets it.** Serve a `window.__BB_CONFIG__` before the
 * bundle, from a file ops owns rather than one the build produced:
 *
 * ```html
 * <script>window.__BB_CONFIG__ = { apiBaseUrl: 'https://gateway.example' };</script>
 * ```
 *
 * A missing config, a missing key, or a browserless context (SSR, a unit test)
 * all fall through to the build-time value, so nothing has to exist for the app
 * to work.
 *
 * **Only non-secret settings belong here.** It is served to every browser that
 * loads the app, so it holds origins and feature flags and never a key. An
 * Angular bundle is a public file whatever it is built from; the difference
 * this makes is when the value can be changed, not who can read it.
 */
export interface RuntimeConfig {
  /** Origin the API is served from, without a trailing slash. Empty = same origin. */
  readonly apiBaseUrl?: string;
}

declare global {
  interface Window {
    __BB_CONFIG__?: RuntimeConfig;
  }
}

/**
 * The runtime API origin, or `fallback` when the deployment did not set one.
 *
 * Wrapped in a try/catch because reading a global is one of the few things that
 * can throw in a sandboxed frame, and an app that will not bootstrap because it
 * could not read an optional setting is worse than one using its default.
 */
export function resolveApiBaseUrl(fallback: string): string {
  try {
    if (typeof window === 'undefined') {
      return fallback;
    }

    const configured = window.__BB_CONFIG__?.apiBaseUrl;

    return typeof configured === 'string' ? configured : fallback;
  } catch {
    return fallback;
  }
}
