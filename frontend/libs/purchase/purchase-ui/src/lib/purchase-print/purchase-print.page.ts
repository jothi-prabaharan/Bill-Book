import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import {
  PurchasePrintKind,
  PurchasePrintService,
  purchaseDocumentRoute,
  purchasePrintLabel,
} from '@bill-book/purchase-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * A purchase order, goods receipt, bill or debit note, printed through the branch's print template.
 *
 * **The layout is the template's, not this page's.** Purchase builds the document's data and Printing lays
 * it out with the template the branch designed in Settings › Print templates, so there is one answer to what
 * a printed document looks like. This page shows the result and prints it. The route says which document it
 * is (`data.printKind`); the page is one for all four.
 *
 * **Printed from the frame, not the page.** The document sits in a sandboxed frame with no scripts, so
 * nothing the template holds can run; the frame is same-origin only so this page can call its print dialog,
 * which then prints the document alone rather than the app around it. A draft or voided document is stamped
 * DRAFT or VOID across every page by the server, whatever the template holds.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-purchase-print',
  standalone: true,
  imports: [MessageBoxComponent],
  templateUrl: './purchase-print.page.html',
  styleUrl: './purchase-print.page.scss',
})
export class PurchasePrintPage implements OnInit {
  private readonly printing = inject(PurchasePrintService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  protected readonly kind = signal<PurchasePrintKind>('bills');
  protected readonly documentId = signal<number | null>(null);
  protected readonly html = signal<SafeHtml | null>(null);
  protected readonly pageCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly label = computed(() => purchasePrintLabel(this.kind()));

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const kind = this.route.snapshot.data['printKind'] as PurchasePrintKind | undefined;
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!kind || !Number.isInteger(id) || id <= 0) {
      await this.router.navigate(['/purchase']);
      return;
    }

    this.kind.set(kind);
    this.documentId.set(id);

    try {
      const printed = await this.printing.print(kind, id);
      this.html.set(this.sanitizer.bypassSecurityTrustHtml(printed.html));
      this.pageCount.set(printed.pageCount);

      if (printed.unknownTags.length > 0) {
        this.messages.set([
          {
            tone: 'warning',
            text: `The print template names fields a ${this.label()} does not have. They print as nothing.`,
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

  protected print(): void {
    this.frame()?.nativeElement.contentWindow?.print();
  }

  protected async back(): Promise<void> {
    const id = this.documentId();
    await this.router.navigate(id === null ? ['/purchase'] : purchaseDocumentRoute(this.kind(), id));
  }
}
