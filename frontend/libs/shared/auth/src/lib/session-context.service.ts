import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AppId } from './app-id';
import { AuthService } from './auth.service';

/** `GET /api/me/context` (TK-43): the session, with no internal ids. */
export interface SessionContext {
  displayName: string;
  email: string;
  branchName: string;
  branchCode: string;
  app: AppId;
  /** Active, Trial, Expired, Suspended or NotLicensed. */
  licenseStatus: string;
  licenseExpiry: string | null;
  expiryIsBranchLevel: boolean;
  permissions: string[];
  /** The apps this user can switch to in this branch, with each one's licence. */
  apps: { app: AppId; licenseStatus: string }[];
}

/** A licence that lets its app be used. */
export function isLicenceOpen(status: string | null | undefined): boolean {
  return status === 'Active' || status === 'Trial';
}

/**
 * The signed-in session as the server describes it (H0.3, TK-44), read once
 * under the shell and again whenever the token changes: a branch switch or an
 * app switch mints a new token, and the context follows it.
 *
 * **Every page decision reads this, not the decoded token.** The page guard,
 * `*bbIfCan` and the no-access page all ask the same service, so a page and its
 * buttons cannot disagree about a permission. The server decides everything
 * regardless; this only stops a user walking into a page that will answer 403.
 */
@Injectable({ providedIn: 'root' })
export class SessionContextService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly context = signal<SessionContext | null>(null);

  private readonly permissionSet = computed(() => new Set(this.context()?.permissions ?? []));

  /** The token the context was read for, so a new token triggers a new read. */
  private loadedFor: string | null = null;
  private loading: Promise<SessionContext> | null = null;

  /** Whether the session holds a permission, by its full code. */
  has(permission: string): boolean {
    return this.permissionSet().has(permission);
  }

  readonly licenceOpen = computed(() => isLicenceOpen(this.context()?.licenseStatus));

  /** The context for the current token, read from the server when the token changed. */
  async ensure(): Promise<SessionContext> {
    const token = this.auth.accessToken();
    const current = this.context();

    if (current !== null && token === this.loadedFor) {
      return current;
    }

    this.loading ??= this.load(token);

    try {
      return await this.loading;
    } finally {
      this.loading = null;
    }
  }

  /** Forgets the context, e.g. on sign-out. */
  clear(): void {
    this.context.set(null);
    this.loadedFor = null;
  }

  private async load(token: string | null): Promise<SessionContext> {
    const context = await firstValueFrom(this.http.get<SessionContext>('/api/me/context'));
    this.context.set(context);
    this.loadedFor = token;
    return context;
  }
}
