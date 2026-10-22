import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { comparePlans } from '@bill-book/api-client';
import { DataGridComponent } from '../data-grid/data-grid.component';
import { TextareaComponent } from '../forms/textarea/textarea.component';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { canAskForUpgrade, comparisonColumns, upgradeMessages, upgradeTitle } from './upgrade-dialog.model';
import { UpgradeDialogService } from './upgrade-dialog.service';

/**
 * The prompt `UpgradeDialogService` opens: the limit that was met, what the
 * plan allows against what Elite does, and a request button. Mounted once, in
 * the app root, beside the confirm dialog.
 *
 * The frame is the global dialog; the words and the figures come from the
 * server through `PlanService`, so nothing here knows a limit's number.
 */
@Component({
  selector: 'bb-upgrade-dialog',
  standalone: true,
  imports: [DataGridComponent, FormsModule, MessageBoxComponent, TextareaComponent],
  templateUrl: './upgrade-dialog.component.html',
  styleUrl: './upgrade-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown.escape)': 'close()',
  },
})
export class UpgradeDialogComponent {
  private readonly service = inject(UpgradeDialogService);

  protected readonly state = this.service.state;
  protected readonly note = signal('');

  protected readonly title = upgradeTitle;
  protected readonly messages = upgradeMessages;
  protected readonly canAsk = canAskForUpgrade;

  /** The grid's columns and rows, worked out once per plan read rather than on every change detection. */
  protected readonly columns = computed(() => {
    const summary = this.state()?.summary;
    return summary ? comparisonColumns(summary) : [];
  });

  protected readonly rows = computed(() => {
    const summary = this.state()?.summary;
    return summary ? comparePlans(summary) : [];
  });

  protected close(): void {
    this.service.close();
    this.note.set('');
  }

  protected request(): void {
    void this.service.request(this.note());
  }
}
