import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { amountInWords } from '@bill-book/currency-format';
import { OrganizationService, OrganizationSummary } from '@bill-book/master-core';
import { InvoiceService, InvoiceView } from '@bill-book/sales-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * A printable tax invoice.
 *
 * **A browser print view, not the archival PDF.** The house rule is that
 * standard documents are rendered server-side to PDF/A and archived to blob
 * storage against `SourceType` + `SourceId`; that needs a PDF library, and the
 * one this project intends to use is licensed and not yet installed. This page
 * is the half that does not need it — a correct layout the browser can print or
 * save as PDF today — and it is also the layout the server-side renderer should
 * reproduce when it lands, so the two do not end up disagreeing about what an
 * invoice looks like.
 *
 * **What makes it a tax invoice rather than a pretty page.** An Indian tax
 * invoice has required content, and the parts that are easy to leave off are the
 * ones that matter: both GSTINs, the place of supply, HSN per line, the tax
 * split shown per rate, and the total in words. The words are there to
 * corroborate the figure — which is why they are computed from the same rounded
 * amount rather than formatted separately.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-print',
  standalone: true,
  imports: [CommonModule, MessageBoxComponent],
  templateUrl: './invoice-print.page.html',
  styleUrl: './invoice-print.page.scss',
})
export class InvoicePrintPage implements OnInit {
  private readonly invoices = inject(InvoiceService);
  private readonly organizations = inject(OrganizationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly invoice = signal<InvoiceView | null>(null);
  protected readonly seller = signal<OrganizationSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly messages = signal<UiMessage[]>([]);

  /**
   * Only a posted invoice is a tax invoice.
   *
   * A draft can still be printed — somebody proofreads it before posting — but
   * it prints as a **proforma**, watermarked, because a draft handed to a
   * customer as a tax invoice is a document they may try to claim credit on.
   */
  protected readonly isProforma = computed(() => this.invoice()?.status !== 'Posted');

  protected readonly isVoid = computed(() => this.invoice()?.status === 'Void');

  protected readonly title = computed(() => {
    if (this.isVoid()) return 'Voided invoice';
    return this.isProforma() ? 'Proforma invoice' : 'Tax invoice';
  });

  /** The figure the words have to agree with, rounded exactly once. */
  protected readonly totalInWords = computed(() => {
    const invoice = this.invoice();
    if (!invoice) {
      return '';
    }

    return amountInWords(invoice.totalAmount, currencyWord(invoice.currencyCode));
  });

  /**
   * The tax broken out per rate, which is how a return reads it.
   *
   * A single "CGST 9%" line is wrong the moment an invoice mixes slabs — a
   * jeweller's bill routinely carries 3% bullion beside 18% making charges — so
   * this groups the line taxes by component and rate rather than summing them
   * into one figure per component.
   */
  protected readonly taxBreakdown = computed(() => {
    const invoice = this.invoice();
    if (!invoice) {
      return [];
    }

    const buckets = new Map<string, { component: string; rate: number; taxable: number; amount: number }>();

    for (const line of invoice.lines) {
      for (const tax of line.taxes) {
        const key = `${tax.taxComponent}|${tax.rate}`;
        const bucket = buckets.get(key) ?? {
          component: tax.taxComponent,
          rate: tax.rate,
          taxable: 0,
          amount: 0,
        };

        bucket.taxable += tax.taxableAmount;
        bucket.amount += tax.amount;
        buckets.set(key, bucket);
      }
    }

    return [...buckets.values()].sort(
      (a, b) => a.component.localeCompare(b.component) || a.rate - b.rate,
    );
  });

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');

    if (!id || id === 'new') {
      await this.router.navigate(['/sales/invoices']);
      return;
    }

    try {
      // Both at once: neither depends on the other, and a printed page waiting
      // on two sequential round trips is a page somebody prints twice.
      const [invoice, seller] = await Promise.all([
        this.invoices.get(Number(id)),
        this.organizations.get(),
      ]);

      this.invoice.set(invoice);
      this.seller.set(seller);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loading.set(false);
    }
  }

  protected print(): void {
    window.print();
  }

  protected async back(): Promise<void> {
    const invoice = this.invoice();

    await this.router.navigate(
      invoice ? ['/sales/invoices', invoice.invoiceId] : ['/sales/invoices'],
    );
  }
}

/**
 * What to call the currency in words.
 *
 * Only the ones a branch here plausibly bills in; anything else falls back to
 * the code itself, which reads oddly but is never wrong.
 */
function currencyWord(code: string): string {
  switch (code) {
    case 'INR':
      return 'Rupees';
    case 'USD':
      return 'Dollars';
    case 'EUR':
      return 'Euros';
    case 'GBP':
      return 'Pounds';
    case 'AED':
      return 'Dirhams';
    default:
      return code;
  }
}

