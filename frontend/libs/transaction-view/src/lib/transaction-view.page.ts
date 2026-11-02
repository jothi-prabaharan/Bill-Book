import { Location, NgClass } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { AuthService } from '@bill-book/auth';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';
import {
  TransactionKindInfo,
  transactionDocumentRoute,
  transactionKindInfo,
  transactionListRoute,
} from './transaction-kinds';
import {
  DESK_COLOUR,
  TransactionView,
  clampZoom,
  documentHeading,
  fitScale,
  isEditableStatus,
  nextZoom,
  statusLabel,
  statusNotice,
  statusTone,
  viewerDocument,
  zoomPercent,
} from './transaction-view.model';
import { TransactionViewService } from './transaction-view.service';

/**
 * Any transaction, laid out the way its print template lays it out and shown like a PDF (TK-263). One page
 * for the quote, sales order, delivery challan, invoice and credit note, the purchase order, goods receipt,
 * bill and debit note, the receipt and payment, and the journal voucher; the route says which
 * (`data.viewKind`) and the id is in the path.
 *
 * **The layout is the template's, not this page's.** The document's own service builds its data and Printing
 * lays it out with the template the branch designed in Settings › Print templates, so a change to the
 * template changes how every document reads here, and the paper on the screen is the paper that prints.
 *
 * **Set on a desk and scaled by the frame.** The pages sit one under another on grey, fitted to the width of
 * the screen (a phone shows the whole page, a wide screen shows it at its own size), and the + and − buttons
 * zoom as a PDF viewer's do. The scaling is done to the frame from outside, never inside the document, so
 * printing the frame prints the pages at their own size.
 *
 * **Read-only, at the view permission.** Print needs the module's print permission, Edit its edit permission
 * and a document that can still be edited; everyone who can read the document can read this.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-transaction-view',
  standalone: true,
  imports: [NgClass, MessageBoxComponent],
  templateUrl: './transaction-view.page.html',
  styleUrl: './transaction-view.page.scss',
})
export class TransactionViewPage {
  private readonly views = inject(TransactionViewService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly auth = inject(AuthService);
  private readonly sanitizer = inject(DomSanitizer);

  private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');
  private readonly viewer = viewChild<ElementRef<HTMLElement>>('viewer');

  protected readonly info = signal<TransactionKindInfo | null>(null);
  protected readonly documentId = signal<number | null>(null);
  protected readonly view = signal<TransactionView | null>(null);
  protected readonly srcdoc = signal<SafeHtml | null>(null);
  protected readonly loading = signal(true);
  protected readonly messages = signal<UiMessage[]>([]);

  /** The paper's own size in pixels, known once the document has loaded in its frame. */
  protected readonly paper = signal<{ width: number; height: number } | null>(null);

  /** The width the paper has to lie in, in pixels. */
  private readonly available = signal(0);

  /** The scale a person chose with + and −; null while the paper is fitted to the screen. */
  protected readonly zoom = signal<number | null>(null);

  protected readonly desk = DESK_COLOUR;
  protected readonly tone = statusTone;
  protected readonly statusText = statusLabel;

  protected readonly scale = computed(() => {
    const chosen = this.zoom();
    if (chosen !== null) {
      return clampZoom(chosen);
    }

    const paper = this.paper();
    return paper ? fitScale(this.available(), paper.width) : 1;
  });

  protected readonly percent = computed(() => zoomPercent(this.scale()));
  protected readonly fitting = computed(() => this.zoom() === null);

  protected readonly label = computed(() => this.info()?.label ?? 'document');
  protected readonly heading = computed(() => documentHeading(this.label(), this.view()?.documentNo));
  protected readonly status = computed(() => this.view()?.status ?? null);
  protected readonly notice = computed(() => statusNotice(this.label(), this.status()));

  protected readonly pages = computed(() => {
    const count = this.view()?.pageCount ?? 0;
    return count === 1 ? '1 page' : `${count} pages`;
  });

  /** Handing a document over is the module's print permission; reading it is not. */
  protected readonly canPrint = computed(() => {
    const info = this.info();
    return info !== null && this.auth.has(`${info.module}.print`);
  });

  /** Edit needs the module's edit permission and a document that has not been posted. */
  protected readonly canEdit = computed(() => {
    const info = this.info();
    return info !== null && isEditableStatus(this.status()) && this.auth.has(`${info.module}.edit`);
  });

  constructor() {
    // The same page serves every document, so a move from one to another re-reads rather than re-creates.
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => void this.load());

    // Keeps the room the paper has up to date as the window, or the panel around it, changes size.
    effect((onCleanup) => {
      const element = this.viewer()?.nativeElement;
      if (!element || typeof ResizeObserver === 'undefined') {
        return;
      }

      const measure = () => this.available.set(element.clientWidth);
      measure();

      const observer = new ResizeObserver(measure);
      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });
  }

  private async load(): Promise<void> {
    const info = transactionKindInfo(this.route.snapshot.data['viewKind'] as string | undefined);
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!info || !Number.isInteger(id) || id <= 0) {
      await this.router.navigate(info ? transactionListRoute(info.kind) : ['/']);
      return;
    }

    this.info.set(info);
    this.documentId.set(id);
    this.loading.set(true);
    this.view.set(null);
    this.srcdoc.set(null);
    this.paper.set(null);
    this.messages.set([]);

    try {
      const view = await this.views.view(info.kind, id);
      this.view.set(view);
      this.srcdoc.set(this.sanitizer.bypassSecurityTrustHtml(viewerDocument(view.html)));

      if (view.unknownTags.length > 0) {
        this.messages.set([
          {
            tone: 'warning',
            text: `The print template names fields a ${info.label} does not have. They print as nothing.`,
            detail: view.unknownTags,
          },
        ]);
      }
    } catch (error) {
      this.messages.set([this.failure(error, info)]);
    } finally {
      this.loading.set(false);
    }
  }

  private failure(error: unknown, info: TransactionKindInfo): UiMessage {
    // Another branch's document and one that does not exist are one answer (decided 23 September 2026).
    if (error instanceof HttpErrorResponse && error.status === 404) {
      return { tone: 'error', text: `That ${info.label} was not found in this branch.` };
    }

    const failure = readApiFailure(error);
    return { tone: 'error', text: failure.text, detail: failure.detail };
  }

  /** Measures the document once it is in its frame, so the frame can be given the paper's own size. */
  protected onFrameLoad(): void {
    const frame = this.frame()?.nativeElement;
    const document = frame?.contentDocument;
    if (!document?.body) {
      return;
    }

    const sheet = document.querySelector<HTMLElement>('.pt-doc');
    const width = Math.ceil(sheet?.getBoundingClientRect().width || document.documentElement.scrollWidth);
    const height = Math.ceil(document.documentElement.scrollHeight);

    if (width > 0 && height > 0) {
      this.paper.set({ width, height });
    }
  }

  protected zoomIn(): void {
    this.zoom.set(nextZoom(this.scale(), 1));
  }

  protected zoomOut(): void {
    this.zoom.set(nextZoom(this.scale(), -1));
  }

  protected fit(): void {
    this.zoom.set(null);
  }

  /** Prints the pages and nothing around them: the frame is the document, at its own size. */
  protected print(): void {
    const frame = this.frame()?.nativeElement.contentWindow;
    frame?.focus();
    frame?.print();
  }

  /** The document's own screen: where a draft is edited and a posted one is voided, reversed or allocated. */
  protected async open(): Promise<void> {
    const info = this.info();
    const id = this.documentId();
    if (info && id !== null) {
      await this.router.navigate(transactionDocumentRoute(info.kind, id));
    }
  }

  /** Back to wherever the person came from, or to the register when the page was opened directly. */
  protected async back(): Promise<void> {
    if (typeof window !== 'undefined' && window.history.length > 1) {
      this.location.back();
      return;
    }

    const info = this.info();
    await this.router.navigate(info ? transactionListRoute(info.kind) : ['/']);
  }
}
