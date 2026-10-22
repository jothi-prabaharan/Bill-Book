import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { PlanService, QuotaBadge, TransactionKind, quotaBadge, quotaKindOf } from '@bill-book/api-client';

/**
 * What is left of the month's documents, beside a register's title (TK-210).
 *
 * A Professional or trial branch may create 1,000 Sales documents and 1,000
 * Purchase documents a month, counted apart, and this says how many of the
 * register's side are left: "Sales: 88 of 1,000 left". It turns to a warning when
 * a fifth is left and to a danger when none is. A branch with no limit, a
 * document that is not counted and a plan that could not be read show nothing: a
 * convenience is not worth an error on a register.
 *
 * It reads the document's code, which every register already gives its heading,
 * so no register needs to know it is there.
 */
@Component({
  selector: 'bb-quota-badge',
  standalone: true,
  templateUrl: './quota-badge.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuotaBadgeComponent {
  /** The register's document code: `QTE`, `SOR`, `INV`, `CRN`, `POR`, `GRN`, `BIL` or `DBN`. Any other shows nothing. */
  readonly code = input.required<string>();

  private readonly plan = inject(PlanService);

  protected readonly badge = signal<QuotaBadge | null>(null);

  /** The latest read, so a slow answer for a register left behind cannot overwrite the one now showing. */
  private latest = 0;

  constructor() {
    effect(() => {
      const kind = quotaKindOf(this.code());
      untracked(() => void this.load(kind));
    });
  }

  private async load(kind: TransactionKind | null): Promise<void> {
    const read = ++this.latest;
    if (kind === null) {
      this.badge.set(null);
      return;
    }

    let next: QuotaBadge | null = null;
    try {
      next = quotaBadge(await this.plan.usage(kind));
    } catch {
      next = null;
    }

    if (read === this.latest) {
      this.badge.set(next);
    }
  }
}
