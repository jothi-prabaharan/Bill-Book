/** The four environments this workspace builds for. */
export type EnvironmentName = 'Development' | 'Staging' | 'UAT' | 'Production';

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
   * dev server proxies /api to the individual services via proxy.conf.json.
   * In a deployed environment set this to the Gateway origin, unless the app
   * is served from behind the Gateway itself.
   */
  readonly apiBaseUrl: string;
}
