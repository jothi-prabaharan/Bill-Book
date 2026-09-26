import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalInvoiceItem, statusLabel, statusTone } from '../retail/portal.models';

/** The contact's invoices (TK-95): posted and voided, newest first, one card each. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-invoices',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './portal-invoices.list.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalInvoicesList implements OnInit {
  private readonly api = inject(PortalApi);

  protected readonly invoices = signal<PortalInvoiceItem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.invoices.set(await this.api.invoices());
    } catch {
      this.error.set('Your invoices could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
