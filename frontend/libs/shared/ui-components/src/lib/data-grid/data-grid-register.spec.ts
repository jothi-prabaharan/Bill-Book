import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DataGridComponent } from './data-grid.component';
import { DataGridService } from './data-grid.service';
import { ColumnDef } from './data-grid.models';

/**
 * The grid's register mode (TK-187): a checkbox on every row, an "All" box
 * across every page, a page-size choice and first/last paging. Opt-in, so a
 * grid that does not ask for it is unchanged.
 */
describe('DataGridComponent register mode', () => {
  let state: { loadState: ReturnType<typeof vi.fn>; saveState: ReturnType<typeof vi.fn> };

  const columns: ColumnDef[] = [
    { field: 'documentNo', header: 'Number' },
    { field: 'status', header: 'Status' },
  ];

  const rows = Array.from({ length: 23 }, (_, i) => ({
    documentNo: `POR-${String(i + 1).padStart(6, '0')}`,
    status: i % 3 === 0 ? 'Open' : 'Closed',
  }));

  beforeEach(() => {
    state = { loadState: vi.fn().mockReturnValue(null), saveState: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: DataGridService, useValue: state }] });
  });

  const create = (setup: (grid: DataGridComponent) => void = () => undefined): DataGridComponent => {
    const grid = TestBed.runInInjectionContext(() => new DataGridComponent());
    grid.gridCode = 'PURCHASE_LIST';
    grid.register = true;
    grid.columns = [...columns];
    setup(grid);
    grid.data = [...rows];
    grid.ngOnInit();
    return grid;
  };

  it('REG-GRID-01: a register opens on its smallest page size and always draws a checkbox column', () => {
    const grid = create();
    expect(grid.pageSize).toBe(10);
    expect(grid.showSelection).toBe(true);
    expect(grid.displayData().length).toBe(10);
    expect(grid.totalPages()).toBe(3);
    expect(grid.rangeSummary()).toBe('1 to 10 of 23');
  });

  it('REG-GRID-02: a saved page size is restored, and one the parent names wins over it', () => {
    state.loadState.mockReturnValue({ gridCode: 'PURCHASE_LIST', columns: [], filters: [], pageSize: 25 });
    expect(create().pageSize).toBe(25);

    expect(create((g) => (g.pageSize = 50)).pageSize).toBe(50);
  });

  it('REG-GRID-03: a saved page size the register does not offer falls back to the smallest', () => {
    state.loadState.mockReturnValue({ gridCode: 'PURCHASE_LIST', columns: [], filters: [], pageSize: 37 });
    expect(create().pageSize).toBe(10);
  });

  it('REG-GRID-04: changing the page size announces it, goes back to page one and is saved', () => {
    const grid = create();
    const sizes: number[] = [];
    const pages: number[] = [];
    grid.pageSizeChange.subscribe((s) => sizes.push(s));
    grid.pageChange.subscribe((p) => pages.push(p));

    grid.goToPage(3);
    grid.setPageSize(25);

    expect(sizes).toEqual([25]);
    expect(grid.currentPage).toBe(1);
    expect(pages).toEqual([3, 1]);
    expect(grid.totalPages()).toBe(1);
    expect(state.saveState).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 25 }));
  });

  it('REG-GRID-05: first, last and out-of-range pages are clamped', () => {
    const grid = create();
    grid.goToPage(99);
    expect(grid.currentPage).toBe(3);
    expect(grid.rangeSummary()).toBe('21 to 23 of 23');
    grid.goToPage(-4);
    expect(grid.currentPage).toBe(1);
  });

  it('REG-GRID-06: "All" selects every filtered row across pages; the heading box selects this page only', () => {
    const grid = create();

    grid.togglePage();
    expect(grid.selectedRows().length).toBe(10);
    expect(grid.pageSelected()).toBe(true);
    expect(grid.someSelected()).toBe(true);

    grid.toggleAll();
    expect(grid.selectedRows().length).toBe(23);
    expect(grid.allSelected()).toBe(true);

    grid.toggleAll();
    expect(grid.selectedRows().length).toBe(0);
  });

  it('REG-GRID-07: ticking a row toggles it and announces the selection', () => {
    const grid = create();
    const seen: number[] = [];
    grid.selectionChange.subscribe((s) => seen.push(s.length));

    grid.toggleRow(rows[0]);
    grid.toggleRow(rows[1]);
    grid.toggleRow(rows[0]);

    expect(seen).toEqual([1, 2, 1]);
    expect(grid.isSelected(rows[1])).toBe(true);
    expect(grid.isSelected(rows[0])).toBe(false);
  });

  it('REG-GRID-08: a selected row that leaves the data is no longer selected', () => {
    const grid = create();
    grid.toggleRow(rows[0]);
    grid.toggleRow(rows[1]);

    grid.data = rows.slice(1);

    expect(grid.selectedRows()).toEqual([rows[1]]);
  });

  it('REG-GRID-09: a server-paged register counts the server total, not the page it was sent', () => {
    const grid = create((g) => (g.totalCount = 230));
    expect(grid.totalItems()).toBe(230);
    expect(grid.totalPages()).toBe(23);
  });

  it('REG-GRID-10: a grid that does not ask for register mode keeps its own page size and no checkboxes', () => {
    const grid = TestBed.runInInjectionContext(() => new DataGridComponent());
    grid.columns = [...columns];
    grid.data = [...rows];
    grid.ngOnInit();

    expect(grid.register).toBe(false);
    expect(grid.showSelection).toBe(false);
    expect(grid.pageSize).toBe(50);
  });
});
