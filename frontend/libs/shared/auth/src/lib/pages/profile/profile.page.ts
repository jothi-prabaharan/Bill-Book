import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  MessageBoxComponent,
  PasswordInputComponent,
  PhoneInputComponent,
  SelectComponent,
  TextInputComponent,
  BbSelectOption,
} from '@bill-book/ui-components';

export interface UserProfile {
  userId: string;
  email: string;
  displayName: string;
  mobileNumber?: string | null;
  themePreference: 'System' | 'Light' | 'Dark';
  roleName?: string | null;
  lastLoginAt?: string | null;
}

@Component({
  selector: 'bb-profile-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    TextInputComponent,
    PhoneInputComponent,
    PasswordInputComponent,
    SelectComponent,
    MessageBoxComponent,
  ],
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.scss',
})
export class ProfilePage implements OnInit {
  private readonly http = inject(HttpClient);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly changingPassword = signal(false);

  readonly notice = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly passwordNotice = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);

  readonly profile = signal<UserProfile | null>(null);

  displayName = '';
  mobileNumber = '';
  themePreference = 'System';

  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  readonly themeOptions: BbSelectOption<string>[] = [
    { value: 'System', label: 'System default' },
    { value: 'Light', label: 'Light mode' },
    { value: 'Dark', label: 'Dark mode' },
  ];

  ngOnInit(): void {
    void this.loadProfile();
  }

  async loadProfile(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const data = await firstValueFrom(
        this.http.get<UserProfile>('/api/auth/profile')
      );
      if (data) {
        this.profile.set(data);
        this.displayName = data.displayName || '';
        this.mobileNumber = data.mobileNumber || '';
        this.themePreference = data.themePreference || 'System';
      }
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.error.set(msg || 'Failed to load profile.');
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    this.notice.set(null);
    this.error.set(null);

    if (!this.displayName.trim()) {
      this.error.set('Display name is required.');
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(
        this.http.put('/api/auth/profile', {
          displayName: this.displayName.trim(),
          mobileNumber: this.mobileNumber?.trim() || null,
          themePreference: this.themePreference,
        })
      );

      this.notice.set('Profile details updated successfully.');
      this.profile.update((p) =>
        p
          ? {
              ...p,
              displayName: this.displayName.trim(),
              mobileNumber: this.mobileNumber?.trim() || null,
              themePreference: this.themePreference as 'System' | 'Light' | 'Dark',
            }
          : null,
      );
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.error.set(msg || 'Failed to update profile.');
    } finally {
      this.saving.set(false);
    }
  }

  async changePassword(): Promise<void> {
    this.passwordNotice.set(null);
    this.passwordError.set(null);

    if (!this.currentPassword) {
      this.passwordError.set('Current password is required.');
      return;
    }

    if (!this.newPassword || this.newPassword.length < 8) {
      this.passwordError.set('New password must be at least 8 characters.');
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('New passwords do not match.');
      return;
    }

    this.changingPassword.set(true);
    try {
      await firstValueFrom(
        this.http.put('/api/auth/profile', {
          displayName: this.displayName.trim(),
          mobileNumber: this.mobileNumber?.trim() || null,
          themePreference: this.themePreference,
          currentPassword: this.currentPassword,
          newPassword: this.newPassword,
        })
      );

      this.passwordNotice.set('Password changed successfully.');
      this.currentPassword = '';
      this.newPassword = '';
      this.confirmPassword = '';
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.passwordError.set(msg || 'Failed to change password.');
    } finally {
      this.changingPassword.set(false);
    }
  }

  userInitials(): string {
    const name = this.profile()?.displayName || '';
    if (!name.trim()) return 'U';
    return name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((w) => w[0].toUpperCase())
      .join('');
  }
}
