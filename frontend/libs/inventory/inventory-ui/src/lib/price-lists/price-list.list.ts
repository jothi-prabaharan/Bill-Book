import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DataGridComponent, ColumnDef, TextInputComponent } from '@bill-book/ui-components';

interface PriceList {
  id: string;
  name: string;
  description: string;
  isActive: boolean;
}

@Component({
  selector: 'bb-price-list-list',
  standalone: true,
  imports: [CommonModule, FormsModule, DataGridComponent, TextInputComponent],
  templateUrl: './price-list.list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriceListListComponent implements OnInit {
  columns: ColumnDef[] = [
    { field: 'name', header: 'Tier Name' },
    { field: 'description', header: 'Description' },
    { field: 'active', header: 'Status' },
    { field: 'actions', header: 'Actions' }
  ];

  private readonly http = inject(HttpClient);
  
  protected readonly rows = signal<PriceList[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<string | null>(null);

  form = this.blank();

  ngOnInit() {
    void this.loadPriceLists();
  }

  async loadPriceLists(): Promise<void> {
    this.busy.set(true);
    try {
      const data = await this.get<PriceList[]>('/api/inventory/price-lists');
      this.rows.set(data);
    } catch {
      this.fail('Could not load price lists.');
    } finally {
      this.busy.set(false);
    }
  }

  startAdd(): void {
    this.editingId.set('new');
    this.form = this.blank();
  }

  startEdit(row: PriceList): void {
    this.editingId.set(row.id);
    this.form = {
      name: row.name,
      description: row.description,
      isActive: row.isActive
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (!this.hasText(this.form.name)) {
      this.fail('Price list name is required.');
      return;
    }

    const body = {
      ...this.form,
      name: this.form.name.trim(),
    };

    await this.run(async () => {
      if (id === 'new') {
        await this.send('POST', '/api/inventory/price-lists', body);
      } else {
        await this.send('PUT', `/api/inventory/price-lists/${id}`, body);
      }
      this.editingId.set(null);
    }, 'Price list saved.');
  }

  private blank() {
    return {
      name: '',
      description: '',
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
      await this.loadPriceLists();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.loadPriceLists();
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
