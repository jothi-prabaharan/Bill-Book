import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { RatesService } from './rates.service';

/** The admin app's rate page talks to Master's `api/rates` (TK-24). */
describe('RatesService', () => {
  let service: RatesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(RatesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the exchange history with a limit', async () => {
    const pending = service.exchangeHistory(50);
    const request = http.expectOne((r) => r.url === '/api/rates/exchange/history');
    expect(request.request.params.get('take')).toBe('50');
    request.flush([]);
    expect(await pending).toEqual([]);
  });

  it('saves a manual exchange rate with PUT', async () => {
    const pending = service.saveExchange({ fromCurrencyCode: 'USD', toCurrencyCode: 'INR', rateDate: '2026-09-24', rate: 83.1 });
    const request = http.expectOne('/api/rates/exchange');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.rate).toBe(83.1);
    request.flush(null);
    await pending;
  });

  it('removes a manual metal rate by id', async () => {
    const pending = service.deleteMetal(7);
    const request = http.expectOne('/api/rates/metal/7');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    await pending;
  });
});
