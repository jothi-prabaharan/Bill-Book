import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FilterOperator, ReportColumn, ReportFilter } from '@bill-book/reporting-core';
import { arityOf, describeOperator, inputTypeFor, operatorsFor } from './filter-operators';

/**
 * The applied filters, as chips, and the editor for adding another.
 *
 * **Chips rather than a form that stays open.** A filter you cannot see is a
 * filter you forget, and a report showing fewer rows than expected because of one
 * somebody set last week is how people come to distrust the numbers. Each chip
 * clears on its own.
 *
 * Filters combine with AND. There is no OR and no bracketing — the screens this
 * replaces had neither, and a filter language nobody asked for is a parser nobody
 * maintains.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-filter-bar',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './filter-bar.component.html',
  styleUrl: './filter-bar.component.scss',
})
export class FilterBarComponent {
  readonly columns = input.required<readonly ReportColumn[]>();
  readonly filters = input.required<readonly ReportFilter[]>();

  readonly filtersChange = output<ReportFilter[]>();

  protected readonly adding = signal(false);
  protected readonly draftColumn = signal('');
  protected readonly draftOperator = signal<FilterOperator>('Contains');
  protected readonly draftValue = signal('');
  protected readonly draftValue2 = signal('');

  protected readonly filterable = computed(() =>
    this.columns().filter((column) => column.isFilterable),
  );

  protected readonly column = computed(() =>
    this.columns().find((candidate) => candidate.key === this.draftColumn()),
  );

  protected readonly operators = computed(() => {
    const column = this.column();

    return column ? operatorsFor(column.dataType) : [];
  });

  protected readonly arity = computed(() => arityOf(this.draftOperator()));

  protected readonly inputType = computed(() => {
    const column = this.column();

    return column ? inputTypeFor(column.dataType) : 'text';
  });

  protected readonly canApply = computed(() => {
    if (!this.column()) {
      return false;
    }

    switch (this.arity()) {
      case 'none':
        return true;
      case 'two':
        return this.draftValue() !== '' && this.draftValue2() !== '';
      default:
        return this.draftValue() !== '';
    }
  });

  protected open(): void {
    const first = this.filterable()[0];

    this.draftColumn.set(first?.key ?? '');
    this.draftOperator.set(first ? operatorsFor(first.dataType)[0] : 'Contains');
    this.draftValue.set('');
    this.draftValue2.set('');
    this.adding.set(true);
  }

  /**
   * Changing the column resets the operator, because the operators are not the
   * same set — leaving `Contains` selected after switching to a date column would
   * offer a filter the server refuses.
   */
  protected onColumnChange(key: string): void {
    this.draftColumn.set(key);

    const column = this.columns().find((candidate) => candidate.key === key);
    this.draftOperator.set(column ? operatorsFor(column.dataType)[0] : 'Contains');
    this.draftValue.set('');
    this.draftValue2.set('');
  }

  protected apply(): void {
    if (!this.canApply()) {
      return;
    }

    const filter: ReportFilter = {
      column: this.draftColumn(),
      operator: this.draftOperator(),
    };

    if (this.arity() === 'list') {
      // A comma-separated list is what people type. Splitting it here keeps the
      // request shape honest rather than sending one string the server would have
      // to guess at.
      filter.values = this.draftValue()
        .split(',')
        .map((value) => value.trim())
        .filter((value) => value !== '');
    } else if (this.arity() !== 'none') {
      filter.value = this.draftValue();

      if (this.arity() === 'two') {
        filter.value2 = this.draftValue2();
      }
    }

    this.filtersChange.emit([...this.filters(), filter]);
    this.adding.set(false);
  }

  protected removeAt(index: number): void {
    this.filtersChange.emit(this.filters().filter((_, at) => at !== index));
  }

  protected clearAll(): void {
    this.filtersChange.emit([]);
  }

  /** What a chip reads: "Account contains Cash". */
  protected describe(filter: ReportFilter): string {
    const column = this.columns().find((candidate) => candidate.key === filter.column);
    const header = column?.header ?? filter.column;
    const operator = describeOperator(filter.operator);

    switch (arityOf(filter.operator)) {
      case 'none':
        return `${header} ${operator}`;
      case 'two':
        return `${header} ${operator} ${filter.value} and ${filter.value2}`;
      case 'list':
        return `${header} ${operator} ${(filter.values ?? []).join(', ')}`;
      default:
        return `${header} ${operator} ${filter.value}`;
    }
  }
}

