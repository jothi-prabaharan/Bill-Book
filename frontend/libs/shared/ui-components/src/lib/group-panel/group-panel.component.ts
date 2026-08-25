import { ChangeDetectionStrategy } from '@angular/core';
import {
  CdkDragDrop,
  DragDropModule,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { Component, computed, input, output } from '@angular/core';
import { ReportColumn } from '@bill-book/reporting-core';

/** Three levels, and no more. Past that the footers outnumber the rows. */
const MAX_LEVELS = 3;

/**
 * What the report is grouped by, outermost level first.
 *
 * **Only text columns can be grouped**, and the server enforces it: grouping runs
 * in SQL on a concatenated key, and concatenating a date asks Postgres to render
 * it in a format nobody chose. A report that wants grouping by account type
 * exposes the type's *name* as a column — which is what the group header wanted
 * to show anyway. This panel therefore offers what the metadata marks groupable
 * and nothing else.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-group-panel',
  standalone: true,
  imports: [DragDropModule],
  templateUrl: './group-panel.component.html',
  styleUrl: './group-panel.component.scss',
})
export class GroupPanelComponent {
  readonly columns = input.required<readonly ReportColumn[]>();
  readonly groupBy = input.required<readonly string[]>();

  readonly groupByChange = output<string[]>();

  protected readonly maxLevels = MAX_LEVELS;

  /** The grouping levels, outermost first. */
  protected readonly levels = computed(() => {
    const byKey = new Map(this.columns().map((column) => [column.key, column]));

    return this.groupBy()
      .map((key) => byKey.get(key))
      .filter((column): column is ReportColumn => column !== undefined);
  });

  /** Groupable columns not already used as a level. */
  protected readonly available = computed(() => {
    const used = new Set(this.groupBy());

    return this.columns().filter((column) => column.isGroupable && !used.has(column.key));
  });

  protected readonly isFull = computed(() => this.groupBy().length >= MAX_LEVELS);

  protected add(key: string): void {
    if (this.isFull() || key === '') {
      return;
    }

    this.groupByChange.emit([...this.groupBy(), key]);
  }

  protected removeAt(index: number): void {
    this.groupByChange.emit(this.groupBy().filter((_, at) => at !== index));
  }

  protected clear(): void {
    this.groupByChange.emit([]);
  }

  /**
   * Reordering the levels changes what the report means, not just how it looks —
   * accounts within types reads differently from types within accounts — so it is
   * a drag rather than something that happens by accident.
   */
  protected reorder(event: CdkDragDrop<string[]>): void {
    const order = [...this.groupBy()];
    moveItemInArray(order, event.previousIndex, event.currentIndex);

    this.groupByChange.emit(order);
  }
}

