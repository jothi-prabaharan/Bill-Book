import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '@bill-book/auth';

@Component({
  selector: 'bb-portal-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly router = inject(Router);
  
  readonly email = signal('');
  readonly password = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly authService = inject(AuthService);

  async onSubmit() {
    if (!this.email() || !this.password()) {
      this.error.set('Email and password are required.');
      return;
    }
    
    this.loading.set(true);
    this.error.set(null);

    try {
      const loginRes = await this.authService.login(this.email(), this.password());
      
      if (loginRes.organizations && loginRes.organizations.length > 0) {
        await this.authService.selectOrganization(loginRes.organizations[0].orgId);
        await this.router.navigate(['/dashboard']);
      } else {
        this.error.set('No linked organizations found for this contact.');
      }
    } catch (err: any) {
      if (err.status === 401) {
        this.error.set('Invalid email or password.');
      } else {
        this.error.set(err.error?.message || 'An error occurred during login.');
      }
    } finally {
      this.loading.set(false);
    }
  }
}
