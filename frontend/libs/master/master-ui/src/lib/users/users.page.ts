import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef , TextInputComponent } from '@bill-book/ui-components';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface UserRow {
  userId: string;
  email: string;
  displayName: string;
  mobileNumber: string | null;
  roleId: number;
  roleName: string;
  lastLoginAt: string | null;
  isActive: boolean;
  isInvitePending: boolean;
  isLockedOut: boolean;
}

interface RoleOption {
  roleId: number;
  displayName: string;
}

/**
 * User management. Adding a user sends an invitation email — a temporary
 * password is never issued. Mobile number is collected but not yet verified.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-users-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, DatePipe, TextInputComponent],
  templateUrl: './users.page.html',
  styleUrl: './users.page.scss',
})
export class UsersPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly users = signal<UserRow[]>([]);
  protected readonly roles = signal<RoleOption[]>([]);
  protected readonly inviting = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  form = { email: '', displayName: '', mobileNumber: '', roleId: 0 };

  columns: ColumnDef[] = [
    { field: 'displayName', header: 'Name' },
    { field: 'email', header: 'Email' },
    { field: 'roleName', header: 'Role' },
    { field: 'lastLoginAt', header: 'Last login' },
    { field: 'status', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.users.set(await this.req<UserRow[]>('GET', '/api/users'));
      this.roles.set(await this.req<RoleOption[]>('GET', '/api/roles'));
    } catch {
      this.show('Could not load users.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async invite(): Promise<void> {
    if (!this.hasText(this.form.email)) {
      this.show('Email is required.', true);
      return;
    }

    if (!this.hasText(this.form.displayName)) {
      this.show('Name is required.', true);
      return;
    }

    if (this.form.roleId <= 0) {
      this.show('Select a role.', true);
      return;
    }

    this.busy.set(true);
    try {
      await this.req('POST', '/api/users', {
        email: this.form.email.trim(),
        displayName: this.form.displayName.trim(),
        mobileNumber: this.form.mobileNumber.trim() || null,
        roleId: this.form.roleId,
      });
      this.show(`Invitation sent to ${this.form.email}.`, false);
      this.form = { email: '', displayName: '', mobileNumber: '', roleId: 0 };
      this.inviting.set(false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not send the invitation.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async resend(user: UserRow): Promise<void> {
    this.busy.set(true);
    try {
      await this.req('POST', `/api/users/${user.userId}/resend-invite`);
      this.show(`Invitation resent to ${user.email}.`, false);
    } catch {
      this.show('Could not resend the invitation.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async revoke(user: UserRow): Promise<void> {
    if (!confirm(`Revoke access for ${user.displayName}?`)) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/users/${user.userId}`);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not revoke access.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}

