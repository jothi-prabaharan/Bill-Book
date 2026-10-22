import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlanLimit } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import { UpgradeDialogService } from '@bill-book/ui-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { planLimitInterceptor } from './plan-limit.interceptor';

const sales: PlanLimit = {
  message:
    'Professional plan Sales transaction limit reached (maximum 1,000 Sales transactions this month). Upgrade to the Elite Plan for unlimited transactions.',
  code: 'PlanLimitReached',
  kind: 'SalesTransactions',
  plan: 'Professional',
  limit: 1000,
  used: 1000,
};

/**
 * One interceptor opens the upgrade prompt for a refused document (TK-210), so no
 * form has handling of its own, and the form still gets the refusal.
 */
describe('planLimitInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let open: ReturnType<typeof vi.fn>;
  let permissions: Set<string>;

  beforeEach(() => {
    open = vi.fn().mockResolvedValue(undefined);
    permissions = new Set(['settings.edit']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([planLimitInterceptor])),
        provideHttpClientTesting(),
        { provide: UpgradeDialogService, useValue: { open } },
        { provide: SessionContextService, useValue: { has: (permission: string) => permissions.has(permission) } },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  async function refused(body: unknown, status = 409): Promise<unknown> {
    const failure = new Promise<unknown>((resolve) => http.post('/api/sales/invoices', {}).subscribe({ error: resolve }));
    controller.expectOne('/api/sales/invoices').flush(body as object, { status, statusText: 'Refused' });
    return failure;
  }

  it('PLIM-01: a document refused at the limit opens the prompt with the limit, and the form still gets the refusal', async () => {
    const error = (await refused(sales)) as { status: number; error: PlanLimit };

    expect(open).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalledWith({ limit: sales, canRequest: true });
    expect(error.status).toBe(409);
    expect(error.error.message).toBe(sales.message);
  });

  it('PLIM-02: Purchase documents open it too', async () => {
    await refused({ ...sales, kind: 'PurchaseTransactions' });

    expect(open).toHaveBeenCalledTimes(1);
  });

  it('PLIM-03: someone who may not change settings is not offered the request', async () => {
    permissions.delete('settings.edit');

    await refused(sales);

    expect(open).toHaveBeenCalledWith({ limit: sales, canRequest: false });
  });

  it('PLIM-04: a limit on users is left to the Users screen', async () => {
    await refused({ ...sales, kind: 'Users', limit: 5, used: 5 });

    expect(open).not.toHaveBeenCalled();
  });

  it('PLIM-05: no other refusal opens it', async () => {
    await refused({ message: 'That document has already been posted.' });
    await refused({ message: 'Nothing here.' }, 404);
    await refused({ ...sales }, 400);

    expect(open).not.toHaveBeenCalled();
  });
});
