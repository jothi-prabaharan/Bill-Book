import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { readApiFailure } from '@bill-book/api-client';
import { AgedReceivableRow, OutstandingService } from '@bill-book/sales-core';
import {
  ColumnDef,
  DataGridComponent,
  MessageBoxComponent,
  UiMessage,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-aging-summary-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, MessageBoxComponent],
  templateUrl: './aging-summary-list.component.html',
  styleUrl: './aging-summary-list.component.scss',
})
export class AgingSummaryListComponent implements OnInit {
  private readonly outstanding = inject(OutstandingService);

  protected readonly rows = signal<AgedReceivableRow[]>([]);
  protected readonly loading = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly columns: ColumnDef[] = [
    { field: 'customerName', header: 'Customer' },
    { field: 'current', header: 'Current', align: 'right', dataType: 'money' },
    { field: 'days1To30', header: '1-30 Days', align: 'right', dataType: 'money' },
    { field: 'days31To60', header: '31-60 Days', align: 'right', dataType: 'money' },
    { field: 'days61To90', header: '61-90 Days', align: 'right', dataType: 'money' },
    { field: 'days90Plus', header: '90+ Days', align: 'right', dataType: 'money' },
    { field: 'total', header: 'Total', align: 'right', dataType: 'money' },
  ];

  protected readonly isEmpty = computed(() => !this.loading() && this.rows().length === 0);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.messages.set([]);

    try {
      const result = await this.outstanding.getAgingSummary();
      this.rows.set(result);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.rows.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

