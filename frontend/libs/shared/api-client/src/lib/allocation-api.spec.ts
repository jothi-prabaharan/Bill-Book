import { describe, expect, it } from 'vitest';
import { OpenDocument, OpenDocuments, allocationPair, locateDocument } from './allocation-api.service';

/**
 * The one thing about allocation that is easy to get backwards and expensive
 * when you do.
 *
 * `GET /api/allocations/open-documents` splits a contact's documents by the
 * direction their control balance runs, not by what a person would call a
 * credit. An invoice is `Dr AR`, so it comes back as a **target**. A bill is
 * `Cr AP`, so it comes back as a **source**. The words invert between
 * receivables and payables, and the first cut of the purchase screen looked for
 * a bill among the targets — where it can never be — so every Allocate click on
 * a bill reported that it had nothing left to settle.
 *
 * The ordering also has to match `Accounts › Settle documents`, which takes its
 * source from `sources` and its target from `targets`. `POST /api/allocations`
 * replaces on the ordered pair, so two screens pairing the same two documents
 * opposite ways round would write two live rows for one settlement, neither
 * aware of the other's claim.
 */
describe('allocationPair', () => {
  const invoice = { transactionTypeCode: 'INV', transactionId: 42 };
  const creditNote = { transactionTypeCode: 'CRN', transactionId: 7 };
  const bill = { transactionTypeCode: 'BIL', transactionId: 90 };
  const debitNote = { transactionTypeCode: 'DBN', transactionId: 3 };

  it('puts a credit note on the source side of the invoice it settles', () => {
    // The invoice is the document being settled and sits on the target side,
    // because that is where open-documents lists it.
    expect(allocationPair('target', invoice, creditNote, 2500)).toMatchObject({
      sourceTransactionTypeCode: 'CRN',
      sourceTransactionId: 7,
      targetTransactionTypeCode: 'INV',
      targetTransactionId: 42,
      amount: 2500,
    });
  });

  it('puts a bill on the source side of the debit note that settles it', () => {
    // The mirror image, and the case the first cut got wrong: the bill is the
    // document being settled, yet it is the *source*.
    expect(allocationPair('source', bill, debitNote, 1500)).toMatchObject({
      sourceTransactionTypeCode: 'BIL',
      sourceTransactionId: 90,
      targetTransactionTypeCode: 'DBN',
      targetTransactionId: 3,
      amount: 1500,
    });
  });

  it('never puts the same document on both sides', () => {
    const pair = allocationPair('target', invoice, creditNote, 100);
    expect(pair.sourceTransactionTypeCode).not.toBe(pair.targetTransactionTypeCode);
  });

  it('carries the date and note through', () => {
    expect(allocationPair('target', invoice, creditNote, 100, '2026-09-01', 'agreed')).toMatchObject(
      { allocationDate: '2026-09-01', notes: 'agreed' },
    );
  });

  it('defaults the date and note to null rather than omitting them', () => {
    // The API treats an absent date as today; sending null says the same thing
    // explicitly rather than relying on a missing key.
    expect(allocationPair('source', bill, debitNote, 100)).toMatchObject({
      allocationDate: null,
      notes: null,
    });
  });
});

describe('locateDocument', () => {
  const doc = (code: string, id: number): OpenDocument => ({
    transactionTypeCode: code,
    transactionId: id,
    documentNo: `${code}-${id}`,
    documentDate: '2026-09-01',
    totalAmount: 1000,
    allocatedAmount: 0,
    unallocatedAmount: 1000,
    settlementStatus: 'Unallocated',
  });

  const open: OpenDocuments = {
    contactId: 5,
    // As the API splits them: debit balances are targets, credit balances sources.
    targets: [doc('INV', 42), doc('DBN', 3)],
    sources: [doc('CRN', 7), doc('BIL', 90)],
    totalOutstanding: 2000,
    totalAvailableCredit: 2000,
  };

  it('finds an invoice on the target side and offers the sources against it', () => {
    const found = locateDocument(open, 'INV', 42);
    expect(found?.side).toBe('target');
    expect(found?.counterparts.map((c) => c.transactionTypeCode)).toEqual(['CRN', 'BIL']);
  });

  it('finds a bill on the source side and offers the targets against it', () => {
    // The case the first cut of the purchase screen got wrong by assuming the
    // invoice screen's answer. Nothing here is per-screen: it is read off the
    // payload.
    const found = locateDocument(open, 'BIL', 90);
    expect(found?.side).toBe('source');
    expect(found?.counterparts.map((c) => c.transactionTypeCode)).toEqual(['INV', 'DBN']);
  });

  it('finds a credit note on the source side', () => {
    expect(locateDocument(open, 'CRN', 7)?.side).toBe('source');
  });

  it('returns null when the document has nothing open', () => {
    // Fully settled or never posted — something to tell the user, not an error.
    expect(locateDocument(open, 'INV', 999)).toBeNull();
  });

  it('does not confuse two documents sharing an id across types', () => {
    // INV-42 and a hypothetical BIL-42 are different documents; the type code is
    // half the key, and matching on id alone would settle the wrong one.
    const clash: OpenDocuments = { ...open, sources: [doc('BIL', 42)] };
    expect(locateDocument(clash, 'BIL', 42)?.side).toBe('source');
    expect(locateDocument(clash, 'INV', 42)?.side).toBe('target');
  });

  it('pairs correctly when fed straight into allocationPair', () => {
    // The two functions are meant to be used together: whatever side the
    // document was found on is the side it posts on.
    const bill = locateDocument(open, 'BIL', 90);
    const pair = allocationPair(bill!.side, bill!.document, bill!.counterparts[0], 500);

    expect(pair.sourceTransactionTypeCode).toBe('BIL');
    expect(pair.targetTransactionTypeCode).toBe('INV');
  });
});
