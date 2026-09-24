import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { readApiFailure } from '@bill-book/api-client';
import { APP_LABELS, AppId, IfCanDirective } from '@bill-book/auth';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';
import { firstValueFrom } from 'rxjs';

/** One app's row, from `GET api/applications`. */
export interface ApplicationRow {
  app: AppId;
  licensed: boolean;
  licenseType: string | null;
  expiryDate: string | null;
  isActive: boolean;
}

/** What a row says about its licence, for the status column. */
export function applicationStatus(row: ApplicationRow, today: string): string {
  if (!row.licensed) {
    return 'Not started';
  }
  if (!row.isActive) {
    return 'Suspended';
  }
  if (row.expiryDate !== null && row.expiryDate < today) {
    return 'Expired';
  }
  return row.licenseType === 'Trial' ? 'Trial' : 'Active';
}

/**
 * Settings › Applications (H0.3, TK-44): the four apps and this customer's
 * licence for each, with **Start trial** on an app it does not hold yet.
 * Shared by every app, like every settings page.
 *
 * Starting a trial is `POST api/applications/{app}/trial` (TK-45). It grants
 * the signed-in owner that app's Owner role in every branch and seeds the app.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-applications-page',
  standalone: true,
  imports: [IfCanDirective, MessageBoxComponent],
  templateUrl: './applications.page.html',
  styleUrl: './applications.page.scss',
})
export class ApplicationsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<ApplicationRow[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal<AppId | null>(null);
  protected readonly labels = APP_LABELS;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected status(row: ApplicationRow): string {
    return applicationStatus(row, this.today);
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async startTrial(app: AppId): Promise<void> {
    this.busy.set(app);
    try {
      await firstValueFrom(this.http.post(`/api/applications/${app}/trial`, {}));
      this.messages.set([{ tone: 'success', text: `${APP_LABELS[app]} trial started. It is ready in every branch.` }]);
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(null);
    }
  }

  private async load(): Promise<void> {
    try {
      this.rows.set(await firstValueFrom(this.http.get<ApplicationRow[]>('/api/applications')));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }
}
