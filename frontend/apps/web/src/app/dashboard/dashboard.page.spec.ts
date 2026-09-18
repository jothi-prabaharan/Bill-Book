import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach } from 'vitest';
import { ShellBoardService } from '@bill-book/app-shell';
import { DashboardPage } from './dashboard.page';
import {
  DASHBOARD_LAYOUT_KEY,
  coerceLayout,
  defaultLayout,
  moveInOrder,
  nextSpan,
} from './dashboard-layout';

describe('Dashboard layout persistence', () => {
  beforeEach(() => {
    localStorage.removeItem(DASHBOARD_LAYOUT_KEY);
  });

  it('LAYOUT-01: Defaults list every widget, none hidden', () => {
    const layout = defaultLayout();
    expect(layout.order.length).toBeGreaterThan(0);
    expect(layout.hidden).toEqual([]);
    expect(layout.spans['sales']).toBe(3);
    expect(layout.spans['stock']).toBe(7);
  });

  it('LAYOUT-02: Unparseable or foreign stored values fall back to the defaults', () => {
    expect(coerceLayout(null)).toEqual(defaultLayout());
    expect(coerceLayout('nonsense')).toEqual(defaultLayout());
    expect(coerceLayout(42)).toEqual(defaultLayout());
  });

  it('LAYOUT-03: Unknown widget ids are dropped and missing ones appended', () => {
    const layout = coerceLayout({
      order: ['aging', 'not-a-widget', 'sales'],
      spans: {},
      hidden: ['also-not-a-widget'],
    });

    expect(layout.order.slice(0, 2)).toEqual(['aging', 'sales']);
    expect(layout.order).toContain('docs');
    expect(layout.order.length).toBe(defaultLayout().order.length);
    expect(layout.hidden).toEqual([]);
  });

  it('LAYOUT-04: A span outside the allowed cycle falls back to the widget default', () => {
    const layout = coerceLayout({ order: [], spans: { sales: 5, docs: 8 }, hidden: [] });
    expect(layout.spans['sales']).toBe(3); // 5 is not a step in the cycle
    expect(layout.spans['docs']).toBe(8);
  });

  it('LAYOUT-05: Duplicated ids in the stored order are collapsed', () => {
    const layout = coerceLayout({ order: ['sales', 'sales', 'recv'], spans: {}, hidden: [] });
    expect(layout.order.filter((id) => id === 'sales').length).toBe(1);
  });

  it('LAYOUT-06: Widths cycle and wrap', () => {
    expect(nextSpan(3)).toBe(4);
    expect(nextSpan(12)).toBe(3);
    // A width that is not in the cycle rejoins it at the start.
    expect(nextSpan(7)).toBe(3);
  });

  it('LAYOUT-07: Moving clamps at both ends instead of wrapping', () => {
    const order = ['a', 'b', 'c'];
    expect(moveInOrder(order, 'a', -1)).toEqual(['a', 'b', 'c']);
    expect(moveInOrder(order, 'c', 1)).toEqual(['a', 'b', 'c']);
    expect(moveInOrder(order, 'b', -1)).toEqual(['b', 'a', 'c']);
    expect(moveInOrder(order, 'b', 1)).toEqual(['a', 'c', 'b']);
    expect(moveInOrder(order, 'z', 1)).toEqual(order);
  });
});

describe('DashboardPage customize mode', () => {
  beforeEach(() => {
    localStorage.removeItem(DASHBOARD_LAYOUT_KEY);
    TestBed.configureTestingModule({ providers: [] });
  });

  const createPage = (): DashboardPage =>
    TestBed.runInInjectionContext(() => new DashboardPage());

  it('DASH-01: Customize mode follows the breadcrumb strip, not the page', () => {
    const page = createPage();
    const board = TestBed.inject(ShellBoardService);
    board.stopEdit();

    expect(page.editing()).toBe(false);
    board.startEdit();
    expect(page.editing()).toBe(true);
  });

  it('DASH-02: Removing a widget moves it to the tray and back', () => {
    const page = createPage();
    const before = page.visible().length;

    page.remove('gst');
    expect(page.visible().length).toBe(before - 1);
    expect(page.removed().map((w) => w.id)).toEqual(['gst']);

    page.add('gst');
    expect(page.visible().length).toBe(before);
    expect(page.removed()).toEqual([]);
  });

  it('DASH-03: Removing the same widget twice is not a second removal', () => {
    const page = createPage();
    page.remove('gst');
    page.remove('gst');
    expect(page.removed().map((w) => w.id)).toEqual(['gst']);
  });

  it('DASH-04: Moving and resizing survive a reload of the same browser', () => {
    const page = createPage();
    page.moveRight('sales');
    page.resize('sales');
    const movedOrder = page.visible().map((w) => w.id);
    const movedSpan = page.span('sales');

    const reloaded = createPage();
    expect(reloaded.visible().map((w) => w.id)).toEqual(movedOrder);
    expect(reloaded.span('sales')).toBe(movedSpan);
  });

  it('DASH-05: Reset from the breadcrumb strip restores the default arrangement', () => {
    const page = createPage();
    const board = TestBed.inject(ShellBoardService);

    page.remove('aging');
    page.resize('docs');
    expect(page.removed().length).toBe(1);

    board.requestReset();
    TestBed.flushEffects();

    expect(page.removed()).toEqual([]);
    expect(page.visible().map((w) => w.id)).toEqual(defaultLayout().order);
    expect(page.span('docs')).toBe(6);
  });
});
