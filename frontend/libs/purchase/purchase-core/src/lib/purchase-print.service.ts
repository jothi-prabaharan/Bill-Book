import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The four purchase documents that print, named as their route and their API path name them. */
export type PurchasePrintKind = 'purchase-orders' | 'goods-receipts' | 'bills' | 'debit-notes';

/** A purchase document laid out by its print template, ready for the browser to print. */
export interface PrintedPurchaseDocument {
  /** The document's pages as HTML. */
  html: string;
  pageCount: number;
  /** Fields the template names that this document could not fill. They print as nothing. */
  unknownTags: string[];
  /** Null when the layout was the platform's own, not a stored template. */
  printTemplateId: number | null;
}

const LABELS: Readonly<Record<PurchasePrintKind, string>> = {
  'purchase-orders': 'purchase order',
  'goods-receipts': 'goods receipt',
  bills: 'bill',
  'debit-notes': 'debit note',
};

/** What the document is called in a sentence: "purchase order", "bill". */
export function purchasePrintLabel(kind: PurchasePrintKind): string {
  return LABELS[kind];
}

/** Where the document's print page is, for a Print button. */
export function purchasePrintRoute(kind: PurchasePrintKind, id: number): string[] {
  return ['/purchase', kind, String(id), 'print'];
}

/** Where the document itself is, for the print page's Back. */
export function purchaseDocumentRoute(kind: PurchasePrintKind, id: number): string[] {
  return ['/purchase', kind, String(id)];
}

/**
 * Prints a purchase order, goods receipt, bill or debit note through the branch's print template.
 * Purchase builds the document's data and Printing lays it out, so a change to the template in
 * Settings › Print templates changes every printed document. Needs `purchase.print`.
 */
@Injectable({ providedIn: 'root' })
export class PurchasePrintService {
  private readonly http = inject(HttpClient);

  print(kind: PurchasePrintKind, id: number): Promise<PrintedPurchaseDocument> {
    return firstValueFrom(this.http.get<PrintedPurchaseDocument>(`/api/purchase/${kind}/${id}/print`));
  }
}
