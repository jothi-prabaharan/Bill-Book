import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { emptyQuery } from './models/report-contracts';
import { ReportQueryService } from './report-query.service';

/**
 * The export call, checked at the wire.
 *
 * **What matters is that the format reaches the server and the query goes with
 * it.** A CSV button that silently posted the default format, or that posted a
 * fresh query instead of the one the grid is showing, would download a correct
 * file of the wrong thing — which looks like a working export until somebody
 * reconciles it.
 */
describe('ReportQueryService.export', () => {
  function setup() {
    const post = vi.fn().mockReturnValue(of(new Blob(['x'])));

    TestBed.configureTestingModule({
      providers: [ReportQueryService, { provide: HttpClient, useValue: { post } }],
    });

    return { service: TestBed.inject(ReportQueryService), post };
  }

  it('asks for xlsx by default', async () => {
    const { service, post } = setup();

    await service.export('trial-balance', emptyQuery());

    expect(post.mock.calls[0][0]).toBe('/api/reports/trial-balance/export?format=Xlsx');
  });

  it('asks for csv when csv is chosen', async () => {
    const { service, post } = setup();

    await service.export('trial-balance', emptyQuery(), 'Csv');

    expect(post.mock.calls[0][0]).toBe('/api/reports/trial-balance/export?format=Csv');
  });

  it('sends the grid’s query as the body, so the file matches the screen', async () => {
    const { service, post } = setup();
    const query = {
      ...emptyQuery(),
      filters: [{ column: 'status', operator: 'Equals' as const, value: 'Posted' }],
      sorts: [{ column: 'date', direction: 'Desc' as const }],
      columns: ['date', 'total'],
    };

    await service.export('sales-register', query, 'Csv');

    expect(post.mock.calls[0][1]).toEqual(query);
  });

  it('asks for the response as a blob, not as parsed JSON', async () => {
    const { service, post } = setup();

    await service.export('trial-balance', emptyQuery(), 'Csv');

    expect(post.mock.calls[0][2]).toEqual({ responseType: 'blob' });
  });
});
