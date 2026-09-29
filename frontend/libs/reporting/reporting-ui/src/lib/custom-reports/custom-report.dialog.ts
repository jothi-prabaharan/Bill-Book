import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomReport, CustomReportService, ReportQuery } from '@bill-book/reporting-core';
import {
  CheckboxComponent,
  MessageBoxComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

/**
 * Saves the layout on screen as a custom report (TK-113): a new one from a
 * dataset, or changes to one already open. Sharing with the branch needs
 * `reports.edit`, which the server enforces and explains.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-custom-report-dialog',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
  ],
  templateUrl: './custom-report.dialog.html',
  styleUrl: './custom-report.dialog.scss',
})
export class CustomReportDialog implements OnInit {
  private readonly reports = inject(CustomReportService);

  readonly datasetKey = input.required<string>();

  /** The layout a save captures. */
  readonly layout = input.required<ReportQuery>();

  /** The report being changed, or null to save a new one. */
  readonly existing = input<CustomReport | null>(null);

  readonly saved = output<CustomReport>();
  readonly closed = output<void>();

  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected title = '';
  protected description = '';
  protected shareWithBranch = false;
  protected saveAsNew = false;

  ngOnInit(): void {
    const existing = this.existing();

    if (existing) {
      this.title = existing.title;
      this.description = existing.description ?? '';
      this.shareWithBranch = existing.isShared;
    }
  }

  protected get editing(): boolean {
    return !!this.existing()?.mayEdit && !this.saveAsNew;
  }

  async save(): Promise<void> {
    if (this.title.trim() === '') {
      return;
    }

    this.busy.set(true);
    this.message.set(null);

    const draft = {
      datasetKey: this.datasetKey(),
      title: this.title.trim(),
      description: this.description.trim() === '' ? null : this.description.trim(),
      isShared: this.shareWithBranch,
      layout: this.layout(),
    };

    try {
      const existing = this.existing();
      const report = this.editing && existing
        ? await this.reports.update(existing.customReportId, draft)
        : await this.reports.create(draft);

      this.saved.emit(report);
      this.closed.emit();
    } catch (error: unknown) {
      const body = (error as { error?: { message?: string; detail?: string } } | null)?.error;
      this.message.set(body?.message ?? body?.detail ?? 'Could not save this report.');
    } finally {
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.closed.emit();
  }
}
