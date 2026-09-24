import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { describe, expect, it, vi } from 'vitest';
import { InvoiceService, SalesOrderListItem, SalesOrderService } from '@bill-book/sales-core';
import { OrderToInvoiceDialogComponent } from './order-to-invoice.dialog';

/** The dialog's members are protected; the test reaches them by a declared shape. */
interface OrderToInvoiceHarness {
  load(): Promise<void>;
  candidates(): SalesOrderListItem[];
}

const order = (id: number, isFullyInvoiced: boolean, invoicedDocumentId?: number): SalesOrderListItem =>
  ({
    salesOrderId: id,
    documentNo: `SO/2026/${id}`,
    documentDate: '2026-08-18',
    fulfilmentStatus: 'PartlyDelivered',
    contactId: 5,
    currencyCode: 'INR',
    taxableAmount: 1000,
    totalAmount: 1180,
    status: 'Posted',
    isInterState: false,
    invoicedDocumentId,
    isFullyInvoiced,
  }) as SalesOrderListItem;

describe('OrderToInvoiceDialogComponent', () => {
  it('offers every confirmed order with something left to bill, including one billed in part', async () => {
    const orders = {
      list: vi.fn().mockResolvedValue({
        total: 3,
        skip: 0,
        take: 50,
        rows: [
          order(1, false),
          // Billed in part: it has an invoice and still owes one. The dialog
          // used to hide any order with an invoice at all.
          order(2, false, 901),
          order(3, true, 902),
        ],
      }),
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: SalesOrderService, useValue: orders },
        { provide: InvoiceService, useValue: {} },
      ],
    });

    const dialog = TestBed.runInInjectionContext(
      () => new OrderToInvoiceDialogComponent(),
    ) as unknown as OrderToInvoiceHarness;

    await dialog.load();

    expect(dialog.candidates().map((o) => o.salesOrderId)).toEqual([1, 2]);
  });
});
