import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * The branch a user is signed in to, as the rest of the product needs to read it.
 *
 * **The seller half of every printed document.** A tax invoice has to name who
 * is supplying, with their GSTIN and their state, and that is this record — so
 * it belongs to Master rather than to whichever module happens to print first.
 * Sales prints invoices, Purchase prints orders, and Reporting will head its
 * statements with the same block; three copies of this fetch is two that fall
 * out of step the day a field is added.
 */
export interface OrganizationSummary {
  orgId: string;
  orgCode: string;
  name: string;
  baseCurrency: string;
  gstin: string | null;
  pan: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateId: number | null;
  postalCode: string | null;
  countryId: number;
  phoneNumber: string | null;
  mobileNumber: string | null;
  email: string | null;
  website: string | null;
  logoUrl: string | null;
}

/**
 * Reads the current branch, once.
 *
 * **Cached for the lifetime of the tab**, because it is the same answer on every
 * document a user prints and it changes only when somebody edits the branch's
 * own settings. `refresh()` is what that settings screen calls; nothing else
 * should need it.
 *
 * Switching organizations replaces the token and reloads the app, so there is no
 * cache to invalidate on that path.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);

  /** Null until the first read completes. Exposed so a template can wait. */
  readonly current = signal<OrganizationSummary | null>(null);

  private inFlight: Promise<OrganizationSummary> | null = null;

  /**
   * The current branch, from cache when it has already been read.
   *
   * Concurrent callers share one request — two documents printed at once should
   * not be two round trips for the same unchanging record.
   */
  async get(): Promise<OrganizationSummary> {
    const cached = this.current();
    if (cached) {
      return cached;
    }

    this.inFlight ??= this.fetch();

    try {
      return await this.inFlight;
    } finally {
      this.inFlight = null;
    }
  }

  /** Re-reads it. For the settings screen, after it has saved a change. */
  async refresh(): Promise<OrganizationSummary> {
    this.current.set(null);
    this.inFlight = null;
    return this.get();
  }

  private async fetch(): Promise<OrganizationSummary> {
    const org = await firstValueFrom(
      this.http.get<OrganizationSummary>('/api/organizations/current'),
    );

    this.current.set(org);
    return org;
  }
}
