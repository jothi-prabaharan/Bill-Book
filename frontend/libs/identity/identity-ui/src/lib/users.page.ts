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
@Component({
  selector: 'bb-users-page',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <header class="page-head">
      <h1>Users</h1>
      <button type="button" (click)="inviting.set(!inviting())" [disabled]="busy()">
        + Invite user
      </button>
    </header>

    @if (message()) {
      <p [class]="messageIsError() ? 'error' : 'ok'">{{ message() }}</p>
    }

    @if (inviting()) {
      <form class="invite" (ngSubmit)="invite()">
        <label>
          Email
          <input name="email" type="email" [(ngModel)]="form.email" required />
        </label>
        <label>
          Name
          <input name="displayName" [(ngModel)]="form.displayName" required />
        </label>
        <label>
          Mobile <span class="opt">(optional)</span>
          <input name="mobileNumber" [(ngModel)]="form.mobileNumber" />
        </label>
        <label>
          Role
          <select name="roleId" [(ngModel)]="form.roleId" required>
            <option [ngValue]="0">Select…</option>
            @for (r of roles(); track r.roleId) {
              <option [ngValue]="r.roleId">{{ r.displayName }}</option>
            }
          </select>
        </label>
        <div class="invite-actions">
          <button type="submit" [disabled]="busy() || !form.roleId">Send invitation</button>
          <button type="button" class="link" (click)="inviting.set(false)">Cancel</button>
        </div>
        <p class="hint">
          They will receive an email with a link to set their own password. No temporary
          password is created.
        </p>
      </form>
    }

    <table class="grid">
      <thead>
        <tr><th>Name</th><th>Email</th><th>Role</th><th>Last login</th><th>Status</th><th></th></tr>
      </thead>
      <tbody>
        @for (u of users(); track u.userId) {
          <tr [class.inactive]="!u.isActive">
            <td>{{ u.displayName }}</td>
            <td>{{ u.email }}</td>
            <td>{{ u.roleName }}</td>
            <td>{{ u.lastLoginAt ? (u.lastLoginAt | date: 'medium') : '—' }}</td>
            <td>
              @if (!u.isActive) {
                <span class="tag off">Revoked</span>
              } @else if (u.isInvitePending) {
                <span class="tag pending">Invite pending</span>
              } @else if (u.isLockedOut) {
                <span class="tag locked">Locked</span>
              } @else {
                <span class="tag on">Active</span>
              }
            </td>
            <td class="row-actions">
              @if (u.isInvitePending && u.isActive) {
                <button type="button" class="link" (click)="resend(u)">Resend</button>
              }
              @if (u.isActive) {
                <button type="button" class="link danger" (click)="revoke(u)">Revoke</button>
              }
            </td>
          </tr>
        } @empty {
          <tr><td colspan="6">No users.</td></tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; }
    .error { color: #c0392b; }
    .ok { color: #187a4b; }
    .hint { color: #6a6f80; font-size: .8rem; grid-column: 1 / -1; margin: 0; }
    .opt { color: #8a8f9e; font-weight: 400; }
    .invite {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
      gap: .75rem; margin: 1rem 0; padding: 1rem; background: #f7f8fa; border-radius: 8px;
    }
    .invite label { display: grid; gap: .25rem; font-size: .85rem; }
    .invite input, .invite select { padding: .45rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; }
    .invite-actions { display: flex; gap: .75rem; align-items: end; }
    .grid { width: 100%; border-collapse: collapse; margin-top: 1rem; }
    .grid th, .grid td { text-align: left; padding: .55rem .6rem; border-bottom: 1px solid #e2e4ea; }
    .grid tr.inactive { opacity: .55; }
    .row-actions { display: flex; gap: .5rem; }
    .tag { font-size: .7rem; padding: .1rem .4rem; border-radius: 4px; }
    .tag.on { background: #d8f5e3; color: #187a4b; }
    .tag.pending { background: #ffe9c7; color: #8a5a00; }
    .tag.locked { background: #ffd9d6; color: #a3271c; }
    .tag.off { background: #eceef3; color: #6a6f80; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; font: inherit; padding: 0; }
    .link.danger { color: #c0392b; }
    button:not(.link) { padding: .5rem .9rem; border: 0; border-radius: 6px; background: #3557d6; color: #fff; font: inherit; cursor: pointer; }

    @media (max-width: 700px) {
      .grid thead { display: none; }
      .grid tr { display: grid; gap: .2rem; padding: .6rem 0; }
      .grid td { border: 0; padding: .1rem 0; }
    }
  `,
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
    this.busy.set(true);
    try {
      await this.req('POST', '/api/users', {
        email: this.form.email,
        displayName: this.form.displayName,
        mobileNumber: this.form.mobileNumber || null,
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

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
