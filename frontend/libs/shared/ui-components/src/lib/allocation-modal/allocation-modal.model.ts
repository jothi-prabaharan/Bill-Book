import { FormatSettings, formatMoney } from '@bill-book/currency-format';
import { AllocationRow } from '../allocation-grid/allocation-grid.component';
import { UiMessage } from '../message-box/message-box.model';

/**
 * The document being settled — the invoice a credit note is applied to, the
 * bill a debit note corrects.
 *
 * `transactionTypeCode` is the three-letter code the ledger knows the document
 * by (`INV`, `BIL`, `CRN`), because that plus the id is the pair
 * `POST /api/allocations` takes. Carrying the code rather than inferring it
 * from which screen opened the modal is what lets one component serve both
 * `sal` and `pur`.
 */
export interface AllocationTarget {
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;

  /** What the document was raised for. Shown, never used as the cap. */
  totalAmount: number;

  /**
   * What is still unsettled, from the ledger. **This is the cap** — a document
   * part-paid last week can only take what is left, and the total on the face
   * of it would over-allocate by whatever has already been claimed.
   */
  outstandingAmount: number;
}

/** One allocation the user has decided on, ready to post. */
export interface AllocationDecision {
  sourceTransactionTypeCode: string;
  sourceTransactionId: number;
  amount: number;
}

/**
 * What the modal hands back on save: the target it was opened for, and every
 * credit the user actually apportioned against it.
 */
export interface AllocationSubmission {
  target: AllocationTarget;
  decisions: readonly AllocationDecision[];
  allocationDate: string | null;
  notes: string | null;
}

/** Re-exported so a host importing the modal need not reach for the grid too. */
export type { AllocationRow };

/**
 * Money compares badly at full precision: a sum of four rounded rows can miss
 * its target by a fraction of a paisa and no user can see why Save is disabled.
 * Half a paisa is below anything the ledger stores, so nothing real hides here.
 */
const EPSILON = 0.005;

/**
 * The decisions the modal makes, as plain functions rather than methods.
 *
 * **Not merely for tidiness.** Signal inputs cannot be set from outside a
 * component fixture, and a component with a `templateUrl` cannot be compiled by
 * this workspace's Vitest setup at all — so logic living in a method is logic
 * that cannot be tested here. `dominantTone` sits beside the message box for
 * the same reason. Everything below is the part worth asserting; the component
 * is the shell that wires it to inputs.
 */

/** What the rows apportion in total. */
export function totalAllocatedOf(rows: readonly AllocationRow[]): number {
  return rows.reduce((sum, row) => sum + (row.allocatedAmount || 0), 0);
}

/** What would still be owing on the target if these rows were posted. */
export function remainingFor(outstanding: number, rows: readonly AllocationRow[]): number {
  return outstanding - totalAllocatedOf(rows);
}

/**
 * Whether the rows claim more than the target still owes.
 *
 * `bb-allocation-grid` clamps every keystroke, so this should be unreachable —
 * which is exactly why it is checked rather than assumed. That clamp is the
 * only thing standing between a typo and a claim on money that is not there.
 */
export function isOverAllocated(outstanding: number, rows: readonly AllocationRow[]): boolean {
  return remainingFor(outstanding, rows) < -EPSILON;
}

/**
 * The allocations worth posting.
 *
 * Zero rows are dropped here rather than by each host: posting a zero
 * allocation is a refusal from the API, not a no-op, and every caller would
 * otherwise have to remember that.
 */
export function decisionsFrom(rows: readonly AllocationRow[]): AllocationDecision[] {
  return rows
    .filter((row) => (row.allocatedAmount || 0) > 0)
    .map((row) => ({
      sourceTransactionTypeCode: row.transactionTypeCode,
      sourceTransactionId: row.transactionId,
      amount: row.allocatedAmount,
    }));
}

/** Everything Save needs to be true before it is allowed. */
export function canSubmit(
  target: AllocationTarget | null,
  rows: readonly AllocationRow[],
  busy: boolean,
): boolean {
  if (busy || target === null) {
    return false;
  }

  return (
    totalAllocatedOf(rows) > 0 && !isOverAllocated(target.outstandingAmount, rows)
  );
}

/**
 * The rule errors this modal raises about the document as a whole.
 *
 * Field-level constraints belong on their fields; these are statements about
 * the allocation, which is why they end up in the shared message box. The
 * nothing-apportioned complaint waits for a submit — an error box shown while
 * someone is still reading the rows is noise.
 */
export function allocationMessages(
  target: AllocationTarget | null,
  rows: readonly AllocationRow[],
  submitted: boolean,
  formats: FormatSettings,
): UiMessage[] {
  if (target === null) {
    return [];
  }

  const messages: UiMessage[] = [];

  if (submitted && totalAllocatedOf(rows) <= 0) {
    messages.push({
      tone: 'error',
      text: 'Nothing has been apportioned yet. Enter an amount against at least one credit.',
    });
  }

  if (isOverAllocated(target.outstandingAmount, rows)) {
    const excess = Math.abs(remainingFor(target.outstandingAmount, rows));
    messages.push({
      tone: 'error',
      text: `This claims ${formatMoney(excess, formats)} more than ${
        target.documentNo
      } still owes.`,
    });
  }

  return messages;
}
