import { compareVersions, parseVersion } from './app-version';

/** The sections of `version.json`, one per installable build (TK-184). */
export type UpdateChannel = 'electron' | 'android' | 'ios';

/** One section of `version.json`, as the app reads it. */
export interface PlatformRelease {
  readonly latestVersion: string;
  readonly minSupportedVersion: string | null;
  readonly forceUpdate: boolean;
  readonly releaseNotes: string | null;
  readonly storeUrl: string | null;
}

export type UpdateManifest = Partial<Record<UpdateChannel, PlatformRelease>>;

const CHANNELS: readonly UpdateChannel[] = ['electron', 'android', 'ios'];

/**
 * Reads `version.json` leniently: a section without a usable `latestVersion`
 * is left out rather than failing the whole file, so a typo in the iOS section
 * never blocks a desktop user.
 */
export function parseManifest(raw: unknown): UpdateManifest {
  const manifest: Partial<Record<UpdateChannel, PlatformRelease>> = {};

  if (raw == null || typeof raw !== 'object') {
    return manifest;
  }

  for (const channel of CHANNELS) {
    const entry = (raw as Record<string, unknown>)[channel];

    if (entry == null || typeof entry !== 'object') {
      continue;
    }

    const fields = entry as Record<string, unknown>;
    const latest = text(fields['latestVersion']);

    if (latest === null || parseVersion(latest) === null) {
      continue;
    }

    manifest[channel] = {
      latestVersion: latest,
      minSupportedVersion: text(fields['minSupportedVersion']),
      forceUpdate: fields['forceUpdate'] === true,
      releaseNotes: text(fields['releaseNotes']),
      storeUrl: text(fields['storeUrl']),
    };
  }

  return manifest;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : null;
}

export type UpdateKind = 'none' | 'optional' | 'forced';

export type UpdateReason =
  | 'up-to-date'
  | 'newer'
  | 'force-flag'
  | 'below-minimum'
  | 'no-release'
  | 'unknown-version';

export interface UpdateDecision {
  readonly kind: UpdateKind;
  readonly reason: UpdateReason;
  readonly installedVersion: string | null;
  readonly latestVersion: string | null;
  readonly releaseNotes: string | null;
  readonly storeUrl: string | null;
}

export const NO_UPDATE: UpdateDecision = {
  kind: 'none',
  reason: 'no-release',
  installedVersion: null,
  latestVersion: null,
  releaseNotes: null,
  storeUrl: null,
};

/**
 * The rule, in order:
 * 1. below `minSupportedVersion` → forced, whatever `forceUpdate` says;
 * 2. below `latestVersion` → forced when `forceUpdate` is true, optional when not;
 * 3. otherwise nothing — `forceUpdate` never blocks a copy that is already current.
 *
 * An installed version that cannot be read decides nothing: blocking a till
 * because its own version string was odd would be the worse failure.
 */
export function decideUpdate(installed: string | null, release: PlatformRelease | undefined): UpdateDecision {
  if (!release) {
    return { ...NO_UPDATE, installedVersion: installed };
  }

  const base = {
    installedVersion: installed,
    latestVersion: release.latestVersion,
    releaseNotes: release.releaseNotes,
    storeUrl: release.storeUrl,
  };
  const current = parseVersion(installed);
  const latest = parseVersion(release.latestVersion);

  if (current === null || latest === null) {
    return { ...base, kind: 'none', reason: 'unknown-version' };
  }

  const minimum = parseVersion(release.minSupportedVersion);

  if (minimum !== null && compareVersions(current, minimum) < 0) {
    return { ...base, kind: 'forced', reason: 'below-minimum' };
  }

  if (compareVersions(current, latest) < 0) {
    return release.forceUpdate
      ? { ...base, kind: 'forced', reason: 'force-flag' }
      : { ...base, kind: 'optional', reason: 'newer' };
  }

  return { ...base, kind: 'none', reason: 'up-to-date' };
}
