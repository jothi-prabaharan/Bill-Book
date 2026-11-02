import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { TRANSACTION_KINDS } from './transaction-kinds';
import { TransactionViewService } from './transaction-view.service';

describe('TransactionViewService', () => {
  let service: TransactionViewService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(TransactionViewService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it.each(TRANSACTION_KINDS.map((info) => [info.kind, `${info.api}/7/view`] as const))(
    'TXV-30: asks for the %s view at %s',
    async (kind, url) => {
      const pending = service.view(kind, 7);
      http.expectOne(url).flush({
        html: '<p>x</p>',
        pageCount: 1,
        unknownTags: [],
        printTemplateId: null,
        documentNo: 'X-0007',
        documentTypeCode: 'XXX',
        status: 'Posted',
      });

      expect(await pending).toMatchObject({ html: '<p>x</p>', pageCount: 1, documentNo: 'X-0007', status: 'Posted' });
    },
  );

  it('TXV-31: a refusal from the server reaches the caller as an error, not as an empty page', async () => {
    const pending = service.view('bills', 7);
    http.expectOne('/api/purchase/bills/7/view').flush('nope', { status: 404, statusText: 'Not Found' });

    await expect(pending).rejects.toMatchObject({ status: 404 });
  });
});
