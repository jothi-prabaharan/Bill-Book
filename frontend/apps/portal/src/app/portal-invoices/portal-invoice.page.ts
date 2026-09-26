import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalInvoiceDetail, statusLabel, statusTone } from '../retail/portal.models';

/** One invoice (TK-95): its lines and totals, and the archived PDF to download. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-invoice',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './portal-invoice.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalInvoicePage implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly detail = signal<PortalInvoiceDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly downloading = signal(false);
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;

  ngOnInit(): void {
    void this.load();
  }

  protected async download(): Promise<void> {
    const detail = this.detail();
    if (!detail) return;
    this.downloading.set(true);
    try {
      const blob = await this.api.invoicePdf(detail.invoice.invoiceId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${detail.invoice.documentNo.replace(/[\\/]/g, '-')}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set('The PDF could not be downloaded. Try again in a moment.');
    } finally {
      this.downloading.set(false);
    }
  }

  private async load(): Promise<void> {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    try {
      this.detail.set(await this.api.invoice(id));
    } catch {
      this.error.set('That invoice could not be found.');
    }
  }
}
