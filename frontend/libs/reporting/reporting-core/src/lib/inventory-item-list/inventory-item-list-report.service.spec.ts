import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { InventoryItemListReportDto, InventoryItemListReportRequest } from './inventory-item-list-report.models';
import { InventoryItemListReportService } from './inventory-item-list-report.service';

function setup(answer: unknown = { items: [], totalItems: 0 }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [InventoryItemListReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(InventoryItemListReportService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

describe('InventoryItemListReportService', () => {
  it('sends asOfDate and optional filters in query params', async () => {
    const mockReport: InventoryItemListReportDto = {
      asOfDate: '2026-10-28',
      baseCurrencyCode: 'INR',
      items: [],
      totalItems: 0,
      pageNumber: 1,
      pageSize: 25,
      totals: {
        totalQuantityOnHand: 0,
        totalValue: 0,
        totalQuantityOnOrder: 0,
        totalQuantityCommitted: 0,
        totalBalanceQty: 0,
        costPending: false,
      },
    };

    const { service, get } = setup(mockReport);

    const request: InventoryItemListReportRequest = {
      asOfDate: '2026-10-28',
      itemCategoryId: 42,
      status: 'Active',
      itemType: 'Inventory',
      onlyWithStock: true,
      search: 'Steel',
      pageNumber: 2,
      pageSize: 50,
    };

    const result = await service.getReport(request);

    expect(result).toBe(mockReport);
    expect(get).toHaveBeenCalledOnce();

    const [url] = get.mock.calls[0];
    expect(url).toContain('/api/statements/inventory-item-list');

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asOfDate')).toBe('2026-10-28');
    expect(params.get('itemCategoryId')).toBe('42');
    expect(params.get('status')).toBe('Active');
    expect(params.get('itemType')).toBe('Inventory');
    expect(params.get('onlyWithStock')).toBe('true');
    expect(params.get('search')).toBe('Steel');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
  });

  it('fetches categories from sub-route', async () => {
    const mockCategories = [{ itemCategoryId: 1, categoryName: 'Hardware' }];
    const { service, get } = setup(mockCategories);

    const result = await service.getCategories();
    expect(result).toEqual(mockCategories);
    expect(get.mock.calls[0][0]).toContain('/api/statements/inventory-item-list/item-categories');
  });
});
