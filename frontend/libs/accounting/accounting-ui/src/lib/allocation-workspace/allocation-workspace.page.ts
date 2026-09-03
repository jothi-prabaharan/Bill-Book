import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  DateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/** As `GET /api/allocations/open-documents/{contactId}` returns them. */
interface OpenDocument {
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;
  totalAmount: number;
  allocatedAmount: number;
  unallocatedAmount: number;
  settlementStatus: 'Unallocated' | 'PartiallyPaid' | 'Paid';
}

interface OpenDocuments {
  contactId: number;
  sources: OpenDocument[];
  targets: OpenDocument[];
  totalOutstanding: number;
  totalAvailableCredit: number;
}

interface ContactSummary {
  contactId: number;
  displayName: string;
  gstin?: string | null;
  currencyCode?: string | null;
}

/** A document with the amount the user is applying against it this session. */
interface Applied extends OpenDocument {
  apply: number | null;
}

/**
 * The settlement workspace: apply a contact's available credit against what
 * they still owe, in one pass.
 *
 * <b>Why the two panels are one screen.</b> Settling is a matching problem — the
 * question is never "how much is this credit note for" on its own, it is "which
 * of these bills does it cover". Split across two screens the person doing it
 * holds the running arithmetic in their head, which is where the mistakes come
 * from; side by side the remaining balance moves as they key.
 *
 * <b>Nothing here decides what is allowed.</b> Every figure comes from the
 * server — what a document was posted for, what is already claimed, what is
 * free — and the confirm button posts one allocation per applied row and shows
 * whatever the server says about each. The page arithmetic is a preview of the
 * answer, never the authority for it: the guard that actually refuses an
 * over-allocation reads the ledger inside a serializable transaction, and no
 * amount of client-side checking can stand in for that.
 */
