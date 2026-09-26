import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PaymentChoice, PortalInvoiceItem, paymentTotal } from '../retail/portal.models';

interface PayRow extends PaymentChoice {
  documentNo: string;
}

/**
 * Pay online (TK-98): choose open invoices and how much of each, and anything
 * extra on account, then go to the gateway's checkout. What happened is read
 * back from the server afterwards, never taken from the gateway's redirect.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-pay',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink],
  templateUrl: './portal-pay.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalPayPage implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);

  protected readonly rows = signal<PayRow[]>([]);
  protected readonly extra = signal(0);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  protected readonly total = computed(() => paymentTotal(this.rows(), this.extra() || 0));

  ngOnInit(): void {
    void this.load();
  }

  protected toggle(row: PayRow, selected: boolean): void {
    this.rows.update(rows => rows.map(r => (r === row ? { ...r, selected } : r)));
  }

  protected setAmount(row: PayRow, amount: number): void {
    this.rows.update(rows => rows.map(r => (r === row ? { ...r, amount: Number(amount) } : r)));
  }

  protected async pay(): Promise<void> {
    const total = this.total();
    if (total === null) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const invoices = this.rows()
        .filter(r => r.selected)
        .map(r => ({ invoiceId: r.invoiceId, amount: r.amount }));
      const started = await this.api.startPayment(invoices, total);

      // The sandbox checks out inside the portal; a real gateway's page is elsewhere.
      if (started.checkoutUrl.startsWith('/')) {
        await this.router.navigateByUrl(started.checkoutUrl);
      } else {
        window.location.assign(started.checkoutUrl);
      }
    } catch (err: unknown) {
      const message = err instanceof HttpErrorResponse ? (err.error as { message?: string } | null)?.message : undefined;
      this.error.set(message ?? 'The payment could not be started. Try again in a moment.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const invoices = await this.api.invoices();
      this.rows.set(
        invoices
          .filter((i: PortalInvoiceItem) => i.status !== 'Paid' && i.status !== 'Void' && (i.outstandingAmount ?? 0) > 0)
          .map(i => ({
            invoiceId: i.invoiceId,
            documentNo: i.documentNo,
            outstanding: i.outstandingAmount ?? 0,
            selected: false,
            amount: i.outstandingAmount ?? 0,
          })),
      );
    } catch {
      this.error.set('Your invoices could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
