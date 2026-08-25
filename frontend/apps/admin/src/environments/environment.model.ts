/** The environments this app builds for — Staging/UAT can join web's set later if admin needs its own deploy there. */
export type EnvironmentName = 'Development' | 'Production';

/**
 * Shape every environment file must satisfy.
 *
 * The active file is chosen at build time by the `fileReplacements` entry on
 * each Nx configuration in project.json, so `environment.ts` is what the code
 * imports and the build swaps the contents underneath it.
 */
export interface AppEnvironment {
  /** Angular production mode: drops dev-only assertions and change-detection checks. */
  readonly production: boolean;

  /** Which environment this bundle targets. Safe to surface in the UI. */
  readonly name: EnvironmentName;

  /**
   * Origin the API is served from, without a trailing slash.
   *
   * Empty string means same origin. In development that is what you want: the
   * dev server proxies /api to the Gateway via proxy.conf.json. In a deployed
   * environment set this to the Gateway origin, unless the app is served from
   * behind the Gateway itself.
   */
  readonly apiBaseUrl: string;
}
