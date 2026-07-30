import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

/**
 * Completes an invitation from the emailed link. The token and email arrive as
 * query parameters; the invitee only chooses a password.
 */
@Component({
  selector: 'bb-accept-invitation-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-card">
      <h1>Set your password</h1>

      @if (!token()) {
        <p class="error">
          This link is missing its token. Ask your administrator to resend the invitation.
        </p>
        <p><a routerLink="/login">Back to sign in</a></p>
      } @else {
        <p class="hint">Welcome{{ email() ? ', ' + email() : '' }}. Choose a password to finish.</p>
        <form (ngSubmit)="submit()">
          <label>
            New password
            <input name="password" type="password" minlength="8" [(ngModel)]="password" required />
          </label>
          <label>
            Confirm password
            <input name="confirmPassword" type="password" [(ngModel)]="confirmPassword" required />
          </label>
          @if (error()) {
            <p class="error">{{ error() }}</p>
          }
          <button type="submit" [disabled]="busy()">Set password and continue</button>
        </form>
      }
    </div>
  `,
})
export class AcceptInvitationPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly token = signal<string | null>(null);
  protected readonly email = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  password = '';
  confirmPassword = '';

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.token.set(params.get('token'));
    this.email.set(params.get('email'));
  }

  async submit(): Promise<void> {
    if (this.password !== this.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.http.post('/api/auth/accept-invitation', {
          email: this.email(),
          token: this.token(),
          password: this.password,
          confirmPassword: this.confirmPassword,
        }),
      );
      await this.router.navigateByUrl('/login');
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.error.set(anyErr?.error?.message ?? 'This invitation link is invalid or has expired.');
    } finally {
      this.busy.set(false);
    }
  }
}
