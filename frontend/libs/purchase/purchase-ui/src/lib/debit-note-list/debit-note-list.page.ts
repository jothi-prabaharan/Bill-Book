import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DebitNoteListItem, DebitNoteService } from '@bill-book/purchase-core';

/**
 * Every debit note in the branch, newest first.
 *
 * The reason column carries real weight: only a purchase return sent goods back,
 * and telling that apart from a money-only correction at a glance is what the
 * column is for.
 */
@Component({
  selector: 'bb-debit-note-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './debit-note-list.page.html',
  styleUrl: './debit-note-list.page.scss',
})
export class DebitNoteListPage {
  private readonly notes = inject(DebitNoteService);

  protected readonly rows = signal<DebitNoteListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) {
      return this.rows();
    }

    return this.rows().filter(
      (row) =>
        row.documentNo.toLowerCase().includes(term) ||
        (row.billNo ?? '').toLowerCase().includes(term) ||
        (row.vendorBillNo ?? '').toLowerCase().includes(term) ||
        (row.contactName ?? '').toLowerCase().includes(term),
    );
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.rows.set(await this.notes.list());
    } catch {
      this.error.set('Debit notes could not be loaded. Try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onSearch(value: string): void {
    this.search.set(value);
  }

  protected statusClass(status: string): string {
    return `pill pill--${status.toLowerCase()}`;
  }
}
