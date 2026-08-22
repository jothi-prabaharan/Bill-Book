import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { SalesListComponent } from './sales-list.component';
import { TransactionService, SalesTransactionListItem } from '@bill-book/sales-core';

describe('SalesListComponent (sales/sales-ui/sales-list)', () => {
  let mockTransactionService: {
    list: ReturnType<typeof vi.fn>;
  };
  let mockRouter: Partial<Router>;

  const sampleTransactions: SalesTransactionListItem[] = [
    {
      transactionId: 101,
      contactId: 1,
      documentDate: '2026-08-18',
      transactionType: 'Invoice',
      documentNo: 'INV-2026-0001',
      contactName: 'Acme Traders',
      totalAmount: 14500.50,
      status: 'Posted'
    },
    {
      transactionId: 102,
      contactId: 2,
      documentDate: '2026-08-17',
      transactionType: 'Quote',
      documentNo: 'QOT-2026-0001',
      contactName: 'Global Enterprises',
      totalAmount: 32000.00,
      status: 'Draft'
    },
    {
      transactionId: 103,
      contactId: 3,
      documentDate: '2026-08-16',
      transactionType: 'SalesOrder',
      documentNo: 'SOR-2026-0001',
      contactName: 'Zenith Retail',
      totalAmount: 5600.75,
      status: 'Approved'
    },
    {
      transactionId: 104,
      contactId: 1,
      documentDate: '2026-08-15',
      transactionType: 'CreditNote',
      documentNo: 'CRN-2026-0001',
      contactName: 'Acme Traders',
      totalAmount: 1200.00,
      status: 'Posted'
    }
  ];

  beforeEach(() => {
    mockTransactionService = {
      list: vi.fn().mockReturnValue(of(sampleTransactions))
    };

    mockRouter = {
      navigate: vi.fn().mockResolvedValue(true)
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: TransactionService, useValue: mockTransactionService },
        { provide: Router, useValue: mockRouter }
      ]
    });
  });

  const createComponent = (): SalesListComponent => {
    const comp = TestBed.runInInjectionContext(() => new SalesListComponent());
    return comp;
  };

  describe('Tier 1: Feature Coverage (R4 Specification)', () => {
    it('SLIST-T1-01: Component instantiates and loads transactions on ngOnInit', () => {
      const comp = createComponent();
      comp.ngOnInit();

      expect(mockTransactionService.list).toHaveBeenCalledWith('');
      expect(comp.transactions.length).toBe(4);
      expect(comp.transactions).toEqual(sampleTransactions);
    });

    it('SLIST-T1-02: Column definitions define 6 columns with proper alignment', () => {
      const comp = createComponent();
      const cols = comp.columns;

      expect(cols.length).toBe(6);
      expect(cols.map(c => c.field)).toEqual([
        'documentDate',
        'transactionType',
        'documentNo',
        'contactName',
        'totalAmount',
        'status'
      ]);

      const amountCol = cols.find(c => c.field === 'totalAmount');
      expect(amountCol?.align).toBe('right');
    });

    it('SLIST-T1-03: setType updates selectedType and re-queries transaction list', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const invoiceItems = sampleTransactions.filter(t => t.transactionType === 'Invoice');
      mockTransactionService.list.mockReturnValue(of(invoiceItems));

      comp.setType('Invoice');
      expect(comp.selectedType).toBe('Invoice');
      expect(mockTransactionService.list).toHaveBeenCalledWith('Invoice');
      expect(comp.transactions.length).toBe(1);
    });

    it('SLIST-T1-04: onTypeChange refreshes list using current selectedType', () => {
      const comp = createComponent();
      comp.selectedType = 'Quote';
      comp.onTypeChange();

      expect(mockTransactionService.list).toHaveBeenCalledWith('Quote');
    });

    it('SLIST-T1-05: Route resolver maps transaction types to appropriate form paths', () => {
      const comp = createComponent();

      expect(comp.getRouteForTransaction(sampleTransactions[0])).toBe('/sales/invoices/101');
      expect(comp.getRouteForTransaction(sampleTransactions[1])).toBe('/sales/quotes/102');
      expect(comp.getRouteForTransaction(sampleTransactions[2])).toBe('/sales/sales-orders/103');
      expect(comp.getRouteForTransaction(sampleTransactions[3])).toBe('/sales/credit-notes/104');
      expect(comp.getRouteForTransaction({ transactionId: 105, transactionType: 'DeliveryChallan' } as any)).toBe('/sales/delivery-challans/105');
    });

    it('SLIST-T1-06: navigateToTransaction triggers router navigation with resolved route', () => {
      const comp = createComponent();
      comp.navigateToTransaction(sampleTransactions[0]);

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/invoices/101']);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('SLIST-T2-01: Unknown or unhandled transaction type falls back to base /sales route', () => {
      const comp = createComponent();
      const unknownTx: SalesTransactionListItem = {
        transactionId: 999,
        contactId: 99,
        documentDate: '2026-08-01',
        transactionType: 'CustomProforma' as any,
        documentNo: 'PRF-001',
        contactName: 'Test Contact',
        totalAmount: 100,
        status: 'Draft'
      };

      expect(comp.getRouteForTransaction(unknownTx)).toBe('/sales');
    });

    it('SLIST-T2-02: Empty transaction list initializes properly without runtime errors', () => {
      mockTransactionService.list.mockReturnValue(of([]));
      const comp = createComponent();
      comp.ngOnInit();

      expect(comp.transactions).toEqual([]);
      expect(comp.columns.length).toBe(6);
    });

    it('SLIST-T2-03: Switching between document types sequentially loads respective data subsets', () => {
      const comp = createComponent();
      comp.ngOnInit();

      comp.setType('Quote');
      expect(mockTransactionService.list).toHaveBeenCalledWith('Quote');

      comp.setType('SalesOrder');
      expect(mockTransactionService.list).toHaveBeenCalledWith('SalesOrder');

      comp.setType('CreditNote');
      expect(mockTransactionService.list).toHaveBeenCalledWith('CreditNote');

      comp.setType('');
      expect(mockTransactionService.list).toHaveBeenCalledWith('');
    });
  });

  describe('Tier 3: Cross-Feature Interactions & Navigation', () => {
    it('SLIST-T3-01: Consecutive navigation calls for different transaction types resolve accurately', () => {
      const comp = createComponent();
      comp.navigateToTransaction(sampleTransactions[1]); // Quote
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/quotes/102']);

      comp.navigateToTransaction(sampleTransactions[2]); // SalesOrder
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/sales-orders/103']);

      comp.navigateToTransaction(sampleTransactions[3]); // CreditNote
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/credit-notes/104']);
    });
  });

  describe('Tier 4: Real-World Sales Register Workflow', () => {
    it('SLIST-T4-01: Complete sales list workflow: init -> filter by Invoice -> drill down into transaction', () => {
      const comp = createComponent();
      comp.ngOnInit();
      expect(comp.transactions.length).toBe(4);

      // User filters for Invoices only
      const invoiceData = [sampleTransactions[0]];
      mockTransactionService.list.mockReturnValue(of(invoiceData));
      comp.setType('Invoice');

      expect(comp.selectedType).toBe('Invoice');
      expect(comp.transactions.length).toBe(1);
      expect(comp.transactions[0].documentNo).toBe('INV-2026-0001');

      // User clicks row
      comp.navigateToTransaction(comp.transactions[0]);
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/invoices/101']);
    });
  });
});
