import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * A document that still owes money: what the ledger says a contact is owed,
 * per document. The outstanding report groups by (type, id) so a payment that
 * settled an invoice does not appear as money still owed.
 */
export interface OutstandingBalance {
  contactId: number;
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;
  dueDate?: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

@Injectable({
  providedIn: 'root'
})
export class LedgerService {
  private http = inject(HttpClient);

  /**
   * A contact's outstanding documents. Ledger type 3 is the CONTROL leg — the
   * AR/AP/bank control account — which is the balance an allocation reads.
   * Negative balances (advances, overpayments) come back too; a caller
   * allocating money has to filter for what it can actually settle.
   */
  outstandingBalances(
    contactId: number,
    ledgerTypeId = 3,
  ): Observable<OutstandingBalance[]> {
    return this.http.get<OutstandingBalance[]>(
      `/api/ledger/contacts/${contactId}/outstanding-balances/${ledgerTypeId}`,
    );
  }
}