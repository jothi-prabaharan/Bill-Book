/** The four environments this workspace builds for. */
export type EnvironmentName = 'Development' | 'Staging' | 'UAT' | 'Production';

/**
 * The docs app reads no API, so it carries only the build flag and the
 * environment label (useful for an environment banner).
 */
export interface DocsEnvironment {
  readonly production: boolean;
  readonly name: EnvironmentName;
}
