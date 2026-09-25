import { Injectable, computed, signal } from '@angular/core';

const TOKEN_KEY = 'bb.portal.token';

/** Reads one claim from a JWT's payload without verifying it; the server does that. */
export function readClaim(token: string | null, claim: string): string | null {
  if (!token) return null;
  const part = token.split('.')[1];
  if (!part) return null;
  try {
    const json = atob(part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '='));
    const value = (JSON.parse(json) as Record<string, unknown>)[claim];
    return typeof value === 'string' ? value : null;
  } catch {
    return null;
  }
}

/**
 * The contact's portal link token (TK-69). The link a school or shop sends
 * carries it as `?token=`; the `/portal` route keeps it here for the tab's
 * life and the portal interceptor sends it on every `/api/portal/` call. A
 * token naming `School` opens the parent portal; one naming no app is a
 * RetailErp customer's statement portal.
 */
@Injectable({ providedIn: 'root' })
export class PortalSession {
  readonly token = signal<string | null>(PortalSession.load());

  readonly app = computed(() => (this.token() ? (readClaim(this.token(), 'app') ?? 'RetailErp') : null));

  set(token: string): void {
    this.token.set(token);
    try {
      sessionStorage.setItem(TOKEN_KEY, token);
    } catch {
      // Private windows and blocked storage: the token lives in memory for this page.
    }
  }

  private static load(): string | null {
    try {
      return sessionStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }
}
