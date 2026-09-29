import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ReportRecipient, ReportSchedule } from './models/report-contracts';

/** What saving a schedule sends. The owner and the next run are the server's. */
export type ReportScheduleDraft = Omit<
  ReportSchedule,
  'reportScheduleId' | 'nextRunAt' | 'lastRunAt' | 'lastStatus' | 'lastMessage'
>;

/** Report schedules (TK-114): a report emailed as an Excel workbook daily, weekly or monthly. */
@Injectable({ providedIn: 'root' })
export class ReportScheduleService {
  private readonly http = inject(HttpClient);

  /** This user's schedules for one report. */
  async list(reportKey: string): Promise<ReportSchedule[]> {
    return this.req<ReportSchedule[]>('GET', `/api/reports/schedules?reportKey=${encodeURIComponent(reportKey)}`);
  }

  /** The branch's users a schedule can be sent to. */
  async recipients(): Promise<ReportRecipient[]> {
    return this.req<ReportRecipient[]>('GET', '/api/reports/schedules/recipients');
  }

  async create(draft: ReportScheduleDraft): Promise<ReportSchedule> {
    return this.req<ReportSchedule>('POST', '/api/reports/schedules', draft);
  }

  async update(id: number, draft: ReportScheduleDraft): Promise<ReportSchedule> {
    return this.req<ReportSchedule>('PUT', `/api/reports/schedules/${id}`, draft);
  }

  async remove(id: number): Promise<void> {
    await this.req<void>('DELETE', `/api/reports/schedules/${id}`);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}

/** "Every Monday at 08:00", for a schedule's list entry. */
export function describeSchedule(s: Pick<ReportSchedule, 'frequency' | 'dayOfWeek' | 'dayOfMonth' | 'sendAtHour'>): string {
  const at = `${String(s.sendAtHour).padStart(2, '0')}:00`;

  switch (s.frequency) {
    case 'Daily':
      return `Every day at ${at}`;
    case 'Weekly':
      return `Every ${s.dayOfWeek ?? 'Monday'} at ${at}`;
    case 'Monthly':
      return `On day ${s.dayOfMonth ?? 1} of every month at ${at}`;
  }
}
