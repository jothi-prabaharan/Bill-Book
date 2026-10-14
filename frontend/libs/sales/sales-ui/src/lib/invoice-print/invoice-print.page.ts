import { ChangeDetectionStrategy } from '@angular/core';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { InvoiceService, InvoiceView } from '@bill-book/sales-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * An invoice, printed through the branch's print template.
 *
 * **The layout is the template's, not this page's.** Sales builds the invoice's
 * data and Printing lays it out with the template the branch designed in
 * Settings › Print templates, so a change to the template changes every printed
 * invoice, and there is one answer to what an invoice looks like rather than a
 * page and a template disagreeing. This page shows the result and prints it.
 *
 * **Printed from the frame, not the page.** The document sits in a sandboxed
 * frame with no scripts, so nothing the template holds can run; the frame is
 * same-origin only so this page can call its print dialog, which then prints
 * the document alone rather than the app around it.
 *
 * A draft or voided invoice is not a tax invoice. Sales asks Printing to stamp
 * PROFORMA or VOID across every page, whatever the template holds, and this
 * page says so above the document as well.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-print',
  standalone: true,
  imports: [MessageBoxComponent],
  templateUrl: './invoice-print.page.html',
  styleUrl: './invoice-print.page.scss',
})
export class InvoicePrintPage implements OnInit {
  private readonly invoices = inject(InvoiceService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  protected readonly invoice = signal<InvoiceView | null>(null);
  protected readonly html = signal<SafeHtml | null>(null);
  protected readonly pageCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly downloading = signal(false);

  /** All invoices (draft, posted, void) can return a backend PDF. */
  protected readonly hasArchive = computed(() => {
    return this.invoice() !== null;
  });

  protected readonly isProforma = computed(() => {
    const status = this.invoice()?.status;
    return status !== undefined && status !== 'Posted';
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
      const [invoice, printed] = await Promise.all([
        this.invoices.get(Number(id)),
        this.invoices.print(Number(id)),
      ]);

      this.invoice.set(invoice);
      // Trusted rather than sanitised again: Printing sanitised every band of
      // the template, the invoice's own text is escaped before it is merged,
      // and the frame runs no scripts. Angular's sanitiser would strip the page
      // styles the layout depends on and print something else.
      this.html.set(this.sanitizer.bypassSecurityTrustHtml(printed.html));
      this.pageCount.set(printed.pageCount);

      if (printed.unknownTags.length > 0) {
        this.messages.set([
          {
            tone: 'warning',
            text: 'The print template names fields an invoice does not have. They print as nothing.',
            detail: printed.unknownTags,
          },
        ]);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loading.set(false);
    }
  }

  /** Saves the PDF archived when the invoice posted (TK-22). */
  protected async downloadPdf(): Promise<void> {
    const invoice = this.invoice();
    if (!invoice) {
      return;
    }

    this.downloading.set(true);
    try {
      const blob = await this.invoices.downloadPdf(invoice.invoiceId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = InvoiceService.pdfFileName(invoice.documentNo);
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.downloading.set(false);
    }
  }

  protected async print(): Promise<void> {
    const invoice = this.invoice();
    if (invoice) {
      this.downloading.set(true);
      try {
        const blob = await this.invoices.downloadPdf(invoice.invoiceId);
        const url = URL.createObjectURL(blob);
        const iframe = document.createElement('iframe');
        iframe.style.position = 'fixed';
        iframe.style.right = '0';
        iframe.style.bottom = '0';
        iframe.style.width = '0';
        iframe.style.height = '0';
        iframe.style.border = '0';
        iframe.src = url;
        document.body.appendChild(iframe);
        iframe.onload = () => {
          setTimeout(() => {
            iframe.contentWindow?.focus();
            iframe.contentWindow?.print();
            setTimeout(() => {
              iframe.remove();
              URL.revokeObjectURL(url);
            }, 60_000);
          }, 300);
        };
        return;
      } catch {
        // Fall back to HTML frame print
      } finally {
        this.downloading.set(false);
      }
    }
    this.frame()?.nativeElement.contentWindow?.print();
  }

  protected async back(): Promise<void> {
    const invoice = this.invoice();

    await this.router.navigate(
      invoice ? ['/sales/invoices', invoice.invoiceId] : ['/sales/invoices'],
    );
  }
}
