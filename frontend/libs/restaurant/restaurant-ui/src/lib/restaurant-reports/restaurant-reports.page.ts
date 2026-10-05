import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  RESTAURANT_REPORTS_API,
  RestaurantReportItem,
  RestaurantSettingsService,
  hiddenReportNotes,
  reportLink,
  sortReports,
} from '@bill-book/restaurant-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * Restaurant › Reports (TK-150). The restaurant reports this person may open,
 * each on the ordinary report screen, which brings its filters, grouping and
 * the Excel and CSV export with it. A report whose switch is off in
 * Settings › Restaurant is not listed, and the page says which and why.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-reports',
  standalone: true,
  imports: [RouterLink, MessageBoxComponent],
  templateUrl: './restaurant-reports.page.html',
  styleUrl: './restaurant-reports.page.scss',
})
export class RestaurantReportsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly settingsService = inject(RestaurantSettingsService);

  protected readonly reports = signal<RestaurantReportItem[]>([]);
  protected readonly notes = signal<string[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly loading = signal(true);
  protected readonly reportLink = reportLink;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const [reports, settings] = await Promise.all([
        firstValueFrom(this.http.get<RestaurantReportItem[]>(RESTAURANT_REPORTS_API)),
        this.settingsService.load(),
      ]);
      this.reports.set(sortReports(reports));
      this.notes.set(hiddenReportNotes(settings));
    } catch (error) {
      const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
      this.messages.set([{ tone: 'error', text: message ?? 'The restaurant reports could not be loaded.' }]);
    } finally {
      this.loading.set(false);
    }
  }
}
