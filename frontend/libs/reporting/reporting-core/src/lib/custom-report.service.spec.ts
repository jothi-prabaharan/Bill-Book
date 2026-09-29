import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { CustomReportService, catalogLink } from './custom-report.service';
import { emptyQuery } from './models/report-contracts';

describe('CustomReportService (TK-113)', () => {
  function setup() {
    const request = vi.fn().mockReturnValue(of({}));

    TestBed.configureTestingModule({
      providers: [CustomReportService, { provide: HttpClient, useValue: { request } }],
    });

    return { service: TestBed.inject(CustomReportService), request };
  }

  it('saves a new report with its dataset and layout', async () => {
    const { service, request } = setup();
    const layout = { ...emptyQuery(), columns: ['custom.salesperson', 'lineTotalBase'], groupBy: ['custom.salesperson'] };

    await service.create({
      datasetKey: 'dataset-sales-lines',
      title: 'Sales by salesperson',
      description: null,
      isShared: false,
      layout,
    });

    expect(request.mock.calls[0][0]).toBe('POST');
    expect(request.mock.calls[0][1]).toBe('/api/reports/custom');
    expect(request.mock.calls[0][2].body.layout.groupBy).toEqual(['custom.salesperson']);
  });

  it('reads, updates and removes by id', async () => {
    const { service, request } = setup();

    await service.get(7);
    await service.update(7, { datasetKey: 'dataset-items', title: 'x', description: null, isShared: true, layout: emptyQuery() });
    await service.remove(7);

    expect(request.mock.calls.map((c) => `${c[0]} ${c[1]}`)).toEqual([
      'GET /api/reports/custom/7',
      'PUT /api/reports/custom/7',
      'DELETE /api/reports/custom/7',
    ]);
  });
});

describe('catalogLink', () => {
  it('opens a custom report as its dataset with the report id', () => {
    expect(catalogLink({ reportKey: 'dataset-sales-lines', customReportId: 12 })).toEqual({
      path: ['/reports', 'dataset-sales-lines'],
      queryParams: { custom: 12 },
    });
  });

  it('opens a standard report by its key alone', () => {
    expect(catalogLink({ reportKey: 'trial-balance' })).toEqual({
      path: ['/reports', 'trial-balance'],
      queryParams: null,
    });
  });
});
