import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

const CODE_KEY = 'bb.portal.code';
const TOKEN_KEY = 'bb.portal.token';
const EXPIRES_KEY = 'bb.portal.expiresAt';

/** Renew this long before the hour runs out, so a page never calls with a spent token. */
export const RENEW_BEFORE_MS = 5 * 60_000;

/** Never renew sooner than this, so a clock that disagrees with the server cannot loop. */
export const MIN_RENEW_DELAY_MS = 30_000;

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

/** How long to wait before exchanging the code again (TK-94). */
export function renewDelayMs(expiresAt: string, now: number): number {
  const expires = Date.parse(expiresAt);
  if (Number.isNaN(expires)) return MIN_RENEW_DELAY_MS;
  return Math.max(MIN_RENEW_DELAY_MS, expires - now - RENEW_BEFORE_MS);
}

/** Whether a session still has more than a minute to run. */
export function isFresh(expiresAt: string | null, now: number): boolean {
  if (!expiresAt) return false;
  const expires = Date.parse(expiresAt);
  return !Number.isNaN(expires) && expires - now > 60_000;
}

interface PortalSessionResponse {
  token: string;
  expiresAt: string;
  app: string;
}

/**
 * The contact's portal session (TK-94). A portal link carries a code, not a
 * token: `/access/:code` keeps the code for the tab's life and exchanges it at
 * Master for a one-hour token, then exchanges it again five minutes before the
 * hour is up. When the business revokes the link, the next exchange is refused
 * and the portal says the link has been withdrawn. The token names its app: a
 * School guardian's opens the parent portal, anyone else's the statement portal.
 */
@Injectable({ providedIn: 'root' })
export class PortalSession {
  private readonly http = inject(HttpClient);
  private renewTimer: ReturnType<typeof setTimeout> | null = null;

  readonly token = signal<string | null>(PortalSession.load(TOKEN_KEY));
  readonly expiresAt = signal<string | null>(PortalSession.load(EXPIRES_KEY));
  private code = PortalSession.load(CODE_KEY);

  readonly app = computed(() => (this.token() ? (readClaim(this.token(), 'app') ?? 'RetailErp') : null));

  /** Starts a session from a link's code. False when the link is expired or withdrawn. */
  async start(code: string): Promise<boolean> {
    this.code = code;
    PortalSession.save(CODE_KEY, code);
    return this.exchange();
  }

  /**
   * A usable session, exchanging the kept code again when the token is spent
   * or missing. False when there is no code, or the link no longer works.
   */
  async ensure(): Promise<boolean> {
    if (this.token() && isFresh(this.expiresAt(), Date.now())) {
      this.scheduleRenewal();
      return true;
    }
    return this.code ? this.exchange() : false;
  }

  /** Forgets the session and the code, as when the link has been withdrawn. */
  clear(): void {
    if (this.renewTimer) clearTimeout(this.renewTimer);
    this.renewTimer = null;
    this.code = null;
    this.token.set(null);
    this.expiresAt.set(null);
    for (const key of [CODE_KEY, TOKEN_KEY, EXPIRES_KEY]) PortalSession.remove(key);
  }

  private async exchange(): Promise<boolean> {
    try {
      const session = await firstValueFrom(
        this.http.post<PortalSessionResponse>('/api/portal/session', { code: this.code }),
      );
      this.token.set(session.token);
      this.expiresAt.set(session.expiresAt);
      PortalSession.save(TOKEN_KEY, session.token);
      PortalSession.save(EXPIRES_KEY, session.expiresAt);
      this.scheduleRenewal();
      return true;
    } catch {
      this.clear();
      return false;
    }
  }

  private scheduleRenewal(): void {
    const expiresAt = this.expiresAt();
    if (!expiresAt || !this.code) return;
    if (this.renewTimer) clearTimeout(this.renewTimer);
    this.renewTimer = setTimeout(() => void this.exchange(), renewDelayMs(expiresAt, Date.now()));
  }

  private static load(key: string): string | null {
    try {
      return sessionStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private static save(key: string, value: string): void {
    try {
      sessionStorage.setItem(key, value);
    } catch {
      // Private windows and blocked storage: the session lives in memory for this page.
    }
  }

  private static remove(key: string): void {
    try {
      sessionStorage.removeItem(key);
    } catch {
      // Nothing was stored.
    }
  }
}
