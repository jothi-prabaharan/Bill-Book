import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../auth.service';

/**
 * The empty page an expired tenant lands on. The licenseActiveGuard routes
 * every feature URL here, and the API refuses feature calls with
 * 403 LicenseExpired — being logged in is all that still works.
 */
@Component({
  selector: 'bb-trial-expired-page',
  standalone: true,
  templateUrl: './trial-expired.page.html',
  styleUrl: './trial-expired.page.scss',
})
export class TrialExpiredPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  renew(): void {
    // Billing/upgrade flow is Phase 2 — this is the only other live route.
    window.alert('Contact sales to upgrade your plan.');
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
