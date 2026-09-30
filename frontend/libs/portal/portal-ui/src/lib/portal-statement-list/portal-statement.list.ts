import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
} from '@bill-book/ui-components';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalStatement, invoiceLink } from '../retail/portal.models';

/**
 * The contact's statement (TK-95): what they owe, and — for a contact who is
 * also a vendor — what they are owed, each with its own running balance.
 * Documents show their own numbers, and an invoice opens in the portal.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-statement-list',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    NgTemplateOutlet,
    RouterLink,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './portal-statement.list.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalStatementList implements OnInit {
  /**
   * One column set per side of the statement — only the running balance's
   * heading differs. Fixed arrays, so the grid is not handed a new one on
   * every render.
   */
  protected readonly columnsFor: Record<string, ColumnDef[]> = {
    'Balance owed': PortalStatementList.columns('Balance owed'),
    'Balance due to you': PortalStatementList.columns('Balance due to you'),
  };

  private static columns(balanceLabel: string): ColumnDef[] {
    return [
      { field: 'ledgerDate', header: 'Date' },
      { field: 'documentNo', header: 'Document' },
      { field: 'debit', header: 'Debit', numeric: true },
      { field: 'credit', header: 'Credit', numeric: true },
      { field: 'balance', header: balanceLabel, numeric: true },
    ];
  }

  private readonly api = inject(PortalApi);

  protected readonly statement = signal<PortalStatement | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly invoiceLink = invoiceLink;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.statement.set(await this.api.statement());
    } catch {
      this.error.set('Your statement could not be loaded. Try again in a moment.');
    }
  }
}
