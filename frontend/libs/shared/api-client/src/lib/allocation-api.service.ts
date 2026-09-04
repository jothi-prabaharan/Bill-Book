import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

/** One side of a contact's settlement position, as `GET /api/allocations/open-documents/{contactId}` returns it. */
export interface OpenDocument {
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;
  totalAmount: number;
  allocatedAmount: number;

  /** What is still free — the most this document can take or give. */
  unallocatedAmount: number;
  settlementStatus: 'Unallocated' | 'PartiallyPaid' | 'Paid';
}

/**
 * Both halves of a contact's position: `sources` are the credits available to
 * apply, `targets` the balances waiting to be settled. Split by the direction
 * the control balance runs rather than by document type.
 */
export interface OpenDocuments {
  contactId: number;
  sources: OpenDocument[];
  targets: OpenDocument[];
  totalOutstanding: number;
  totalAvailableCredit: number;
}

/** The body `POST /api/allocations` takes. */
export interface CreateAllocation {
  sourceTransactionTypeCode: string;
  sourceTransactionId: number;
  targetTransactionTypeCode: string;
  targetTransactionId: number;
  amount: number;
  allocationDate?: string | null;
  notes?: string | null;
}

/** One end of an allocation, named the way the API keys on it. */
export interface AllocationEnd {
  transactionTypeCode: string;
  transactionId: number;
}

/**
 * Which side of the ledger the document being settled sits on.
 *
 * `open-documents` splits by the direction the control balance runs, not by
 * what a user would call a credit: an invoice is `Dr AR`, so it is a
 * **target**; a bill is `Cr AP`, so it is a **source**. The words invert
 * between receivables and payables, which is exactly why this is named and
 * passed in rather than assumed.
 */
export type LedgerSide = 'source' | 'target';

/**
 * The body to post for one claim, with the two ends put on the right sides.
 *
 * **The order is not cosmetic.** The API replaces on the ordered pair
 * (source, target), so posting a bill-and-its-debit-note one way round and the
 * settlement workspace posting it the other would write two live rows for one
 * economic fact, each unaware of the other. The workspace takes its source from
 * `sources` and its target from `targets`; this keeps every caller to that same
 * convention.
 */
export function allocationPair(
  documentSide: LedgerSide,
  document: AllocationEnd,
  counterpart: AllocationEnd,
  amount: number,
  allocationDate: string | null = null,
  notes: string | null = null,
): CreateAllocation {
  const source = documentSide === 'source' ? document : counterpart;
  const target = documentSide === 'source' ? counterpart : document;

  return {
    sourceTransactionTypeCode: source.transactionTypeCode,
    sourceTransactionId: source.transactionId,
    targetTransactionTypeCode: target.transactionTypeCode,
    targetTransactionId: target.transactionId,
    amount,
    allocationDate,
    notes,
  };
}

/** Where a document turned out to sit, and what it can be settled against. */
export interface LocatedDocument {
  side: LedgerSide;
  document: OpenDocument;

  /** Everything on the opposite side — the credits this document can take, or the debts it can cover. */
  counterparts: OpenDocument[];
}

/**
 * Finds a document in whichever half of `open-documents` it landed in.
 *
 * <b>Derived rather than declared, because declaring it is what went wrong.</b>
 * The first cut of the purchase screen hardcoded "look in targets" from the
 * invoice screen it was copied from, and a bill is never in targets — so every
 * Allocate click reported nothing left to settle. The rule is not per screen
 * and not per document type; it is simply the direction that document's control
 * balance happens to run, which is a fact the payload already carries. Reading
 * it off the payload means a screen cannot hold a stale opinion about it, and
 * the counterparts come back from the opposite side by construction.
 *
 * Returns null when the document has nothing open — fully settled, or never
 * posted — which is a thing to tell the user rather than an error.
 */
export function locateDocument(
  open: OpenDocuments,
  transactionTypeCode: string,
  transactionId: number,
): LocatedDocument | null {
  const matches = (doc: OpenDocument): boolean =>
    doc.transactionTypeCode === transactionTypeCode && doc.transactionId === transactionId;

  const asTarget = open.targets.find(matches);
  if (asTarget) {
    return { side: 'target', document: asTarget, counterparts: open.sources };
  }

  const asSource = open.sources.find(matches);
  if (asSource) {
    return { side: 'source', document: asSource, counterparts: open.targets };
  }

  return null;
}

/**
 * The client behind the allocation modal, shared by `sal` and `pur`.
 *
 * **Here rather than duplicated into each module's `-core`.** Both sides read
 * the same Accounting endpoints with the same payloads, and a service that
 * posts money allocations is the last thing worth keeping two copies of — a fix
 * to one that misses the other is a settlement bug in exactly half the product.
 * It sits in `api-client` rather than in `accounting-core` so neither
 * `sales-ui` nor `purchase-ui` has to depend on another module to settle a
 * document.
 */
@Injectable({ providedIn: 'root' })
export class AllocationApiService {
  private readonly http = inject(HttpClient);

  /** What a contact has open on both sides. */
  async openDocuments(contactId: number): Promise<OpenDocuments> {
    return new Promise<OpenDocuments>((resolve, reject) => {
      this.http
        .get<OpenDocuments>(`/api/allocations/open-documents/${contactId}`)
        .subscribe({ next: resolve, error: reject });
    });
  }

  /**
   * Posts one allocation.
   *
   * **One call per credit, deliberately sequential at the call site.** The API
   * keys on (source, target) and replaces rather than appends, so a retry after
   * a dropped response is safe — but each claim is checked against what is left
   * at the moment it lands, and firing them in parallel makes them race each
   * other for the same remaining balance.
   */
  async allocate(request: CreateAllocation): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      this.http.post<void>('/api/allocations', request).subscribe({
        next: () => resolve(),
        error: reject,
      });
    });
  }
}