@Component({
  selector: 'bb-allocation-workspace-page',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    DateInputComponent,
    NumberInputComponent,
    MessageBoxComponent,
  ],
  templateUrl: './allocation-workspace.page.html',
  styleUrl: './allocation-workspace.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllocationWorkspacePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly contactId = signal<number | null>(null);
  protected readonly contact = signal<ContactSummary | null>(null);

  protected readonly sources = signal<Applied[]>([]);
  protected readonly targets = signal<Applied[]>([]);

  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  /** Defaults to today; the user may back-date into an open period. */
  protected readonly allocationDate = signal<string>(
    new Date().toISOString().slice(0, 10),
  );

  protected readonly totalOutstanding = computed(() =>
    this.targets().reduce((sum, t) => sum + t.unallocatedAmount, 0),
  );

  protected readonly totalAvailableCredit = computed(() =>
    this.sources().reduce((sum, s) => sum + s.unallocatedAmount, 0),
  );

  protected readonly appliedFromSources = computed(() =>
    this.sources().reduce((sum, s) => sum + (s.apply ?? 0), 0),
  );

  protected readonly appliedToTargets = computed(() =>
    this.targets().reduce((sum, t) => sum + (t.apply ?? 0), 0),
  );

  /** What the contact still owes once what is keyed is applied. */
  protected readonly netPayableAfter = computed(
    () => this.totalOutstanding() - this.appliedToTargets(),
  );

  /**
   * The two sides have to agree before anything can be posted: money applied to
   * bills has to come from somewhere. Shown as a figure rather than only
   * disabling the button, so the person can see which way it is out.
   */
  protected readonly difference = computed(
    () => this.appliedFromSources() - this.appliedToTargets(),
  );

  protected readonly isBalanced = computed(
    () => Math.abs(this.difference()) < 0.005,
  );

  /** Any row keyed past what its document can take. */
  protected readonly overApplied = computed(() =>
    [...this.sources(), ...this.targets()].filter(
      (row) => (row.apply ?? 0) > row.unallocatedAmount + 0.005,
    ),
  );

  protected readonly canSettle = computed(
    () =>
      !this.busy() &&
      this.appliedToTargets() > 0 &&
      this.isBalanced() &&
      this.overApplied().length === 0,
  );

  ngOnInit(): void {
    // Read once from the route rather than watched: arriving at another
    // contact is a fresh component.
    const param = this.route.snapshot.paramMap.get('contactId');
    const id = param ? Number(param) : null;

    if (id !== null && !Number.isNaN(id)) {
      this.contactId.set(id);
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    const id = this.contactId();

    if (id === null) {
      return;
    }

    this.busy.set(true);
    this.messages.set([]);

    try {
      const open = await this.req<OpenDocuments>(
        `/api/allocations/open-documents/${id}`,
      );

      this.sources.set(open.sources.map((d) => ({ ...d, apply: null })));
      this.targets.set(open.targets.map((d) => ({ ...d, apply: null })));

      // The contact card is a convenience, not the point of the screen: if the
      // name cannot be fetched the workspace still works on ids.
      try {
        this.contact.set(await this.req<ContactSummary>(`/api/contacts/${id}`));
      } catch {
        this.contact.set(null);
      }
    } catch (error) {
      this.messages.set([{ tone: 'error', text: this.describe(error) }]);
    } finally {
      this.busy.set(false);
    }
  }

  /** Puts the most this row can take into its input. */
  protected applyMax(row: Applied, side: 'source' | 'target'): void {
    this.patch(side, row.transactionTypeCode, row.transactionId, {
      apply: row.unallocatedAmount,
    });
  }

  /**
   * Fills both sides oldest-first, up to whichever runs out first. It is a
   * starting point for the common case — settle the oldest bills with whatever
   * credit is on account — not a decision: every figure stays editable.
   */
  protected autoAllocate(): void {
    let credit = this.totalAvailableCredit();

    const targets = this.targets().map((t) => {
      const take = Math.min(t.unallocatedAmount, credit);
      credit -= take;
      return { ...t, apply: take > 0 ? this.round(take) : null };
    });

    let toCover = targets.reduce((sum, t) => sum + (t.apply ?? 0), 0);

    const sources = this.sources().map((s) => {
      const give = Math.min(s.unallocatedAmount, toCover);
      toCover -= give;
      return { ...s, apply: give > 0 ? this.round(give) : null };
    });

    this.targets.set(targets);
    this.sources.set(sources);
    this.messages.set([]);
  }

  protected reset(): void {
    this.sources.set(this.sources().map((s) => ({ ...s, apply: null })));
    this.targets.set(this.targets().map((t) => ({ ...t, apply: null })));
    this.messages.set([]);
  }

  /**
   * Posts one allocation per (source, target) pair, spreading each source's
   * applied amount across the targets in order.
   *
   * <b>Each pair is its own request and its own answer.</b> The server refuses
   * per allocation — over-allocation, a raced write — so a partial success has
   * to be reportable as one: what went on, and what did not and why. Rolling
   * them into a single call would mean one failure discarding work that was
   * accepted, and the screen reloads afterwards either way so the figures shown
   * are the server's rather than the page's guess.
   */
  protected async settle(): Promise<void> {
    if (!this.canSettle()) {
      return;
    }

    this.busy.set(true);
    this.messages.set([]);

    const pairs = this.pairs();
    const failures: string[] = [];
    let applied = 0;

    for (const pair of pairs) {
      try {
        await this.post('/api/allocations', {
          sourceTransactionTypeCode: pair.sourceCode,
          sourceTransactionId: pair.sourceId,
          targetTransactionTypeCode: pair.targetCode,
          targetTransactionId: pair.targetId,
          amount: pair.amount,
          allocationDate: this.allocationDate(),
        });

        applied++;
      } catch (error) {
        failures.push(
          `${pair.sourceCode}-${pair.sourceId} → ${pair.targetCode}-${pair.targetId}: ` +
            this.describe(error),
        );
      }
    }

    this.busy.set(false);

    if (failures.length === 0) {
      this.messages.set([
        { tone: 'success', text: `${applied} allocation(s) applied.` },
      ]);
    } else {
      this.messages.set([
        {
          tone: 'error',
          text:
            `${applied} of ${pairs.length} allocation(s) were applied. ` +
            'The rest were refused:',
          detail: failures,
        },
      ]);
    }

    // Reload either way: what is free now is the server's answer, and after a
    // partial failure the page's own figures are the least trustworthy thing
    // on screen.
    await this.load();
  }

  /**
   * Turns the two columns of applied amounts into (source, target, amount)
   * triples, walking targets in order and drawing from each source until it is
   * spent. The two sides are already known to sum equal, so nothing is left over.
   */
  private pairs(): {
    sourceCode: string;
    sourceId: number;
    targetCode: string;
    targetId: number;
    amount: number;
  }[] {
    const remainingBySource = this.sources()
      .filter((s) => (s.apply ?? 0) > 0)
      .map((s) => ({ row: s, left: s.apply as number }));

    const result: {
      sourceCode: string;
      sourceId: number;
      targetCode: string;
      targetId: number;
      amount: number;
    }[] = [];

    for (const target of this.targets().filter((t) => (t.apply ?? 0) > 0)) {
      let needed = target.apply as number;

      for (const source of remainingBySource) {
        if (needed <= 0) {
          break;
        }

        if (source.left <= 0) {
          continue;
        }

        const take = this.round(Math.min(source.left, needed));

        result.push({
          sourceCode: source.row.transactionTypeCode,
          sourceId: source.row.transactionId,
          targetCode: target.transactionTypeCode,
          targetId: target.transactionId,
          amount: take,
        });

        source.left -= take;
        needed -= take;
      }
    }

    return result;
  }

  /** The status this document would be left in if what is keyed were applied. */
  protected previewStatus(row: Applied): 'Unallocated' | 'PartiallyPaid' | 'Paid' {
    const after = row.allocatedAmount + (row.apply ?? 0);

    if (after <= 0) {
      return 'Unallocated';
    }

    return after >= row.totalAmount - 0.005 ? 'Paid' : 'PartiallyPaid';
  }

  protected isOver(row: Applied): boolean {
    return (row.apply ?? 0) > row.unallocatedAmount + 0.005;
  }

  protected onApplyChange(
    side: 'source' | 'target',
    row: Applied,
    value: number | null,
  ): void {
    this.patch(side, row.transactionTypeCode, row.transactionId, { apply: value });
  }

  private patch(
    side: 'source' | 'target',
    code: string,
    id: number,
    change: Partial<Applied>,
  ): void {
    const target = side === 'source' ? this.sources : this.targets;

    target.set(
      target().map((row) =>
        row.transactionTypeCode === code && row.transactionId === id
          ? { ...row, ...change }
          : row,
      ),
    );
  }

  private round(value: number): number {
    // Money is two decimals everywhere in this product; a third would be
    // refused by the server anyway.
    return Math.round(value * 100) / 100;
  }

  private async req<T>(url: string): Promise<T> {
    return await new Promise<T>((resolve, reject) => {
      this.http.get<T>(url).subscribe({ next: resolve, error: reject });
    });
  }

  private async post<T>(url: string, body: unknown): Promise<T> {
    return await new Promise<T>((resolve, reject) => {
      this.http.post<T>(url, body).subscribe({ next: resolve, error: reject });
    });
  }

  /**
   * The server's own words where there are any. Every refusal in this product
   * carries a message the user can act on, and paraphrasing it into "request
   * failed" throws away the only useful part.
   */
  private describe(error: unknown): string {
    const body = (error as { error?: { message?: string } } | null)?.error;

    if (body?.message) {
      return body.message;
    }

    const status = (error as { status?: number } | null)?.status;

    return status === 0
      ? 'The server could not be reached.'
      : `The request was refused${status ? ` (${status})` : ''}.`;
  }
}
