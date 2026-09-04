import { describe, expect, it } from 'vitest';
import { DEFAULT_FORMAT_SETTINGS, FormatSettings } from '@bill-book/currency-format';
import {
  AllocationRow,
  AllocationTarget,
  allocationMessages,
  canSubmit,
  decisionsFrom,
  isOverAllocated,
  remainingFor,
  totalAllocatedOf,
} from './allocation-modal.model';

/**
 * What the allocation modal decides, as opposed to what `bb-allocation-grid`
 * decides for it. The grid's per-keystroke clamping has its own suite; these
 * are the rules that only exist once a target document is involved.
 *
 * Asserted against the plain functions rather than a component fixture,
 * because signal inputs cannot be set from outside one and this workspace's
 * Vitest cannot compile a `templateUrl` component at all. That constraint is
 * why the logic lives in functions in the first place.
 */
describe('allocation modal rules', () => {
  const target: AllocationTarget = {
    transactionTypeCode: 'INV',
    transactionId: 42,
    documentNo: 'INV-2026-0042',
    documentDate: '2026-09-04',
    totalAmount: 10000,
    // Part-settled already: only 4,000 is left to claim.
    outstandingAmount: 4000,
  };

  const credit = (id: number, available: number, allocated = 0): AllocationRow => ({
    transactionTypeCode: 'CRN',
    transactionId: id,
    documentNo: `CRN-${id}`,
    documentDate: '2026-08-20',
    totalAmount: available,
    outstandingAmount: available,
    allocatedAmount: allocated,
  });

  describe('the cap', () => {
    it('is what the document still owes, not its face total', () => {
      // The distinction this modal exists to get right: a 10,000 invoice with
      // 6,000 already settled can only take 4,000 more. Capping on totalAmount
      // would let that 6,000 be claimed a second time.
      expect(remainingFor(target.outstandingAmount, [credit(1, 9000)])).toBe(4000);
      expect(remainingFor(target.totalAmount, [credit(1, 9000)])).toBe(10000);
    });

    it('reaches exactly zero when the balance is filled', () => {
      const rows = [credit(1, 9000, 4000)];
      expect(remainingFor(target.outstandingAmount, rows)).toBe(0);
      expect(isOverAllocated(target.outstandingAmount, rows)).toBe(false);
    });

    it('flags a claim beyond the balance', () => {
      const rows = [credit(1, 9000, 5000)];
      expect(isOverAllocated(target.outstandingAmount, rows)).toBe(true);
    });

    it('tolerates a rounding fraction below half a paisa', () => {
      // Four rounded rows can miss their target by less than the ledger stores.
      // Treating that as over-allocation disables Save for no visible reason.
      const rows = [credit(1, 9000, 4000.001)];
      expect(isOverAllocated(target.outstandingAmount, rows)).toBe(false);
    });

    it('sums across rows', () => {
      expect(totalAllocatedOf([credit(1, 3000, 1200), credit(2, 3000, 800)])).toBe(2000);
    });
  });

  describe('canSubmit', () => {
    it('refuses with nothing apportioned', () => {
      expect(canSubmit(target, [credit(1, 9000)], false)).toBe(false);
    });

    it('allows once something is apportioned', () => {
      expect(canSubmit(target, [credit(1, 9000, 4000)], false)).toBe(true);
    });

    it('refuses when over-allocated', () => {
      expect(canSubmit(target, [credit(1, 9000, 5000)], false)).toBe(false);
    });

    it('refuses while busy, however valid the rows', () => {
      expect(canSubmit(target, [credit(1, 9000, 4000)], true)).toBe(false);
    });

    it('refuses without a target', () => {
      expect(canSubmit(null, [credit(1, 9000, 4000)], false)).toBe(false);
    });
  });

  describe('decisionsFrom', () => {
    it('drops zero rows', () => {
      // Posting a zero allocation is a refusal from the API, not a no-op, so it
      // is dropped here rather than by every host that opens the modal.
      expect(decisionsFrom([credit(1, 3000, 2500), credit(2, 3000, 0)])).toEqual([
        { transactionTypeCode: 'CRN', transactionId: 1, amount: 2500 },
      ]);
    });

    it('carries the type code the API keys on', () => {
      const [decision] = decisionsFrom([credit(7, 500, 500)]);
      expect(decision.transactionTypeCode).toBe('CRN');
      expect(decision.transactionId).toBe(7);
    });

    it('emits nothing when every row is zero', () => {
      expect(decisionsFrom([credit(1, 3000), credit(2, 3000)])).toEqual([]);
    });
  });

  describe('allocationMessages', () => {
    it('says nothing before a submit', () => {
      // An error box shown while the user is still reading the rows is noise.
      expect(allocationMessages(target, [credit(1, 3000)], false, DEFAULT_FORMAT_SETTINGS)).toEqual(
        [],
      );
    });

    it('complains about an empty apportionment once submitted', () => {
      const messages = allocationMessages(
        target,
        [credit(1, 3000)],
        true,
        DEFAULT_FORMAT_SETTINGS,
      );
      expect(messages[0].tone).toBe('error');
      expect(messages[0].text).toContain('Nothing has been apportioned');
    });

    it('names the excess and the document, in the branch currency', () => {
      const messages = allocationMessages(
        target,
        [credit(1, 9000, 5000)],
        false,
        DEFAULT_FORMAT_SETTINGS,
      );
      expect(messages[0].text).toBe(
        'This claims ₹1,000.00 more than INV-2026-0042 still owes.',
      );
    });

    it('reports over-allocation without waiting for a submit', () => {
      // Unlike the empty case, this is about money rather than about the user
      // not having started — it is true the moment it is true.
      const messages = allocationMessages(
        target,
        [credit(1, 9000, 5000)],
        false,
        DEFAULT_FORMAT_SETTINGS,
      );
      expect(messages).toHaveLength(1);
    });

    it('formats the excess with the branch mask rather than a hardcoded one', () => {
      const western: FormatSettings = {
        ...DEFAULT_FORMAT_SETTINGS,
        currencySymbol: '$',
        currencyMask: '###,###,##0.00',
      };
      const messages = allocationMessages(
        { ...target, outstandingAmount: 0 },
        [credit(1, 9000000, 1234567)],
        false,
        western,
      );
      expect(messages[0].text).toContain('$1,234,567.00');
    });

    it('says nothing at all without a target', () => {
      expect(allocationMessages(null, [credit(1, 3000, 100)], true, DEFAULT_FORMAT_SETTINGS)).toEqual(
        [],
      );
    });
  });
});
