import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { DataGridComponent } from './data-grid.component';
import { DataGridService } from './data-grid.service';
import { ColumnDef, GridState } from './data-grid.models';

describe('DataGridComponent — Adversarial Stress & Empirical Challenge Suite', () => {
  let mockStateService: {
    loadState: ReturnType<typeof vi.fn>;
    saveState: ReturnType<typeof vi.fn>;
  };

  const sampleCols: ColumnDef[] = [
    { field: 'id', header: 'ID', numeric: true, dataType: 'number', width: '60px' },
    { field: 'code', header: 'Code', sortable: true },
    { field: 'name', header: 'Customer Name', sortable: true },
    { field: 'amount', header: 'Amount', numeric: true, align: 'right', dataType: 'money' },
    { field: 'date', header: 'Created Date', dataType: 'date', sortable: true },
    { field: 'status', header: 'Status', dataType: 'status' },
    { field: 'isVip', header: 'VIP', dataType: 'boolean' },
    { field: 'actions', header: 'Actions', sortable: false }
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

  const createGrid = (cols: ColumnDef[] = sampleCols, data: any[] = []): DataGridComponent => {
    const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
    comp.gridCode = 'stress_test_grid';
    comp.columns = cols;
    comp.data = data;
    comp.ngOnInit();
    return comp;
  };

  // =========================================================================
  // SUITE 1: BOUNDARY CONDITIONS & HOSTILE DATA SHAPES
  // =========================================================================
  describe('Suite 1: Boundary Conditions & Hostile Data Shapes', () => {
    it('STRESS-01: Empty data array [] maintains robust state and pagination invariants', () => {
      const grid = createGrid(sampleCols, []);

      expect(grid.filteredData()).toEqual([]);
      expect(grid.displayData()).toEqual([]);
      expect(grid.totalPages()).toBe(1);
      expect(grid.paginationSummary()).toBe('0 records');

      // Sort on empty grid
      grid.onSort(sampleCols[1]);
      expect(grid.filteredData()).toEqual([]);

      // Filter on empty grid
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      grid.filterVal.set('nonexistent');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData()).toEqual([]);
      expect(grid.paginationSummary()).toBe('0 records');
    });

    it('STRESS-02: Single-item array handles full lifecycle (render, filter, sort, pagination)', () => {
      const singleRow = { id: 101, code: 'SO-101', name: 'Alpha Traders', amount: 5400, date: new Date('2026-01-01'), status: 'Posted', isVip: true };
      const grid = createGrid(sampleCols, [singleRow]);

      expect(grid.filteredData().length).toBe(1);
      expect(grid.displayData().length).toBe(1);
      expect(grid.totalPages()).toBe(1);
      expect(grid.paginationSummary()).toBe('1–1 of 1 records');

      // Sort single item
      grid.onSort(sampleCols[3]); // amount asc
      expect(grid.filteredData()[0].id).toBe(101);

      // Filter match
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      grid.filterVal.set('Alpha');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(1);

      // Filter miss
      grid.filterVal.set('Beta');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(0);
      expect(grid.paginationSummary()).toBe('0 records');
    });

    it('STRESS-03: Sparse, null-heavy, and undefined row records do not throw runtime exceptions', () => {
      const hostileData = [
        { id: 1, code: null, name: null, amount: null, date: null, status: null, isVip: null },
        { id: 2, code: undefined, name: undefined, amount: undefined, date: undefined, status: undefined, isVip: undefined },
        { id: 3 }, // missing all keys
        { id: 4, code: 'SO-4', name: 'Solid Corp', amount: 1000, date: new Date('2026-02-01'), status: 'Open', isVip: false },
        {} // completely empty object
      ];

      const grid = createGrid(sampleCols, hostileData);
      expect(grid.filteredData().length).toBe(5);

      // Filter with null/undefined values present
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      grid.filterVal.set('Solid');
      grid.filterOp.set('contains');
      grid.applyFilter('name', mockEvent);

      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(4);

      // Clear filter
      grid.clearFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(5);

      // Sort with nulls/undefineds
      grid.onSort(sampleCols[3]); // sort by amount asc
      const sorted = grid.filteredData();
      expect(sorted.length).toBe(5);
      // Valid amount should be first, nulls/undefineds sorted to end
      expect(sorted[0].id).toBe(4);
    });

    it('STRESS-04: Massive dataset (2,500 items) computes sorting, filtering, and paging efficiently', () => {
      const largeData: any[] = [];
      const statuses = ['Draft', 'Open', 'Posted', 'Void', 'Closed'];
      for (let i = 1; i <= 2500; i++) {
        largeData.push({
          id: i,
          code: `DOC-${String(i).padStart(5, '0')}`,
          name: `Customer ${i % 100}`,
          amount: (i * 17.5) % 10000,
          date: new Date(2026, 0, (i % 28) + 1),
          status: statuses[i % statuses.length],
          isVip: i % 10 === 0
        });
      }

      const grid = createGrid(sampleCols, largeData);
      grid.pageSize = 50;

      // Check initial large pagination
      expect(grid.filteredData().length).toBe(2500);
      expect(grid.totalPages()).toBe(50);
      expect(grid.paginationSummary()).toBe('1–50 of 2500 records');
      expect(grid.displayData().length).toBe(50);
      expect(grid.displayData()[0].id).toBe(1);

      // Sort 2,500 items by amount asc
      const t0 = performance.now();
      grid.onSort(sampleCols[3]);
      const sortDuration = performance.now() - t0;
      expect(sortDuration).toBeLessThan(200); // must execute sub-200ms

      const sortedData = grid.filteredData();
      expect(sortedData.length).toBe(2500);
      expect(sortedData[0].amount).toBeLessThanOrEqual(sortedData[1].amount);
      expect(sortedData[100].amount).toBeLessThanOrEqual(sortedData[101].amount);

      // Filter 2,500 items down
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;
      grid.filterVal.set('Customer 42');
      grid.filterOp.set('equals');
      grid.applyFilter('name', mockEvent);

      expect(grid.filteredData().length).toBe(25); // exactly 25 items match
      expect(grid.totalPages()).toBe(1);
      expect(grid.paginationSummary()).toBe('1–25 of 25 records');
      expect(grid.displayData().length).toBe(25);
    });
  });

  // =========================================================================
  // SUITE 2: ADVANCED SORTING EDGE CASES & ORACLES
  // =========================================================================
  describe('Suite 2: Advanced Sorting Edge Cases & Oracles', () => {
    it('STRESS-05: Numeric comparator sorts numbers by numeric magnitude, not lexicographically', () => {
      const data = [
        { id: 1, amount: 100 },
        { id: 2, amount: 2 },
        { id: 3, amount: 10 },
        { id: 4, amount: 20 },
        { id: 5, amount: 1 }
      ];
      const grid = createGrid([{ field: 'amount', header: 'Amount', dataType: 'number', numeric: true }], data);

      // Ascending
      grid.onSort(grid.columns[0]);
      expect(grid.filteredData().map(r => r.amount)).toEqual([1, 2, 10, 20, 100]);

      // Descending
      grid.onSort(grid.columns[0]);
      expect(grid.filteredData().map(r => r.amount)).toEqual([100, 20, 10, 2, 1]);

      // Idle / reset
      grid.onSort(grid.columns[0]);
      expect(grid.filteredData().map(r => r.amount)).toEqual([100, 2, 10, 20, 1]);
    });

    it('STRESS-06: Negative numbers and zero are correctly ordered in ascending and descending', () => {
      const data = [
        { id: 1, amount: 0 },
        { id: 2, amount: -15.5 },
        { id: 3, amount: 100 },
        { id: 4, amount: -100 },
        { id: 5, amount: 50 }
      ];
      const grid = createGrid([{ field: 'amount', header: 'Amount', dataType: 'number', numeric: true }], data);

      grid.onSort(grid.columns[0]); // asc
      expect(grid.filteredData().map(r => r.amount)).toEqual([-100, -15.5, 0, 50, 100]);

      grid.onSort(grid.columns[0]); // desc
      expect(grid.filteredData().map(r => r.amount)).toEqual([100, 50, 0, -15.5, -100]);
    });

    it('STRESS-07: Date instances sort chronologically across leap years and timestamps', () => {
      const data = [
        { id: 1, date: new Date('2026-08-19T20:00:00Z') },
        { id: 2, date: new Date('2024-02-29T12:00:00Z') }, // leap day
        { id: 3, date: new Date('2026-01-01T00:00:00Z') },
        { id: 4, date: new Date('2025-12-31T23:59:59Z') }
      ];
      const grid = createGrid([{ field: 'date', header: 'Date', dataType: 'date' }], data);

      grid.onSort(grid.columns[0]); // asc
      const ascIds = grid.filteredData().map(r => r.id);
      expect(ascIds).toEqual([2, 4, 3, 1]);

      grid.onSort(grid.columns[0]); // desc
      const descIds = grid.filteredData().map(r => r.id);
      expect(descIds).toEqual([1, 3, 4, 2]);
    });

    it('STRESS-08: Natural alphanumeric sorting correctly handles embedded numbers (INV-1, INV-2, INV-10)', () => {
      const data = [
        { id: 1, code: 'INV-10' },
        { id: 2, code: 'INV-2' },
        { id: 3, code: 'INV-1' },
        { id: 4, code: 'INV-20' },
        { id: 5, code: 'INV-100' }
      ];
      const grid = createGrid([{ field: 'code', header: 'Code' }], data);

      grid.onSort(grid.columns[0]); // asc
      expect(grid.filteredData().map(r => r.code)).toEqual([
        'INV-1',
        'INV-2',
        'INV-10',
        'INV-20',
        'INV-100'
      ]);
    });

    it('STRESS-09: Switching active sort column cleanly updates sortField and direction to asc', () => {
      const data = [
        { id: 1, code: 'B', name: 'Zebra', amount: 300 },
        { id: 2, code: 'A', name: 'Apple', amount: 100 },
        { id: 3, code: 'C', name: 'Mango', amount: 200 }
      ];
      const grid = createGrid(sampleCols, data);
      const sortSpy = vi.fn();
      grid.sortChange.subscribe(sortSpy);

      // Sort code asc -> desc
      grid.onSort(sampleCols[1]); // code asc
      expect(grid.sortField()).toBe('code');
      expect(grid.sortDirection()).toBe('asc');

      grid.onSort(sampleCols[1]); // code desc
      expect(grid.sortField()).toBe('code');
      expect(grid.sortDirection()).toBe('desc');

      // Click different column: amount
      grid.onSort(sampleCols[3]); // amount asc
      expect(grid.sortField()).toBe('amount');
      expect(grid.sortDirection()).toBe('asc');
      expect(grid.filteredData().map(r => r.amount)).toEqual([100, 200, 300]);
      expect(sortSpy).toHaveBeenLastCalledWith({ field: 'amount', direction: 'asc' });
    });

    it('STRESS-10: Non-sortable columns and disabled sortable grid reject sort attempts', () => {
      const data = [{ id: 1, name: 'Test' }];
      const grid = createGrid(sampleCols, data);
      const sortSpy = vi.fn();
      grid.sortChange.subscribe(sortSpy);

      // col.sortable = false
      const actionsCol = sampleCols.find(c => c.field === 'actions')!;
      grid.onSort(actionsCol);
      expect(grid.sortField()).toBeNull();
      expect(sortSpy).not.toHaveBeenCalled();

      // grid.sortable = false
      grid.sortable = false;
      grid.onSort(sampleCols[2]); // name
      expect(grid.sortField()).toBeNull();
      expect(sortSpy).not.toHaveBeenCalled();
    });

    it('STRESS-11: Sorting array with all identical values remains stable with zero comparator diffs', () => {
      const data = [
        { id: 1, amount: 500, seq: 1 },
        { id: 2, amount: 500, seq: 2 },
        { id: 3, amount: 500, seq: 3 },
        { id: 4, amount: 500, seq: 4 }
      ];
      const grid = createGrid(sampleCols, data);

      grid.onSort(sampleCols[3]); // amount asc
      expect(grid.filteredData().map(r => r.id)).toEqual([1, 2, 3, 4]);

      grid.onSort(sampleCols[3]); // amount desc
      expect(grid.filteredData().map(r => r.id)).toEqual([1, 2, 3, 4]);
    });
  });

  // =========================================================================
  // SUITE 3: PAGINATION INTEGRITY & BOUNDARY CONDITIONS
  // =========================================================================
  describe('Suite 3: Pagination Integrity & Boundary Conditions', () => {
    it('STRESS-12: Client-side pagination exactly handles exact multiples of pageSize', () => {
      const data: any[] = [];
      for (let i = 1; i <= 100; i++) {
        data.push({ id: i, name: `Item ${i}` });
      }

      const grid = createGrid(sampleCols, data);
      grid.pageSize = 50;
      grid.currentPage = 1;

      expect(grid.totalPages()).toBe(2);
      expect(grid.paginationSummary()).toBe('1–50 of 100 records');
      expect(grid.displayData().length).toBe(50);
      expect(grid.displayData()[0].id).toBe(1);
      expect(grid.displayData()[49].id).toBe(50);

      // Page 2
      grid.nextPage();
      expect(grid.currentPage).toBe(2);
      expect(grid.paginationSummary()).toBe('51–100 of 100 records');
      expect(grid.displayData().length).toBe(50);
      expect(grid.displayData()[0].id).toBe(51);
      expect(grid.displayData()[49].id).toBe(100);

      // nextPage at boundary page 2 does nothing
      grid.nextPage();
      expect(grid.currentPage).toBe(2);
    });

    it('STRESS-13: Client-side pagination with non-multiple trailing records (e.g. 73 items on pageSize 25)', () => {
      const data: any[] = [];
      for (let i = 1; i <= 73; i++) {
        data.push({ id: i, name: `Item ${i}` });
      }

      const grid = createGrid(sampleCols, data);
      grid.pageSize = 25;
      grid.currentPage = 1;

      expect(grid.totalPages()).toBe(3); // Math.ceil(73/25) = 3
      expect(grid.paginationSummary()).toBe('1–25 of 73 records');
      expect(grid.displayData().length).toBe(25);

      grid.nextPage(); // Page 2
      expect(grid.currentPage).toBe(2);
      expect(grid.paginationSummary()).toBe('26–50 of 73 records');
      expect(grid.displayData().length).toBe(25);

      grid.nextPage(); // Page 3
      expect(grid.currentPage).toBe(3);
      expect(grid.paginationSummary()).toBe('51–73 of 73 records');
      expect(grid.displayData().length).toBe(23); // remaining 23 items
    });

    it('STRESS-14: Server-side pagination with totalCount bypasses client slice and respects page boundaries', () => {
      const pageData = [
        { id: 101, name: 'Item 101' },
        { id: 102, name: 'Item 102' }
      ];

      const grid = createGrid(sampleCols, pageData);
      grid.totalCount = 500;
      grid.pageSize = 50;
      grid.currentPage = 3;

      expect(grid.totalPages()).toBe(10);
      expect(grid.paginationSummary()).toBe('101–150 of 500 records');
      // Must not slice because server already provided the page
      expect(grid.displayData().length).toBe(2);
      expect(grid.displayData()[0].id).toBe(101);

      const pageSpy = vi.fn();
      grid.pageChange.subscribe(pageSpy);

      grid.nextPage();
      expect(grid.currentPage).toBe(4);
      expect(pageSpy).toHaveBeenCalledWith(4);

      grid.prevPage();
      expect(grid.currentPage).toBe(3);
      expect(pageSpy).toHaveBeenCalledWith(3);
    });

    it('STRESS-15: Zero and negative pageSize gracefully fallback to single page without division errors', () => {
      const data = [{ id: 1 }, { id: 2 }];
      const grid = createGrid(sampleCols, data);

      grid.pageSize = 0;
      expect(grid.totalPages()).toBe(1);
      expect(grid.displayData().length).toBe(2);

      grid.pageSize = -10;
      expect(grid.totalPages()).toBe(1);
      expect(grid.displayData().length).toBe(2);
    });

    it('STRESS-16: prevPage at page 1 and nextPage at last page do not mutate state or emit events', () => {
      const data = [{ id: 1 }, { id: 2 }, { id: 3 }];
      const grid = createGrid(sampleCols, data);
      grid.pageSize = 2;
      grid.currentPage = 1;

      const pageSpy = vi.fn();
      grid.pageChange.subscribe(pageSpy);

      // Prev at first page
      grid.prevPage();
      grid.prevPage();
      expect(grid.currentPage).toBe(1);
      expect(pageSpy).not.toHaveBeenCalled();

      // Go to last page (page 2)
      grid.nextPage();
      expect(grid.currentPage).toBe(2);
      expect(pageSpy).toHaveBeenCalledTimes(1);

      // Next at last page
      grid.nextPage();
      grid.nextPage();
      expect(grid.currentPage).toBe(2);
      expect(pageSpy).toHaveBeenCalledTimes(1);
    });
  });

  // =========================================================================
  // SUITE 4: FILTER INTERACTIONS & HOSTILE QUERY STRINGS
  // =========================================================================
  describe('Suite 4: Filter Interactions & Hostile Query Strings', () => {
    const complexData = [
      { id: 1, code: 'DOC [V1.0]', name: 'Acme & Sons (Pvt) Ltd', status: 'Posted', isVip: true, amount: 1500 },
      { id: 2, code: 'DOC +Special', name: 'Global.*Logistics?^', status: 'Draft', isVip: false, amount: 2500 },
      { id: 3, code: 'DOC $100', name: 'Zenith {Core} | Branch', status: 'Open', isVip: true, amount: 3500 },
      { id: 4, code: 'DOC \\Backslash', name: 'Omega [Alpha] Corp', status: 'Posted', isVip: false, amount: 4500 }
    ];

    it('STRESS-17: Regex metacharacters (.*+?^${}()|[]\\) in filter input are treated as exact literal strings', () => {
      const grid = createGrid(sampleCols, complexData);
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // 1. Literal ".*"
      grid.filterVal.set('.*');
      grid.filterOp.set('contains');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(2);
      grid.clearFilter('name', mockEvent);

      // 2. Literal "{"
      grid.filterVal.set('{Core}');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(3);
      grid.clearFilter('name', mockEvent);

      // 3. Literal "[]"
      grid.filterVal.set('[V1.0]');
      grid.applyFilter('code', mockEvent);
      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(1);
      grid.clearFilter('code', mockEvent);

      // 4. Literal "\\"
      grid.filterVal.set('\\Backslash');
      grid.applyFilter('code', mockEvent);
      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(4);
      grid.clearFilter('code', mockEvent);
    });

    it('STRESS-18: Empty or whitespace-only filter strings clear the active filter for that column', () => {
      const grid = createGrid(sampleCols, complexData);
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // Apply initial filter
      grid.filterVal.set('Acme');
      grid.applyFilter('name', mockEvent);
      expect(grid.activeFilters().length).toBe(1);
      expect(grid.filteredData().length).toBe(1);

      // Overwrite with empty string
      grid.filterVal.set('');
      grid.applyFilter('name', mockEvent);
      expect(grid.activeFilters().length).toBe(0);
      expect(grid.filteredData().length).toBe(4);

      // Overwrite with whitespace-only string
      grid.filterVal.set('   ');
      grid.applyFilter('name', mockEvent);
      expect(grid.activeFilters().length).toBe(0);
      expect(grid.filteredData().length).toBe(4);
    });

    it('STRESS-19: Combined multi-column filter and sort interaction maintains data coherence', () => {
      const grid = createGrid(sampleCols, complexData);
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // Filter 1: status equals Posted (items 1 & 4)
      grid.filterVal.set('Posted');
      grid.filterOp.set('equals');
      grid.applyFilter('status', mockEvent);
      expect(grid.filteredData().length).toBe(2);

      // Sort by amount descending
      grid.onSort(sampleCols[3]); // asc
      grid.onSort(sampleCols[3]); // desc
      expect(grid.sortDirection()).toBe('desc');

      // Filtered & sorted: amount 4500 (id 4) then amount 1500 (id 1)
      expect(grid.filteredData().map(r => r.id)).toEqual([4, 1]);

      // Add Filter 2: name contains Omega -> only item 4
      grid.filterVal.set('Omega');
      grid.filterOp.set('contains');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(1);
      expect(grid.filteredData()[0].id).toBe(4);

      // Clear Filter 2 -> restores items 4 and 1 in desc sort order
      grid.clearFilter('name', mockEvent);
      expect(grid.filteredData().length).toBe(2);
      expect(grid.filteredData().map(r => r.id)).toEqual([4, 1]);
    });

    it('STRESS-20: Filter operators (equals, contains, starts) are strictly case-insensitive', () => {
      const data = [
        { id: 1, name: 'BANGALORE BRANCH' },
        { id: 2, name: 'bangalore central' },
        { id: 3, name: 'South Bangalore hub' },
        { id: 4, name: 'Mumbai depot' }
      ];
      const grid = createGrid([{ field: 'name', header: 'Name' }], data);
      const mockEvent = { stopPropagation: vi.fn() } as unknown as Event;

      // starts with "bangalore"
      grid.filterVal.set('bangalore');
      grid.filterOp.set('starts');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().map(r => r.id)).toEqual([1, 2]);

      // contains "BANGALORE"
      grid.filterVal.set('BANGALORE');
      grid.filterOp.set('contains');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().map(r => r.id)).toEqual([1, 2, 3]);

      // equals "MUMBAI DEPOT"
      grid.filterVal.set('MUMBAI DEPOT');
      grid.filterOp.set('equals');
      grid.applyFilter('name', mockEvent);
      expect(grid.filteredData().map(r => r.id)).toEqual([4]);
    });
  });

  // =========================================================================
  // SUITE 5: CSV EXPORT RESILIENCE
  // =========================================================================
  describe('Suite 5: CSV Export Resilience', () => {
    it('STRESS-21: CSV export handles commas, quotes, nulls, and special characters cleanly', () => {
      const complexData = [
        { id: 1, code: 'SO,"001"', name: 'Acme, "Super" Traders', amount: 1250.5, status: null },
        { id: 2, code: 'SO-002', name: 'Normal Name', amount: null, status: undefined }
      ];

      const grid = createGrid(sampleCols, complexData);

      const createObjectURLSpy = vi.fn().mockImplementation((_blob: Blob) => {
        return 'blob:mock-url';
      });
      global.URL.createObjectURL = createObjectURLSpy;

      grid.exportToCsv();

      expect(createObjectURLSpy).toHaveBeenCalledTimes(1);
      const blobArg = createObjectURLSpy.mock.calls[0][0] as Blob;
      expect(blobArg).toBeInstanceOf(Blob);
      expect(blobArg.type).toContain('text/csv');
    });

    it('STRESS-22: CSV export is a safe no-op when filtered data is empty', () => {
      const grid = createGrid(sampleCols, []);
      const createObjectURLSpy = vi.fn();
      global.URL.createObjectURL = createObjectURLSpy;

      grid.exportToCsv();
      expect(createObjectURLSpy).not.toHaveBeenCalled();
    });
  });

  // =========================================================================
  // SUITE 6: DYNAMIC RECONFIGURATION & RE-RENDER RESILIENCE
  // =========================================================================
  describe('Suite 6: Dynamic Reconfiguration & Lifecycle Resilience', () => {
    it('STRESS-23: Dynamically resetting data from large dataset to empty array updates all signals', () => {
      const largeData = Array.from({ length: 500 }, (_, i) => ({ id: i + 1, name: `Row ${i + 1}` }));
      const grid = createGrid(sampleCols, largeData);
      grid.pageSize = 25;
      grid.currentPage = 10;

      expect(grid.totalPages()).toBe(20);
      expect(grid.paginationSummary()).toBe('226–250 of 500 records');

      // Dynamically wipe data
      grid.data = [];
      expect(grid.filteredData().length).toBe(0);
      expect(grid.displayData().length).toBe(0);
      expect(grid.paginationSummary()).toBe('0 records');
    });

    it('STRESS-24: Dynamically changing columns updates visibleColumns and strips hidden ones', () => {
      const grid = createGrid(sampleCols, [{ id: 1, name: 'Test' }]);
      expect(grid.visibleColumns().length).toBe(8);

      const newCols: ColumnDef[] = [
        { field: 'id', header: 'ID' },
        { field: 'hiddenOne', header: 'H1', visible: false },
        { field: 'name', header: 'Name' },
        { field: 'hiddenTwo', header: 'H2', visible: false }
      ];

      grid.columns = newCols;
      expect(grid.visibleColumns().length).toBe(2);
      expect(grid.visibleColumns().map(c => c.field)).toEqual(['id', 'name']);
    });

    it('STRESS-25: isNumericCol correctly identifies all numeric variants and ignores non-numeric', () => {
      const grid = createGrid(sampleCols, []);

      expect(grid.isNumericCol({ field: 'f1', numeric: true })).toBe(true);
      expect(grid.isNumericCol({ field: 'f2', align: 'right' })).toBe(true);
      expect(grid.isNumericCol({ field: 'f3', dataType: 'money' })).toBe(true);
      expect(grid.isNumericCol({ field: 'f4', dataType: 'quantity' })).toBe(true);
      expect(grid.isNumericCol({ field: 'f5', dataType: 'unitprice' })).toBe(true);
      expect(grid.isNumericCol({ field: 'f6', dataType: 'number' })).toBe(true);
      expect(grid.isNumericCol({ field: 'f7', dataType: 'string' })).toBe(false);
      expect(grid.isNumericCol({ field: 'f8', dataType: 'date' })).toBe(false);
      expect(grid.isNumericCol(null as any)).toBe(false);
    });

    it('STRESS-26: Saved state rehydration correctly sets sort, filters, and column visibility', () => {
      const savedState: GridState = {
        gridCode: 'rehydrate_grid',
        columns: [
          { field: 'id', visible: true },
          { field: 'code', visible: false },
          { field: 'name', visible: true }
        ],
        filters: [{ field: 'name', operator: 'starts', value: 'Alpha' }],
        pageSize: 25,
        sort: { field: 'name', direction: 'desc' }
      };

      mockStateService.loadState.mockReturnValue(savedState);

      const comp = TestBed.runInInjectionContext(() => new DataGridComponent());
      comp.gridCode = 'rehydrate_grid';
      comp.columns = [
        { field: 'id', header: 'ID' },
        { field: 'code', header: 'Code' },
        { field: 'name', header: 'Name' }
      ];
      comp.ngOnInit();

      expect(comp.visibleColumns().map(c => c.field)).toEqual(['id', 'name']);
      expect(comp.activeFilters()).toEqual([{ field: 'name', operator: 'starts', value: 'Alpha' }]);
      expect(comp.sortField()).toBe('name');
      expect(comp.sortDirection()).toBe('desc');
    });
  });
});
