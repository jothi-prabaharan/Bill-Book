import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { emptyQuery } from './models/report-contracts';
import { ReportScheduleService, describeSchedule } from './report-schedule.service';

describe('ReportScheduleService (TK-114)', () => {
  function setup() {
    const request = vi.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [ReportScheduleService, { provide: HttpClient, useValue: { request } }],
    });

    return { service: TestBed.inject(ReportScheduleService), request };
  }

  it('lists one report’s schedules by its key', async () => {
    const { service, request } = setup();

    await service.list('dataset-sales-lines');

    expect(request.mock.calls[0][1]).toBe('/api/reports/schedules?reportKey=dataset-sales-lines');
  });

  it('saves a weekly schedule with its recipients and layout', async () => {
    const { service, request } = setup();

    await service.create({
      reportKey: 'trial-balance',
      customReportId: null,
      title: 'Weekly trial balance',
      frequency: 'Weekly',
      dayOfWeek: 'Monday',
      dayOfMonth: null,
      sendAtHour: 8,
      timeZoneId: null,
      recipientUserIds: ['u1'],
      isActive: true,
      layout: emptyQuery(),
    });

    expect(request.mock.calls[0][0]).toBe('POST');
    expect(request.mock.calls[0][2].body.recipientUserIds).toEqual(['u1']);
  });
});

describe('describeSchedule', () => {
  it('reads as a sentence for each frequency', () => {
    expect(describeSchedule({ frequency: 'Daily', dayOfWeek: null, dayOfMonth: null, sendAtHour: 7 })).toBe('Every day at 07:00');
    expect(describeSchedule({ frequency: 'Weekly', dayOfWeek: 'Friday', dayOfMonth: null, sendAtHour: 18 })).toBe('Every Friday at 18:00');
    expect(describeSchedule({ frequency: 'Monthly', dayOfWeek: null, dayOfMonth: 1, sendAtHour: 9 })).toBe('On day 1 of every month at 09:00');
  });
});
