import { ChangeDetectionStrategy } from '@angular/core';
import { TextInputComponent } from '@bill-book/ui-components';
import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ReportQuery, SavedView, SavedViewService } from '@bill-book/reporting-core';

/**
 * Saved layouts: open one, save the current one, or remove one.
 *
 * **Saving is how a report stops being re-configured every morning.** These
 * reports carry eight to forty columns; without a saved view, the person who wants
 * six of them picks those six every single time.
 *
 * Branch-wide views are the other half — a standard layout somebody publishes so
 * the whole branch reads the same report the same way. Publishing needs
 * `reports.edit`, and the server enforces that rather than this dialog.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-saved-view-dialog',
  standalone: true,
  imports: [FormsModule, TextInputComponent],
  templateUrl: './saved-view.dialog.html',
  styleUrl: './saved-view.dialog.scss',
})
export class SavedViewDialog implements OnInit {
  private readonly views = inject(SavedViewService);

  readonly reportKey = input.required<string>();

  /** The layout a save would capture. */
  readonly layout = input.required<ReportQuery>();

  readonly applied = output<ReportQuery>();
  readonly closed = output<void>();

  protected readonly saved = signal<SavedView[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected name = '';
  protected shareWithBranch = false;
  protected makeDefault = false;

  ngOnInit(): void {
    // Fire and forget, matching the other pages: the lifecycle hook returns void,
    // and a Promise handed back to Angular is one nobody awaits or catches.
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      this.saved.set(await this.views.list(this.reportKey()));
    } catch {
      this.message.set('Could not load saved views.');
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.name.trim() === '') {
      return;
    }

    this.busy.set(true);
    this.message.set(null);

    try {
      await this.views.create(this.reportKey(), this.name.trim(), this.layout(), {
        isShared: this.shareWithBranch,
        isDefault: this.makeDefault,
      });

      this.name = '';
      this.shareWithBranch = false;
      this.makeDefault = false;

      await this.load();
    } catch (error: unknown) {
      // The server refuses a branch-wide view without reports.edit, and says so
      // in words worth showing rather than replacing.
      this.message.set(this.serverMessage(error) ?? 'Could not save this view.');
      this.busy.set(false);
    }
  }

  protected apply(view: SavedView): void {
    this.applied.emit(view.layout);
    this.closed.emit();
  }

  protected async remove(view: SavedView): Promise<void> {
    this.busy.set(true);

    try {
      await this.views.remove(this.reportKey(), view.reportViewId);
      await this.load();
    } catch {
      this.message.set('Could not remove that view.');
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.closed.emit();
  }

  private serverMessage(error: unknown): string | null {
    const body = (error as { error?: { message?: string } } | null)?.error;

    return typeof body?.message === 'string' ? body.message : null;
  }
}

