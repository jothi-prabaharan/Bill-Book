import { ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

/**
 * Completes an invitation from the emailed link. The token and email arrive as
 * query parameters; the invitee only chooses a password.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-accept-invitation-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './accept-invitation.page.html',
  styleUrl: './accept-invitation.page.scss',
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

  async submit(form: NgForm): Promise<void> {
    if (form.invalid) return;
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

