import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AggregateFunction,
  ReportColumn,
  ReportPivot,
} from '@bill-book/reporting-core';

/**
 * Rows down the side, columns across the top, measures in the cells.
 *
 * **A pivot replaces the report rather than decorating it.** Grouping and paging
 * mean nothing to a matrix, so turning one on changes what comes back entirely —
 * which is why it is a panel that says what it is doing rather than a toggle
 * tucked beside the filters.
 *
 * Not offered below the tablet breakpoint: a matrix has no card form, and
 * shrinking one until it fits produces something nobody can read.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-pivot-panel',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './pivot-panel.component.html',
  styleUrl: './pivot-panel.component.scss',
})
export class PivotPanelComponent {
  readonly columns = input.required<readonly ReportColumn[]>();
  readonly pivot = input.required<ReportPivot | null>();

  readonly pivotChange = output<ReportPivot | null>();

  protected readonly aggregates: AggregateFunction[] = ['Sum', 'Count', 'Min', 'Max', 'Avg'];

  /** Only text columns can be an axis — the server groups on a concatenated key. */
  protected readonly axisColumns = computed(() =>
    this.columns().filter((column) => column.isGroupable),
  );

  /** Only columns the report says are worth aggregating can be a measure. */
  protected readonly valueColumns = computed(() =>
    this.columns().filter((column) => column.aggregate !== 'None'),
  );

  protected readonly active = computed(() => this.pivot() !== null);

  protected readonly canPivot = computed(
    () => this.axisColumns().length > 0 && this.valueColumns().length > 0,
  );

  protected header(key: string): string {
    return this.columns().find((column) => column.key === key)?.header ?? key;
  }

  /**
   * Turning it on picks the first sensible axis and measure rather than opening
   * empty. An empty pivot renders nothing and reads as a broken screen.
   */
  protected toggle(on: boolean): void {
    if (!on) {
      this.pivotChange.emit(null);
      return;
    }

    this.pivotChange.emit({
      rows: [this.axisColumns()[0].key],
      columns: [],
      values: [{ column: this.valueColumns()[0].key, aggregate: 'Sum' }],
    });
  }

  protected setRow(key: string): void {
    this.emit({ rows: key ? [key] : [] });
  }

  protected setColumn(key: string): void {
    this.emit({ columns: key ? [key] : [] });
  }

  protected setValue(key: string): void {
    const current = this.pivot()?.values[0];

    this.emit({ values: [{ column: key, aggregate: current?.aggregate ?? 'Sum' }] });
  }

  protected setAggregate(aggregate: AggregateFunction): void {
    const current = this.pivot()?.values[0];

    if (current) {
      this.emit({ values: [{ column: current.column, aggregate }] });
    }
  }

  private emit(change: Partial<ReportPivot>): void {
    const current = this.pivot();

    if (current) {
      this.pivotChange.emit({ ...current, ...change });
    }
  }
}

