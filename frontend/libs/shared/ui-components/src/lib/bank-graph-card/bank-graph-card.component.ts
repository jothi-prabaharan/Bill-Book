import { ChangeDetectionStrategy } from '@angular/core';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface BankAccountGraphData {
  name: string;
  ccy: string;
  balText: string;
  chartLabel: string;
  book: string;
  stmt: string;
  seriesLabel: string;
  stmtText: string;
  metaText: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-graph-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bank-graph-card.component.html',
})
export class BankGraphCardComponent {
  @Input({ required: true }) account!: BankAccountGraphData;
}

