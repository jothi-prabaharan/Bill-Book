import { describe, expect, it } from 'vitest';
import { declaredPageSize, layoutWidthPx, printPageSize } from './print-page';

const page = (size: string): string => `<html><head><style>@page { size: ${size}; margin: 0 }</style></head><body></body></html>`;

describe('declaredPageSize', () => {
  it("reads a receipt roll's width with an auto height", () => {
    expect(declaredPageSize(page('80mm auto'))).toEqual({ widthMm: 80, heightMm: null });
  });

  it("reads a label's width and height", () => {
    expect(declaredPageSize(page('50mm 25mm'))).toEqual({ widthMm: 50, heightMm: 25 });
  });

  it('converts other units to millimetres', () => {
    expect(declaredPageSize(page('2in 1in'))).toEqual({ widthMm: 50.8, heightMm: 25.4 });
    expect(declaredPageSize(page('5.8cm auto')).widthMm).toBeCloseTo(58);
  });

  it('leaves named sizes and pages without a rule to the printer', () => {
    expect(declaredPageSize(page('A4'))).toEqual({ widthMm: null, heightMm: null });
    expect(declaredPageSize('<p>no rule</p>')).toEqual({ widthMm: null, heightMm: null });
  });
});

describe('printPageSize', () => {
  it('prints a receipt as long as its content', () => {
    // 960 px at 96 per inch is 10 inches, 254 mm.
    expect(printPageSize(page('80mm auto'), 960)).toEqual({ width: 80_000, height: 254_000 });
  });

  it('prints a label at exactly its size, whatever its content measures', () => {
    expect(printPageSize(page('50mm 25mm'), 4000)).toEqual({ width: 50_000, height: 25_000 });
  });

  it('never asks for a side smaller than Electron accepts', () => {
    expect(printPageSize(page('0.1mm auto'), 0)).toEqual({ width: 353, height: 353 });
  });

  it('is null when the page names no width', () => {
    expect(printPageSize(page('A4 portrait'), 1000)).toBeNull();
  });
});

describe('layoutWidthPx', () => {
  it('lays the page out at the paper width', () => {
    expect(layoutWidthPx(page('80mm auto'))).toBe(303);
    expect(layoutWidthPx('<p>plain</p>')).toBe(800);
  });
});
