import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth.service';

/**
 * The empty page an expired tenant lands on. The licenseActiveGuard routes
 * every feature URL here, and the API refuses feature calls with
 * 403 LicenseExpired — being logged in is all that still works.
 */
@Component({
  selector: 'bb-trial-expired-page',
  standalone: true,
  template: `
    <div class="auth-card">
      <h1>Your trial has expired</h1>
      <p>Your data is safe, but access is paused until you renew.</p>
      <div class="actions">
        <button type="button" (click)="renew()">Renew / Upgrade</button>
        <button type="button" class="link" (click)="logout()">Sign out</button>
      </div>
    </div>
  `,
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
