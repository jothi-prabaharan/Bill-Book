import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BbSelectOption, SelectComponent, TextInputComponent } from '@bill-book/ui-components';

export interface ApiClientRow {
  id: string;
  name: string;
  roleId: number;
  roleName: string | null;
  isActive: boolean;
  lastUsedAt: string | null;
}

export interface ApiClientRole {
  roleId: number;
  displayName: string;
  isSystemRole: boolean;
}

/**
 * Settings › API keys (TK-29).
 *
 * A key acts as a role: an integration can do exactly what that role's
 * permissions allow, as a user holding it could (D-07). Only this business's
 * roles and the standard ones are offered, never one with platform access. The
 * key is shown once, when it is made; afterwards only its name, role and last
 * use are.
 */
@Component({
  selector: 'bb-api-clients-list',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent],
  templateUrl: './api-clients.list.html',
  styleUrl: './api-clients.list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiClientsListComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/master/api-clients';

  readonly clients = signal<ApiClientRow[]>([]);
  readonly roles = signal<ApiClientRole[]>([]);
  readonly newClientName = signal('');
  readonly newRoleId = signal<number | null>(null);
  readonly generatedKey = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  readonly roleOptions = computed<BbSelectOption<number>[]>(() =>
    this.roles().map((r) => ({ value: r.roleId, label: r.isSystemRole ? `${r.displayName} (standard)` : r.displayName })),
  );

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    try {
      const [clients, roles] = await Promise.all([
        firstValueFrom(this.http.get<ApiClientRow[]>(this.base)),
        firstValueFrom(this.http.get<ApiClientRole[]>(`${this.base}/roles`)),
      ]);
      this.clients.set(clients);
      this.roles.set(roles);
    } catch {
      this.error.set('Could not load the API keys.');
    }
  }

  async createClient(): Promise<void> {
    const name = this.newClientName().trim();
    const roleId = this.newRoleId();
    if (!name || !roleId) {
      this.error.set('Name the integration and choose the role it acts as.');
      return;
    }

    await this.run(async () => {
      const created = await firstValueFrom(
        this.http.post<{ apiKey: string; apiClient: ApiClientRow }>(this.base, { name, roleId }),
      );
      this.generatedKey.set(created.apiKey);
      this.newClientName.set('');
      this.newRoleId.set(null);
    });
  }

  async changeRole(client: ApiClientRow, roleId: number | null): Promise<void> {
    if (!roleId || roleId === client.roleId) {
      return;
    }
    await this.run(() => firstValueFrom(this.http.put(`${this.base}/${client.id}/role`, { roleId })));
  }

  async revoke(client: ApiClientRow): Promise<void> {
    await this.run(() => firstValueFrom(this.http.delete(`${this.base}/${client.id}`)));
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (err: unknown) {
      const failure = err as { error?: { message?: string } };
      this.error.set(failure?.error?.message ?? 'That did not work.');
    } finally {
      this.busy.set(false);
    }
    await this.load();
  }
}
