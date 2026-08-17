import { CardTableComponent } from '@bill-book/ui-components';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Bank {
  bankId: number;
  bankCode: string;
  bankName: string;
  displayOrder: number;
  isSystem: boolean;
  isActive: boolean;
  accountCount: number;
}

/**
 * Banking › Banks. The institution, kept apart from the account so its name is
 * entered once and balances can be reported by bank rather than by whatever
 * each account happened to be called.
 */
@Component({
  selector: 'bb-banks-page',
  standalone: true,
  imports: [CardTableComponent, FormsModule, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './banks.page.html',
  styleUrl: './banks.page.scss',
})
export class BanksPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<Bank[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  showInactive = false;
  form = { bankCode: '', bankName: '', isActive: true };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<Bank[]>(`/api/banks?includeInactive=${this.showInactive}`));
    } catch {
      this.fail('Could not load banks.');
    } finally {
      this.busy.set(false);
    }
  }

  startAdd(): void {
    this.editingId.set(0);
    this.form = { bankCode: '', bankName: '', isActive: true };
  }

  startEdit(row: Bank): void {
    this.editingId.set(row.bankId);
    this.form = { bankCode: row.bankCode, bankName: row.bankName, isActive: row.isActive };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (!this.hasText(this.form.bankName)) {
      this.fail('Bank name is required.');
      return;
    }

    const body = {
      bankCode: this.form.bankCode.trim() || null,
      bankName: this.form.bankName.trim(),
      isActive: this.form.isActive,
    };

    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', '/api/banks', body);
      } else {
        await this.send('PUT', `/api/banks/${id}`, body);
      }
      this.editingId.set(null);
    }, 'Bank saved.');
  }

  async remove(row: Bank): Promise<void> {
    await this.run(() => this.send('DELETE', `/api/banks/${row.bankId}`, {}), 'Bank deleted.');
  }

  async onDrop(event: CdkDragDrop<Bank[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/banks/reorder', {
          movedId: moved.bankId,
          previousId: list[event.currentIndex - 1]?.bankId ?? null,
          nextId: list[event.currentIndex + 1]?.bankId ?? null,
        }),
      null,
    );
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

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
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
