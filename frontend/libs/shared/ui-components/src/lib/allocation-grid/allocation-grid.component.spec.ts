import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AllocationGridComponent, AllocationRow } from './allocation-grid.component';

function row(transactionId: number, outstanding: number, allocated = 0): AllocationRow {
  return {
    transactionId,
    documentNo: `INV-${transactionId}`,
    documentDate: '2026-08-01',
    totalAmount: outstanding,
    outstandingAmount: outstanding,
    allocatedAmount: allocated
  };
}

describe('AllocationGridComponent', () => {
  let comp: AllocationGridComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    comp = TestBed.runInInjectionContext(() => new AllocationGridComponent());
  });

  describe('Tier 1: Allocation math', () => {
    it('AGG-T1-01: totalAllocated and remainingToAllocate track the rows', () => {
      comp.rows = [row(1, 1000, 300), row(2, 500, 200)];
      comp.amountToAllocate = 1000;

      expect(comp.totalAllocated).toBe(500);
      expect(comp.remainingToAllocate).toBe(500);
    });

    it('AGG-T1-02: onAllocateChange clamps to the outstanding amount', () => {
      comp.rows = [row(1, 1000)];
      comp.amountToAllocate = 1000;

      comp.onAllocateChange(0, 1500);

      expect(comp.rows[0].allocatedAmount).toBe(1000);
    });

    it('AGG-T1-03: onAllocateChange clamps negative input to zero', () => {
      comp.rows = [row(1, 1000, 400)];
      comp.amountToAllocate = 1000;

      comp.onAllocateChange(0, -50);

      expect(comp.rows[0].allocatedAmount).toBe(0);
    });

    it('AGG-T1-04: onAllocateChange never claims past what remains of the amount', () => {
      comp.rows = [row(1, 1000, 600), row(2, 500)];
      comp.amountToAllocate = 1000;

      // 400 remains; a row cannot take 500 of it.
      comp.onAllocateChange(1, 500);

      expect(comp.rows[1].allocatedAmount).toBe(400);
      expect(comp.totalAllocated).toBe(1000);
    });
  });

  describe('Tier 2: Clamping when the amount shrinks', () => {
    it('AGG-T2-01: a shrinking amount trims the youngest rows first', () => {
      const rows = [row(1, 1000, 500), row(2, 1000, 400)];
      comp.rows = rows;
      comp.amountToAllocate = 900;

      expect(comp.totalAllocated).toBe(900);
      expect(comp.rows[0].allocatedAmount).toBe(500);
      expect(comp.rows[1].allocatedAmount).toBe(400);
    });

    it('AGG-T2-02: a shrinking amount cuts into the tail until the rows fit', () => {
      comp.rows = [row(1, 1000, 700), row(2, 1000, 500)];
      comp.amountToAllocate = 900;

      expect(comp.totalAllocated).toBe(900);
      expect(comp.rows[0].allocatedAmount).toBe(700);
      expect(comp.rows[1].allocatedAmount).toBe(200);
    });

    it('AGG-T2-03: the trim emits rowsChange so the parent stays in sync', () => {
      const changeSpy = vi.fn();
      comp.rows = [row(1, 1000, 800), row(2, 1000, 800)];
      comp.rowsChange.subscribe(changeSpy);
      comp.amountToAllocate = 1000;

      expect(changeSpy).toHaveBeenCalledTimes(1);
      expect(comp.rows[1].allocatedAmount).toBe(200);
    });

    it('AGG-T2-04: setting the same amount again does not re-emit', () => {
      const changeSpy = vi.fn();
      comp.rows = [row(1, 1000, 400)];
      comp.amountToAllocate = 1000;
      comp.rowsChange.subscribe(changeSpy);
      comp.amountToAllocate = 1000;

      expect(changeSpy).not.toHaveBeenCalled();
    });

    it('AGG-T2-05: a growing amount does not disturb existing allocations', () => {
      comp.rows = [row(1, 1000, 400)];
      comp.amountToAllocate = 500;
      comp.amountToAllocate = 800;

      expect(comp.rows[0].allocatedAmount).toBe(400);
    });
  });

  describe('Tier 3: Auto allocate', () => {
    it('AGG-T3-01: autoAllocate fills rows oldest-first up to the amount', () => {
      comp.rows = [row(1, 1000), row(2, 500)];
      comp.amountToAllocate = 1200;

      comp.autoAllocate();

      expect(comp.rows[0].allocatedAmount).toBe(1000);
      expect(comp.rows[1].allocatedAmount).toBe(200);
      expect(comp.totalAllocated).toBe(1200);
    });

    it('AGG-T3-02: autoAllocate with nothing to allocate clears every row', () => {
      comp.rows = [row(1, 1000, 500), row(2, 500, 500)];
      comp.amountToAllocate = 0;

      comp.autoAllocate();

      expect(comp.totalAllocated).toBe(0);
    });
  });
});