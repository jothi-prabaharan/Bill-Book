import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { InvoiceListItem, InvoiceService } from '@bill-book/sales-core';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  UiMessage,
} from '@bill-book/ui-components';

/** What one page asks for. Matches the grid's own default. */
const PAGE_SIZE = 25;

/**
 * The invoice list.
 *
 * **Paged on the server, not in the browser** — the same reasoning as the sales
 * order list, and more pressing here: invoices are the fastest-growing table a
 * branch has, so a list that fetched all of them to show twenty-five is the one
 * that gets slower every week.
 *
 * The **overdue** filter and its running total are what a collections call
 * actually needs: who owes, how much, and how late. Only a posted invoice can be
 * overdue — a draft owes nothing yet and a voided one never will — which is
 * decided on the server so the screen and a report cannot disagree about it.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MessageBoxComponent,
  ],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss',
})
export class InvoiceListComponent implements OnInit {
  private readonly invoices = inject(InvoiceService);
  private readonly router = inject(Router);

  protected readonly rows = signal<InvoiceListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly pageSize = PAGE_SIZE;

  /** Bound with ngModel, so they are plain fields rather than signals. */
  protected status = '';
  protected search = '';
  protected overdueOnly = false;

  protected readonly statuses: readonly { value: string; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'Draft', label: 'Draft' },
    { value: 'ReadyToPost', label: 'Ready to post' },
    { value: 'Posted', label: 'Posted' },
    { value: 'Void', label: 'Void' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date', dataType: 'date' },
    { field: 'documentNo', header: 'Number', isTemplate: true },
    { field: 'contactName', header: 'Customer' },
    { field: 'dueDate', header: 'Due', isTemplate: true },
    { field: 'totalAmount', header: 'Amount', align: 'right', dataType: 'money' },
    { field: 'settlementStatus', header: 'Payment', isTemplate: true },
    { field: 'status', header: 'Status', isTemplate: true },
  ];

  protected readonly isEmpty = computed(() => !this.loading() && this.rows().length === 0);

  /**
   * What the rows on this page come to.
   *
   * **Of the page, and it says so** — the server pages before it sums, so a
   * running total across every match would be a different query. A figure
   * labelled as the page's own is honest; the same figure labelled "total
   * outstanding" is the one somebody reconciles against and finds short.
   */
  protected readonly pageTotal = computed(() =>
    this.rows().reduce((sum, row) => sum + row.totalAmount, 0),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.messages.set([]);

    try {
      const result = await this.invoices.list({
        // The server clamps this again. Both do it, because a page that trusted
        // the server and a server that trusted the page would each be relying
        // on the other.
        skip: Math.max(0, (this.page() - 1) * PAGE_SIZE),
        take: PAGE_SIZE,
        status: this.status || undefined,
        search: this.search.trim() || undefined,
        overdueOnly: this.overdueOnly || undefined,
      });

      this.rows.set(result.rows);
      this.total.set(result.total);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.rows.set([]);
      this.total.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  /** A changed filter goes back to page one — page four of the old result is not page four of the new one. */
  protected async onFilterChange(): Promise<void> {
    this.page.set(1);
    await this.load();
  }

  protected async onPageChange(page: number): Promise<void> {
    this.page.set(page);
    await this.load();
  }

  protected open(invoice: InvoiceListItem): void {
    void this.router.navigate(['/sales/invoices', invoice.invoiceId]);
  }

  protected create(): void {
    void this.router.navigate(['/sales/invoices/new']);
  }

  /**
   * The tag a settlement status wears.
   *
   * A draft has no settlement at all — it is not an unpaid receivable — so it
   * gets no tag rather than an "Unpaid" one, which would put it beside invoices
   * somebody is actually chasing.
   */
  protected settlementTag(status: string | undefined): string {
    switch (status) {
      case 'Paid':
        return 'badge valid';
      case 'PartPaid':
        return 'badge soon';
      case 'Unpaid':
        return 'badge expired';
      default:
        return '';
    }
  }

  /** The tag class a status wears, from the shared set in `_tags.scss`. */
  protected statusTag(status: string): string {
    switch (status) {
      case 'Posted':
        return 'tag tag-accent';
      case 'Void':
        return 'tag tag-outline';
      default:
        return 'tag tag-neutral';
    }
  }
}

