import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth.service';

/**
 * Two-step login. Step one posts credentials; step two picks the organization
 * (skipped automatically when there is exactly one).
 */
@Component({
  selector: 'bb-login-page',
  standalone: true,
  imports: [AuthShellComponent, ReactiveFormsModule, RouterLink],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
    remember: new FormControl(false, { nonNullable: true })
  });

  protected readonly step = signal<'credentials' | 'organization'>('credentials');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    if (this.loginForm.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const response = await this.auth.login(this.loginForm.value.email!, this.loginForm.value.password!);
      if (response.requiresOrgSelection) {
        this.step.set('organization');
      } else {
        await this.pick(response.organizations[0].orgId);
      }
    } catch (err: unknown) {
      this.error.set(messageOf(err, 'Invalid email or password.'));
    } finally {
      this.busy.set(false);
    }
  }

  async pick(orgId: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const tokens = await this.auth.selectOrganization(orgId);
      await this.router.navigateByUrl(tokens.licenseStatus === 'Expired' ? '/expired' : '/');
    } catch (err: unknown) {
      this.error.set(messageOf(err, 'Could not open that organization.'));
    } finally {
      this.busy.set(false);
    }
  }
}

function messageOf(err: unknown, fallback: string): string {
  const anyErr = err as { error?: { message?: string } };
  return anyErr?.error?.message ?? fallback;
}
