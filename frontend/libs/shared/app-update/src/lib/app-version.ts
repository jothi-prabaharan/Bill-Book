/**
 * Version arithmetic for the update check (TK-184). Pure, so the rule that
 * decides whether a running copy is blocked is tested without a device.
 */
export interface AppVersion {
  readonly major: number;
  readonly minor: number;
  readonly patch: number;
  /** Pre-release identifiers: `1.2.0-beta.2` is `['beta', 2]`. Empty for a release. */
  readonly pre: readonly (string | number)[];
}

const VERSION = /^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;

/** `1.2`, `v1.2.0`, `1.2.0-beta.1+42`; null for anything else. */
export function parseVersion(text: string | null | undefined): AppVersion | null {
  if (typeof text !== 'string') {
    return null;
  }

  const match = VERSION.exec(text.trim());

  if (!match) {
    return null;
  }

  return {
    major: Number(match[1]),
    minor: Number(match[2] ?? 0),
    patch: Number(match[3] ?? 0),
    pre: match[4] ? match[4].split('.').map((part) => (/^\d+$/.test(part) ? Number(part) : part)) : [],
  };
}

/** Semantic-version order: negative when `a` is older. A pre-release sorts below its release. */
export function compareVersions(a: AppVersion, b: AppVersion): number {
  for (const key of ['major', 'minor', 'patch'] as const) {
    if (a[key] !== b[key]) {
      return a[key] < b[key] ? -1 : 1;
    }
  }

  if (a.pre.length === 0 && b.pre.length === 0) {
    return 0;
  }

  if (a.pre.length === 0 || b.pre.length === 0) {
    return a.pre.length === 0 ? 1 : -1;
  }

  for (let i = 0; i < Math.max(a.pre.length, b.pre.length); i++) {
    const x = a.pre[i];
    const y = b.pre[i];

    if (x === undefined) {
      return -1;
    }

    if (y === undefined) {
      return 1;
    }

    if (x === y) {
      continue;
    }

    if (typeof x === 'number' && typeof y === 'number') {
      return x < y ? -1 : 1;
    }

    if (typeof x === 'number') {
      return -1;
    }

    if (typeof y === 'number') {
      return 1;
    }

    return x < y ? -1 : 1;
  }

  return 0;
}
