import { ChangeDetectionStrategy } from '@angular/core';
import {
  CdkDragDrop,
  DragDropModule,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ReportColumn } from '@bill-book/reporting-core';

/**
 * Which columns a report shows, and in what order.
 *
 * **Search is not optional here.** Fixed Assets Schedule offers thirty-three
 * columns and Account Transaction twenty-nine; nobody scrolls a list that long
 * hunting for *Residual Value*.
 *
 * Selected columns sit at the top in their display order and reorder by drag —
 * the same CDK idiom the numbering-series screen uses, so the gesture is one the
 * product already teaches.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-column-chooser',
  standalone: true,
  imports: [FormsModule, DragDropModule],
  templateUrl: './column-chooser.dialog.html',
  styleUrl: './column-chooser.dialog.scss',
})
export class ColumnChooserDialog {
  readonly available = input.required<readonly ReportColumn[]>();

  /** The chosen keys, in display order. */
  readonly selected = input.required<readonly string[]>();

  readonly selectedChange = output<string[]>();
  readonly closed = output<void>();

  protected readonly search = signal('');

  /** The chosen columns, in the order they will appear. */
  protected readonly chosen = computed(() => {
    const byKey = new Map(this.available().map((column) => [column.key, column]));

    return this.selected()
      .map((key) => byKey.get(key))
      .filter((column): column is ReportColumn => column !== undefined);
  });

  /** Everything not chosen, filtered by the search box. */
  protected readonly rest = computed(() => {
    const chosen = new Set(this.selected());
    const needle = this.search().trim().toLowerCase();

    return this.available()
      .filter((column) => !chosen.has(column.key))
      .filter((column) => needle === '' || column.header.toLowerCase().includes(needle));
  });

  protected add(column: ReportColumn): void {
    this.selectedChange.emit([...this.selected(), column.key]);
  }

  protected remove(key: string): void {
    // A report with no columns renders an empty grid and looks broken, so the
    // last one cannot be removed.
    if (this.selected().length <= 1) {
      return;
    }

    this.selectedChange.emit(this.selected().filter((chosen) => chosen !== key));
  }

  protected reorder(event: CdkDragDrop<string[]>): void {
    const order = [...this.selected()];
    moveItemInArray(order, event.previousIndex, event.currentIndex);

    this.selectedChange.emit(order);
  }

  protected close(): void {
    this.closed.emit();
  }
}

