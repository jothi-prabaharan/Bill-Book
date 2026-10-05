import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '@bill-book/auth';
import { BarcodeBurst, normaliseScan } from '@bill-book/scan-core';
import { isEditable } from '@bill-book/ui-components';

/** What Sales or Purchase answers for a printed document number. */
export interface DocumentNumberMatch {
  transactionType: string;
  transactionId: number;
}

const DOCUMENT_PATHS: Record<string, string> = {
  Invoice: '/sales/invoices',
  CreditNote: '/sales/credit-notes',
  DeliveryChallan: '/sales/delivery-challans',
  SalesOrder: '/sales/sales-orders',
  Quote: '/sales/quotes',
  Bill: '/purchase/bills',
  GoodsReceipt: '/purchase/goods-receipts',
  PurchaseOrder: '/purchase/purchase-orders',
  DebitNote: '/purchase/debit-notes',
};

/** The screen a matched document opens on, or null for a kind no screen shows. */
export function documentPath(match: DocumentNumberMatch | null | undefined): string | null {
  if (!match || !(match.transactionId > 0)) {
    return null;
  }
  const base = DOCUMENT_PATHS[match.transactionType];
  return base ? `${base}/${match.transactionId}` : null;
}

/**
 * Scan to open (TK-162): reading a document code printed by TK-157 opens that
 * document, from anywhere in the app.
 *
 * A scanner's burst is caught at the page while no field has focus — the same
 * rule as the till — unless a screen's own `bb-scan` took it first, which it
 * marks by preventing the Enter. The number is asked of Sales and Purchase, as
 * far as the user may view them; each answers only from the current branch.
 */
@Injectable({ providedIn: 'root' })
export class ScanToOpenService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly burst = new BarcodeBurst();

  /** Starts listening for scans at the page, until `destroyRef` ends. */
  listen(destroyRef: DestroyRef): void {
    const handler = (event: KeyboardEvent): void => this.onKeyDown(event);
    document.addEventListener('keydown', handler);
    destroyRef.onDestroy(() => document.removeEventListener('keydown', handler));
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.defaultPrevented || isEditable(event.target)) {
      this.burst.reset();
      return;
    }
    const code = this.burst.push(event.key, event.timeStamp);
    if (code) {
      event.preventDefault();
      void this.open(code);
    }
  }

  /** Opens the document a code names. False when no document in this branch has it. */
  async open(code: string): Promise<boolean> {
    const path = await this.find(code);
    if (!path) {
      return false;
    }
    await this.router.navigateByUrl(path);
    return true;
  }

  /** The screen for the document a code names, or null. */
  async find(code: string): Promise<string | null> {
    const no = normaliseScan(code);
    if (!no) {
      return null;
    }

    const query = `no=${encodeURIComponent(no)}`;
    const asks: Promise<DocumentNumberMatch | null>[] = [];
    if (this.auth.canView('sales')) {
      asks.push(this.ask(`/api/sales/transactions/by-number?${query}`));
    }
    if (this.auth.canView('purchase')) {
      asks.push(this.ask(`/api/purchase/transactions/by-number?${query}`));
    }

    for (const match of await Promise.all(asks)) {
      const path = documentPath(match);
      if (path) {
        return path;
      }
    }
    return null;
  }

  /** A 404 is "not one of ours"; a branch without that module refuses; either way, no match. */
  private async ask(url: string): Promise<DocumentNumberMatch | null> {
    try {
      return await firstValueFrom(this.http.get<DocumentNumberMatch>(url));
    } catch {
      return null;
    }
  }
}
