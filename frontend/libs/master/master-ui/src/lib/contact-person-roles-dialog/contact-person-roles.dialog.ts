import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MessageBoxComponent, TextInputComponent } from '@bill-book/ui-components';
import { ContactPersonRolesList } from '../contact-person-roles-list/contact-person-roles.list';

export type { ContactPersonRole } from '../contact-person-roles-list/contact-person-roles.list';

export interface RoleSavedEvent {
  roleId?: number;
  roleName: string;
}

/**
 * The role master as a popup. It opens from the contact form role dropdown
 * so somebody can add a missing role or edit an existing one without abandoning
 * what they have typed.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contact-person-roles-dialog',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    MessageBoxComponent,
    ContactPersonRolesList,
  ],
  templateUrl: './contact-person-roles.dialog.html',
  styleUrl: './contact-person-roles.dialog.scss',
  host: { '(document:keydown.escape)': 'close()' },
})
export class ContactPersonRolesDialog implements OnInit {
  private readonly http = inject(HttpClient);

  readonly mode = input<'add' | 'edit' | 'manage'>('add');
  readonly roleId = input<number | null>(null);
  readonly initialRoleName = input<string>('');

  /** Fires on close so the opener can reload its role list. */
  readonly closed = output<void>();

  /** Fires when a role is created or saved so the opener can select it. */
  readonly saved = output<RoleSavedEvent>();

  protected readonly currentMode = signal<'add' | 'edit' | 'manage'>('add');
  protected roleName = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.currentMode.set(this.mode());
    if (this.mode() === 'edit') {
      this.roleName = this.initialRoleName();
    }
  }

  close(): void {
    this.closed.emit();
  }

  switchToManage(): void {
    this.currentMode.set('manage');
    this.error.set(null);
  }

  switchToAdd(): void {
    this.currentMode.set('add');
    this.roleName = '';
    this.error.set(null);
  }

  async saveRole(): Promise<void> {
    const trimmed = this.roleName.trim();
    if (!trimmed) {
      this.error.set('Role name is required.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      if (this.currentMode() === 'edit' && this.roleId()) {
        await firstValueFrom(
          this.http.put(`/api/contact-person-roles/${this.roleId()}`, {
            roleName: trimmed,
            isActive: true,
          }),
        );
        this.saved.emit({ roleId: this.roleId()!, roleName: trimmed });
      } else {
        await firstValueFrom(
          this.http.post('/api/contact-person-roles', {
            roleName: trimmed,
            isActive: true,
          }),
        );
        this.saved.emit({ roleName: trimmed });
      }
    } catch (err: unknown) {
      const anyErr = err as {
        error?: {
          message?: string;
          detail?: string;
          title?: string;
          errors?: Record<string, string[]>;
        };
      };
      const errObj = anyErr?.error;
      const extracted = errObj?.message
        || (errObj?.errors ? Object.values(errObj.errors).flat().join(' ') : null)
        || errObj?.detail
        || errObj?.title
        || 'Could not save role.';
      this.error.set(extracted);
    } finally {
      this.busy.set(false);
    }
  }

  onRoleChangedInList(): void {
    this.saved.emit({ roleName: '' });
  }
}
