import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Branch {
  branchId: number;
  branchCode: string;
  branchName: string;
  isHeadOffice: boolean;
  gstin: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateId: number | null;
  postalCode: string | null;
  countryId: number | null;
  phoneNumber: string | null;
  mobileNumber: string | null;
  email: string | null;
  displayOrder: number;
  isActive: boolean;
}

interface State {
  stateId: number;
  stateCode: string;
  stateName: string;
}

type BranchForm = Omit<Branch, 'branchId' | 'displayOrder'>;

/**
 * Settings › Branches. The places the organization trades from.
 *
 * A branch is a reporting and numbering dimension, never a stock boundary —
 * stock is one pool across every branch, and splitting it here is what makes
 * weighted average cost differ per location.
 */
@Component({
  selector: 'bb-branches-page',
  standalone: true,
  imports: [FormsModule, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './branches.page.html',
  styleUrl: './branches.page.scss',
})
export class BranchesPage implements OnInit {
  private readonly http = inject(HttpClient);

  private readonly orgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  /** India. Replace with the org's country once the org context service lands. */
  private readonly defaultCountryId = 1;

  protected readonly rows = signal<Branch[]>([]);
  protected readonly states = signal<State[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  showInactive = false;
  form: BranchForm = this.blank();

  ngOnInit(): void {
    void this.load();
    void this.loadStates();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.get<Branch[]>(
          `/api/organizations/${this.orgId()}/branches?includeInactive=${this.showInactive}`,
        ),
      );
    } catch {
      this.fail('Could not load branches.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadStates(): Promise<void> {
    try {
      this.states.set(
        await this.get<State[]>(`/api/master/countries/${this.defaultCountryId}/states`),
      );
    } catch {
      // The GSTIN-versus-state check runs server-side regardless; without the
      // list the field is simply a number the user cannot pick.
    }
  }

  startAdd(): void {
    this.editingId.set(0);
    this.form = this.blank();
  }

  startEdit(row: Branch): void {
    this.editingId.set(row.branchId);
    this.form = {
      branchCode: row.branchCode,
      branchName: row.branchName,
      isHeadOffice: row.isHeadOffice,
      gstin: row.gstin,
      addressLine1: row.addressLine1,
      addressLine2: row.addressLine2,
      city: row.city,
      stateId: row.stateId,
      postalCode: row.postalCode,
      countryId: row.countryId ?? this.defaultCountryId,
      phoneNumber: row.phoneNumber,
      mobileNumber: row.mobileNumber,
      email: row.email,
      isActive: row.isActive,
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', `/api/organizations/${this.orgId()}/branches`, this.form);
      } else {
        await this.send('PUT', `/api/organizations/${this.orgId()}/branches/${id}`, this.form);
      }
      this.editingId.set(null);
    }, 'Branch saved.');
  }

  async deactivate(row: Branch): Promise<void> {
    await this.run(
      () => this.send('DELETE', `/api/organizations/${this.orgId()}/branches/${row.branchId}`, {}),
      'Branch deactivated.',
    );
  }

  async onDrop(event: CdkDragDrop<Branch[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', `/api/organizations/${this.orgId()}/branches/reorder`, {
          movedId: moved.branchId,
          previousId: list[event.currentIndex - 1]?.branchId ?? null,
          nextId: list[event.currentIndex + 1]?.branchId ?? null,
        }),
      null,
    );
  }

  /** The head office cannot be demoted or deactivated from here — pick another one first. */
  protected get editingHeadOffice(): boolean {
    const id = this.editingId();
    return id !== null && id !== 0 && (this.rows().find((r) => r.branchId === id)?.isHeadOffice ?? false);
  }

  protected stateNameOf(stateId: number | null): string {
    if (stateId === null) {
      return '—';
    }

    return this.states().find((s) => s.stateId === stateId)?.stateName ?? String(stateId);
  }

  private blank(): BranchForm {
    return {
      branchCode: '',
      branchName: '',
      isHeadOffice: false,
      gstin: null,
      addressLine1: null,
      addressLine2: null,
      city: null,
      stateId: null,
      postalCode: null,
      countryId: this.defaultCountryId,
      phoneNumber: null,
      mobileNumber: null,
      email: null,
      isActive: true,
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
