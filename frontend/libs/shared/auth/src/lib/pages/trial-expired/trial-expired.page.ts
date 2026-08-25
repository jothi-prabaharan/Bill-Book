import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../auth.service';

/**
 * The empty page an expired tenant lands on. The licenseActiveGuard routes
 * every feature URL here, and the API refuses feature calls with
 * 403 LicenseExpired — being logged in is all that still works.
 *
 * Two different things land here and they need different words. An account
 * whose licence has lapsed should renew. A branch that has reached its own end
 * date sits under an account that is perfectly valid, and sending that customer
 * to a billing page with nothing to pay is the wrong instruction.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-trial-expired-page',
  standalone: true,
  templateUrl: './trial-expired.page.html',
  styleUrl: './trial-expired.page.scss',
})
export class TrialExpiredPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** True when this branch's own end date is what stopped access. */
  protected readonly branchEnded = this.auth.expiryIsBranchLevel;

  /** The date access ended, formatted for reading, or null if we were not told. */
  protected readonly endedOn = computed(() => {
    const iso = this.auth.licenseExpiry();
    if (!iso) {
      return null;
    }

    const date = new Date(iso);
    return Number.isNaN(date.getTime()) ? null : date.toLocaleDateString();
  });

  renew(): void {
    // Billing/upgrade flow is Phase 2 — this is the only other live route.
    window.alert('Contact sales to upgrade your plan.');
  }

  /**
   * Signing out is the only way to another branch: the branch switcher lives on
   * a settings page, and every settings page is behind the same guard that sent
   * the user here. Saying so is better than leaving them to work it out.
   */
  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}

