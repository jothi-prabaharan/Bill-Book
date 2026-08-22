import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { DataGridComponent } from './data-grid.component';
import { DataGridService } from './data-grid.service';
import { ColumnDef, GridState } from './data-grid.models';

describe('DataGridComponent (shared/ui-components/data-grid)', () => {
  let mockStateService: {
    loadState: ReturnType<typeof vi.fn>;
    saveState: ReturnType<typeof vi.fn>;
  };

  const sampleColumns: ColumnDef[] = [
    { field: 'id', header: 'ID', width: '60px' },
    { field: 'docNo', header: 'Doc Number' },
    { field: 'customer', header: 'Customer Name' },
    { field: 'amount', header: 'Amount', align: 'right', dataType: 'number' },
    { field: 'status', header: 'Status' },
    { field: 'hiddenField', header: 'Hidden', visible: false }
  ];

  const sampleData = [
    { id: 1, docNo: 'INV-001', customer: 'Acme Traders', amount: 15000, status: 'Posted' },
    { id: 2, docNo: 'INV-002', customer: 'Global Mart', amount: 25000, status: 'Draft' },
    { id: 3, docNo: 'INV-003', customer: 'Acme Supermarket', amount: 8500, status: 'Posted' },
    { id: 4, docNo: 'INV-004', customer: 'Zenith Retail', amount: 42000, status: 'Void' },
    { id: 5, docNo: 'INV-005', customer: 'Global Exports', amount: 67000, status: 'Posted' },
  ];

  beforeEach(() => {
    mockStateService = {
      loadState: vi.fn().mockReturnValue(null),
      saveState: vi.fn()
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: DataGridService, useValue: mockStateService }
      ]
    });
  });

  const createComponent = (): DataGridComponent => {
    const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
    comp.gridCode = 'sales_invoices_grid';
    comp.columns = [...sampleColumns];
    comp.data = [...sampleData];
    comp.ngOnInit();
    return comp;
  };

  describe('Tier 1: Feature / Contract Coverage (R3 Specification)', () => {
    it('GRID-T1-01: Initializes visibleColumns omitting columns where visible=false', () => {
      const comp = createComponent();
      const visible = comp.visibleColumns();

      expect(visible.length).toBe(5);
      expect(visible.map(c => c.field)).toEqual(['id', 'docNo', 'customer', 'amount', 'status']);
      expect(visible.find(c => c.field === 'hiddenField')).toBeUndefined();
    });

    it('GRID-T1-02: Returns all data rows by default when no filters are active', () => {
      const comp = createComponent();
      expect(comp.filteredData().length).toBe(5);
      expect(comp.activeFilters().length).toBe(0);
    });

    it('GRID-T1-03: Row click emits row item via rowClick EventEmitter', () => {
      const comp = createComponent();
      const spy = vi.fn();
      comp.rowClick.subscribe(spy);

      const targetRow = sampleData[1];
      comp.onRowClick(targetRow);

      expect(spy).toHaveBeenCalledTimes(1);
      expect(spy).toHaveBeenCalledWith(targetRow);
    });

    it('GRID-T1-04: Filter toggle opens filter popup with default "contains" operator', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.toggleFilter('customer', mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalled();
      expect(comp.openFilterField()).toBe('customer');
      expect(comp.filterOp()).toBe('contains');
      expect(comp.filterVal()).toBe('');

      // Toggle again closes
      comp.toggleFilter('customer', mockEvent);
      expect(comp.openFilterField()).toBeNull();
    });

    it('GRID-T1-05: Applying "contains" filter narrows down filteredData', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.toggleFilter('customer', mockEvent);
      comp.filterVal.set('Global');
      comp.filterOp.set('contains');
      comp.applyFilter('customer', mockEvent);

      expect(comp.openFilterField()).toBeNull();
      expect(comp.activeFilters()).toEqual([
        { field: 'customer', operator: 'contains', value: 'Global' }
      ]);

      const filtered = comp.filteredData();
      expect(filtered.length).toBe(2);
      expect(filtered.map(r => r.docNo)).toEqual(['INV-002', 'INV-005']);
    });

    it('GRID-T1-06: Clearing an active filter restores dataset', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.filterVal.set('Acme');
      comp.applyFilter('customer', mockEvent);
      expect(comp.filteredData().length).toBe(2);

      comp.clearFilter('customer', mockEvent);
      expect(comp.activeFilters().length).toBe(0);
      expect(comp.filteredData().length).toBe(5);
    });
  });

  describe('Tier 2: Boundary & Filter Operator Edge Cases', () => {
    it('GRID-T2-01: "equals" operator matches exact string ignoring case', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.filterVal.set('posted');
      comp.filterOp.set('equals');
      comp.applyFilter('status', mockEvent);

      const filtered = comp.filteredData();
      expect(filtered.length).toBe(3);
      expect(filtered.every(r => r.status === 'Posted')).toBe(true);
    });

    it('GRID-T2-02: "starts" operator matches prefix of cell content', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.filterVal.set('INV-00');
      comp.filterOp.set('starts');
      comp.applyFilter('docNo', mockEvent);
      expect(comp.filteredData().length).toBe(5);

      comp.filterVal.set('INV-004');
      comp.applyFilter('docNo', mockEvent);
      expect(comp.filteredData().length).toBe(1);
      expect(comp.filteredData()[0].docNo).toBe('INV-004');
    });

    it('GRID-T2-03: Empty dataset data=[] handles filtering and computed without errors', () => {
      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.gridCode = 'empty_grid';
      comp.columns = sampleColumns;
      comp.data = [];
      comp.ngOnInit();

      expect(comp.filteredData()).toEqual([]);

      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      comp.filterVal.set('test');
      comp.applyFilter('customer', mockEvent);
      expect(comp.filteredData()).toEqual([]);
    });

    it('GRID-T2-04: Null or undefined row values handled gracefully during filtering', () => {
      const comp = createComponent();
      comp.data = [
        { id: 1, docNo: 'INV-001', customer: null, status: 'Draft' },
        { id: 2, docNo: 'INV-002', customer: undefined, status: 'Draft' },
        { id: 3, docNo: 'INV-003', customer: 'Valid Customer', status: 'Draft' }
      ];

      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      comp.filterVal.set('Valid');
      comp.applyFilter('customer', mockEvent);

      expect(comp.filteredData().length).toBe(1);
      expect(comp.filteredData()[0].id).toBe(3);
    });

    it('GRID-T2-05: Multi-column filters apply conjunction (AND logic)', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // Filter 1: Customer contains Acme
      comp.filterVal.set('Acme');
      comp.filterOp.set('contains');
      comp.applyFilter('customer', mockEvent);

      // Filter 2: Status equals Posted
      comp.filterVal.set('Posted');
      comp.filterOp.set('equals');
      comp.applyFilter('status', mockEvent);

      expect(comp.activeFilters().length).toBe(2);
      expect(comp.filteredData().map(r => r.docNo)).toEqual(['INV-001', 'INV-003']);
    });

    it('GRID-T2-06: Special regex metacharacters in filter queries are treated literally', () => {
      const comp = createComponent();
      comp.data = [
        { id: 1, docNo: 'INV-001 (A)', customer: 'Acme [Special]', status: 'Posted' },
        { id: 2, docNo: 'INV-002', customer: 'Normal Corp', status: 'Draft' }
      ];

      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      comp.filterVal.set('[Special]');
      comp.filterOp.set('contains');
      comp.applyFilter('customer', mockEvent);

      expect(comp.filteredData().length).toBe(1);
      expect(comp.filteredData()[0].id).toBe(1);
    });
  });

  describe('Tier 3: State Persistence & Cross-Feature Integration', () => {
    it('GRID-T3-01: Loads saved column visibility and active filters on init', () => {
      const savedState: GridState = {
        gridCode: 'sales_invoices_grid',
        columns: [
          { field: 'id', visible: false },
          { field: 'docNo', visible: true, width: '120px' },
          { field: 'customer', visible: true },
          { field: 'amount', visible: true },
          { field: 'status', visible: false }
        ],
        filters: [
          { field: 'status', operator: 'equals', value: 'Posted' }
        ],
        pageSize: 50
      };

      mockStateService.loadState.mockReturnValue(savedState);

      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.gridCode = 'sales_invoices_grid';
      comp.columns = sampleColumns;
      comp.data = sampleData;
      comp.ngOnInit();

      expect(mockStateService.loadState).toHaveBeenCalledWith('sales_invoices_grid');
      expect(comp.visibleColumns().map(c => c.field)).toEqual(['docNo', 'customer', 'amount']);
      expect(comp.activeFilters()).toEqual([{ field: 'status', operator: 'equals', value: 'Posted' }]);
      expect(comp.filteredData().length).toBe(3);
    });

    it('GRID-T3-02: Applying and clearing filters triggers saveState on stateService', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.filterVal.set('Draft');
      comp.filterOp.set('equals');
      comp.applyFilter('status', mockEvent);

      expect(mockStateService.saveState).toHaveBeenCalledWith(
        expect.objectContaining({
          gridCode: 'sales_invoices_grid',
          filters: [{ field: 'status', operator: 'equals', value: 'Draft' }]
        })
      );

      comp.clearFilter('status', mockEvent);
      expect(mockStateService.saveState).toHaveBeenLastCalledWith(
        expect.objectContaining({
          gridCode: 'sales_invoices_grid',
          filters: []
        })
      );
    });

    it('GRID-T3-03: Reactive updates to data and activeFilters signal recalculate filteredData immediately', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      comp.filterVal.set('Posted');
      comp.filterOp.set('equals');
      comp.applyFilter('status', mockEvent);
      expect(comp.filteredData().length).toBe(3);

      // Mutate data input and re-apply / update activeFilters signal
      comp.data = [
        ...sampleData,
        { id: 6, docNo: 'INV-006', customer: 'Fresh Supermarket', amount: 99000, status: 'Posted' }
      ];
      comp.activeFilters.set([...comp.activeFilters()]);

      expect(comp.filteredData().length).toBe(4);
      expect(comp.filteredData()[3].docNo).toBe('INV-006');
    });
  });

  describe('Tier 4: Real-World Workflows & CSV Export', () => {
    it('GRID-T4-01: CSV export creates valid downloadable CSV from filtered dataset', () => {
      const comp = createComponent();
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // Filter only Posted items
      comp.filterVal.set('Posted');
      comp.filterOp.set('equals');
      comp.applyFilter('status', mockEvent);

      const createObjectURLSpy = vi.fn().mockReturnValue('blob:mock-url');
      const revokeObjectURLSpy = vi.fn();
      global.URL.createObjectURL = createObjectURLSpy;
      global.URL.revokeObjectURL = revokeObjectURLSpy;

      comp.exportToCsv();

      expect(createObjectURLSpy).toHaveBeenCalled();
    });

    it('GRID-T4-02: Exporting an empty grid does not create blobs or trigger downloads', () => {
      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.gridCode = 'empty_grid';
      comp.columns = sampleColumns;
      comp.data = [];
      comp.ngOnInit();

      const createObjectURLSpy = vi.fn();
      global.URL.createObjectURL = createObjectURLSpy;

      comp.exportToCsv();
      expect(createObjectURLSpy).not.toHaveBeenCalled();
    });
  });

  describe('Tier 5: ColumnDef Extensions & Numeric Formatting', () => {
    it('GRID-T5-01: isNumericCol returns true for numeric=true, align="right", or numeric dataTypes', () => {
      const comp = createComponent();

      expect(comp.isNumericCol({ field: 'f1', header: 'Col 1', numeric: true })).toBe(true);
      expect(comp.isNumericCol({ field: 'f2', header: 'Col 2', align: 'right' })).toBe(true);
      expect(comp.isNumericCol({ field: 'f3', header: 'Col 3', dataType: 'money' })).toBe(true);
      expect(comp.isNumericCol({ field: 'f4', header: 'Col 4', dataType: 'quantity' })).toBe(true);
      expect(comp.isNumericCol({ field: 'f5', header: 'Col 5', dataType: 'unitprice' })).toBe(true);
      expect(comp.isNumericCol({ field: 'f6', header: 'Col 6', dataType: 'number' })).toBe(true);

      // Non-numeric
      expect(comp.isNumericCol({ field: 'f7', header: 'Col 7', dataType: 'string' })).toBe(false);
      expect(comp.isNumericCol({ field: 'f8', header: 'Col 8', dataType: 'date' })).toBe(false);
      expect(comp.isNumericCol({ field: 'f9', header: 'Col 9', dataType: 'status' })).toBe(false);
    });

    it('GRID-T5-02: ColumnDef with sortable=false and custom width is respected', () => {
      const customCols: ColumnDef[] = [
        { field: 'actions', header: 'Actions', width: '100px', sortable: false },
        { field: 'balance', header: 'Balance', numeric: true, align: 'right', dataType: 'money' }
      ];
      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.columns = customCols;
      comp.data = [];
      comp.ngOnInit();

      expect(comp.visibleColumns()[0].sortable).toBe(false);
      expect(comp.visibleColumns()[0].width).toBe('100px');
      expect(comp.isNumericCol(comp.visibleColumns()[1])).toBe(true);
    });
  });

  describe('Tier 6: Interactive Sorting Mechanics & Sort Events', () => {
    it('GRID-T6-01: onSort cycles through asc -> desc -> null and emits sortChange', () => {
      const comp = createComponent();
      const sortSpy = vi.fn();
      comp.sortChange.subscribe(sortSpy);

      const col = sampleColumns[3]; // amount

      // First click: asc
      comp.onSort(col);
      expect(comp.sortField()).toBe('amount');
      expect(comp.sortDirection()).toBe('asc');
      expect(sortSpy).toHaveBeenCalledWith({ field: 'amount', direction: 'asc' });

      // Second click: desc
      comp.onSort(col);
      expect(comp.sortField()).toBe('amount');
      expect(comp.sortDirection()).toBe('desc');
      expect(sortSpy).toHaveBeenCalledWith({ field: 'amount', direction: 'desc' });

      // Third click: clear
      comp.onSort(col);
      expect(comp.sortField()).toBeNull();
      expect(comp.sortDirection()).toBeNull();
    });

    it('GRID-T6-02: onSort ignores columns where sortable=false or when grid sortable=false', () => {
      const comp = createComponent();
      const sortSpy = vi.fn();
      comp.sortChange.subscribe(sortSpy);

      // Non-sortable column
      const nonSortCol: ColumnDef = { field: 'action', header: 'Action', sortable: false };
      comp.onSort(nonSortCol);
      expect(comp.sortField()).toBeNull();
      expect(sortSpy).not.toHaveBeenCalled();

      // Grid sortable = false
      comp.sortable = false;
      comp.onSort(sampleColumns[1]);
      expect(comp.sortField()).toBeNull();
      expect(sortSpy).not.toHaveBeenCalled();
    });

    it('GRID-T6-03: Client-side sorting accurately sorts numeric and string data', () => {
      const comp = createComponent();

      // Sort by amount asc
      comp.onSort(sampleColumns[3]); // amount
      const ascAmounts = comp.filteredData().map(r => r.amount);
      expect(ascAmounts).toEqual([8500, 15000, 25000, 42000, 67000]);

      // Sort by amount desc
      comp.onSort(sampleColumns[3]);
      const descAmounts = comp.filteredData().map(r => r.amount);
      expect(descAmounts).toEqual([67000, 42000, 25000, 15000, 8500]);

      // Sort by customer name asc
      comp.onSort(sampleColumns[2]); // customer
      const ascCustomers = comp.filteredData().map(r => r.customer);
      expect(ascCustomers).toEqual([
        'Acme Supermarket',
        'Acme Traders',
        'Global Exports',
        'Global Mart',
        'Zenith Retail'
      ]);
    });

    it('GRID-T6-04: Saved sort state is restored on init and saved on sort change', () => {
      const savedState: GridState = {
        gridCode: 'sales_invoices_grid',
        columns: sampleColumns.map(c => ({ field: c.field, visible: true })),
        filters: [],
        pageSize: 50,
        sort: { field: 'customer', direction: 'desc' }
      };
      mockStateService.loadState.mockReturnValue(savedState);

      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.gridCode = 'sales_invoices_grid';
      comp.columns = sampleColumns;
      comp.data = sampleData;
      comp.ngOnInit();

      expect(comp.sortField()).toBe('customer');
      expect(comp.sortDirection()).toBe('desc');
      expect(comp.filteredData()[0].customer).toBe('Zenith Retail');
    });
  });

  describe('Tier 7: Pagination Mechanics & Page Events', () => {
    it('GRID-T7-01: Client-side pagination slices displayData to pageSize', () => {
      const comp = createComponent();
      comp.pageSize = 2;
      comp.currentPage = 1;
      comp.data = sampleData; // 5 items

      expect(comp.totalPages()).toBe(3);
      expect(comp.paginationSummary()).toBe('1–2 of 5 records');
      expect(comp.displayData().length).toBe(2);
      expect(comp.displayData()[0].id).toBe(1);
      expect(comp.displayData()[1].id).toBe(2);

      // Page 2
      comp.currentPage = 2;
      expect(comp.paginationSummary()).toBe('3–4 of 5 records');
      expect(comp.displayData().length).toBe(2);
      expect(comp.displayData()[0].id).toBe(3);

      // Page 3
      comp.currentPage = 3;
      expect(comp.paginationSummary()).toBe('5–5 of 5 records');
      expect(comp.displayData().length).toBe(1);
      expect(comp.displayData()[0].id).toBe(5);
    });

    it('GRID-T7-02: Server-side pagination uses totalCount and does not slice data', () => {
      const comp = createComponent();
      comp.totalCount = 120;
      comp.pageSize = 50;
      comp.currentPage = 1;
      comp.data = sampleData; // parent already passed 5 items for this page

      expect(comp.totalPages()).toBe(3);
      expect(comp.paginationSummary()).toBe('1–50 of 120 records');
      expect(comp.displayData().length).toBe(5);
    });

    it('GRID-T7-03: prevPage and nextPage update currentPage and emit pageChange', () => {
      const comp = createComponent();
      comp.pageSize = 2;
      comp.currentPage = 1;
      comp.data = sampleData; // 5 items, 3 pages

      const pageSpy = vi.fn();
      comp.pageChange.subscribe(pageSpy);

      // Boundary: prevPage at page 1 does nothing
      comp.prevPage();
      expect(comp.currentPage).toBe(1);
      expect(pageSpy).not.toHaveBeenCalled();

      // nextPage moves to page 2
      comp.nextPage();
      expect(comp.currentPage).toBe(2);
      expect(pageSpy).toHaveBeenCalledWith(2);

      // nextPage moves to page 3
      comp.nextPage();
      expect(comp.currentPage).toBe(3);
      expect(pageSpy).toHaveBeenCalledWith(3);

      // Boundary: nextPage at last page does nothing
      comp.nextPage();
      expect(comp.currentPage).toBe(3);
      expect(pageSpy).toHaveBeenCalledTimes(2);

      // prevPage moves back to page 2
      comp.prevPage();
      expect(comp.currentPage).toBe(2);
      expect(pageSpy).toHaveBeenCalledWith(2);
    });

    it('GRID-T7-04: Empty dataset pagination returns 0 records summary', () => {
      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.columns = sampleColumns;
      comp.data = [];
      comp.ngOnInit();

      expect(comp.totalPages()).toBe(1);
      expect(comp.paginationSummary()).toBe('0 records');
    });
  });

  describe('Tier 8: Loading State & Custom Empty Template', () => {
    it('GRID-T8-01: Default inputs reflect contract defaults', () => {
      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      expect(comp.loading).toBe(false);
      expect(comp.totalCount).toBe(0);
      expect(comp.pageSize).toBe(50);
      expect(comp.currentPage).toBe(1);
      expect(comp.compact).toBe(true);
      expect(comp.sortable).toBe(true);
      expect(comp.showExport).toBe(true);
    });

    it('GRID-T8-02: Updating input properties reactively updates signals', () => {
      const comp = createComponent();
      comp.loading = true;
      comp.compact = false;
      comp.showExport = false;

      expect(comp.loading).toBe(true);
      expect(comp.compact).toBe(false);
      expect(comp.showExport).toBe(false);
    });
  });
});
