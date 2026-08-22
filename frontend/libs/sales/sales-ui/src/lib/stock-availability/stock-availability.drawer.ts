import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { StockAvailability } from '@bill-book/sales-core';

/**
 * What the items on this document have, hold, and can still promise.
 *
 * **Three numbers rather than one, because one of them misleads on its own.**
 * Reserved is never subtracted from on hand — the goods are physically there and
 * still worth what they cost, only their availability has changed. A screen
 * showing on-hand where available belongs promises the same unit twice; one
 * showing available where on-hand belongs tells somebody the shelf is empty when
 * it is full. So the drawer shows both and the difference between them.
 *
 * **It is a snapshot and says so.** Another till can confirm the last unit while
 * this is open. What actually decides is the guarded reservation taken when the
 * order is confirmed, which no stale screen can slip past; this exists so a
 * refusal is rare, not so it is impossible.
 */
@Component({
  selector: 'bb-stock-availability-drawer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stock-availability.drawer.html',
  styleUrl: './stock-availability.drawer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockAvailabilityDrawerComponent {
  readonly rows = input<readonly StockAvailability[]>([]);

  /** What the document is asking for, by item, so a shortfall can be named. */
  readonly wanted = input<ReadonlyMap<number, number>>(new Map());

  readonly loading = input(false);

  readonly dismiss = output<void>();

  /** Stocked items only. A service line has nothing to report and no shortfall. */
  protected readonly tracked = computed(() => this.rows().filter((r) => r.isTracked));

  protected readonly untrackedCount = computed(
    () => this.rows().length - this.tracked().length,
  );

  /** Short by how much, or zero. Drives both the row's tone and the summary. */
  protected shortfall(row: StockAvailability): number {
    const want = this.wanted().get(row.itemId) ?? 0;
    return Math.max(0, want - row.quantityAvailable);
  }

  protected wantedOf(row: StockAvailability): number {
    return this.wanted().get(row.itemId) ?? 0;
  }

  protected readonly shortCount = computed(
    () => this.tracked().filter((r) => this.shortfall(r) > 0).length,
  );
}
