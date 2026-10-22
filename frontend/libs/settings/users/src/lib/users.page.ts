import { ChangeDetectionStrategy } from '@angular/core';
import {
  BbSelectOption,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  EmailInputComponent,
  IconButtonComponent,
  InfoTooltipComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  UpgradeDialogService,
} from '@bill-book/ui-components';
import { PlanService, PlanSummary, planLimitOf, readApiFailure, seatBanner, seatsFull, userLimitOf } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
 *
 * A branch holds as many users as its plan allows, deactivated ones included
 * (D-68, TK-209). The banner says how many places are taken; when none is left
 * the Invite button is off, with the server's own sentence beside it, and the
 * Upgrade button asks for Elite.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-users-page',
  standalone: true,
  imports: [
    InfoTooltipComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    IconButtonComponent,
    FormsModule,
    DatePipe,
    MessageBoxComponent,
    TextInputComponent,
    SelectComponent,
    EmailInputComponent,
  ],
  templateUrl: './users.page.html',
  styleUrl: './users.page.scss',
  host: {
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class UsersPage implements OnInit {
  onEscape(): void {
    if (this.inviting()) {
      this.inviting.set(false);
    }
  }

  protected readonly roleOptions = computed<BbSelectOption<number>[]>(() =>
    this.roles().map((role) => ({ value: role.roleId, label: role.displayName })),
  );
  private readonly http = inject(HttpClient);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly plans = inject(PlanService);
  private readonly upgradeDialog = inject(UpgradeDialogService);
  private readonly session = inject(SessionContextService);

  protected readonly users = signal<UserRow[]>([]);
  protected readonly roles = signal<RoleOption[]>([]);
  protected readonly plan = signal<PlanSummary | null>(null);
  protected readonly inviting = signal(false);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  /** "5 of 5 User Slots Used (Includes Deactivated Users)"; nothing on a plan with no limit. */
  protected readonly banner = computed(() => {
    const plan = this.plan();
    return plan ? seatBanner(plan) : null;
  });

  /** No place is left to give. */
  protected readonly full = computed(() => {
    const plan = this.plan();
    return plan ? seatsFull(plan) : false;
  });

  protected readonly canUpgrade = computed(() => this.full() && this.plan()?.canUpgrade === true);

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
      this.show('Could not load users.', 'error');
    } finally {
      this.busy.set(false);
    }

    await this.refreshPlan();
  }

  startInvite(): void {
    if (!this.full()) {
      this.inviting.set(true);
    }
  }

  async invite(): Promise<void> {
    if (!this.hasText(this.form.email)) {
      this.show('Email is required.', 'error');
      return;
    }

    if (!this.hasText(this.form.displayName)) {
      this.show('Name is required.', 'error');
      return;
    }

    if (this.form.roleId <= 0) {
      this.show('Select a role.', 'error');
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
      this.show(`Invitation sent to ${this.form.email}.`, 'success');
      this.form = { email: '', displayName: '', mobileNumber: '', roleId: 0 };
      this.inviting.set(false);
      await this.load();
    } catch (err: unknown) {
      const limit = planLimitOf(err);
      if (limit) {
        // The branch filled up since this page was read. Say so in the
        // server's words, and bring the banner and the button into line.
        this.inviting.set(false);
        this.show(limit.message, 'warning');
        await this.refreshPlan();
      } else {
        this.show(readApiFailure(err).text, 'error');
      }
    } finally {
      this.busy.set(false);
    }
  }

  async upgrade(): Promise<void> {
    const plan = this.plan();
    await this.upgradeDialog.open({
      limit: (plan && userLimitOf(plan)) ?? undefined,
      canRequest: this.session.has('settings.edit'),
    });
    await this.refreshPlan();
  }

  async resend(user: UserRow): Promise<void> {
    this.busy.set(true);
    try {
      await this.req('POST', `/api/users/${user.userId}/resend-invite`);
      this.show(`Invitation resent to ${user.email}.`, 'success');
    } catch {
      this.show('Could not resend the invitation.', 'error');
    } finally {
      this.busy.set(false);
    }
  }

  async revoke(user: UserRow): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Revoke Access',
      message: `Revoke access for ${user.displayName}? They will still count towards the plan's user limit.`,
      confirmText: 'Revoke',
      tone: 'danger',
    });
    if (!ok) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/users/${user.userId}`);
      await this.load();
    } catch (err: unknown) {
      this.show(readApiFailure(err).text, 'error');
    } finally {
      this.busy.set(false);
    }
  }

  protected clearMessages(): void {
    this.messages.set([]);
  }

  /** The plan is context for the page, so failing to read it leaves the list usable. */
  private async refreshPlan(): Promise<void> {
    this.plan.set(await this.plans.summary().catch(() => null));
  }

  private show(text: string, tone: UiMessage['tone']): void {
    this.messages.set([{ tone, text }]);
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
