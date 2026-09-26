import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, RouterLink],
  templateUrl: './portal-statement.list.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalStatementList implements OnInit {
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
