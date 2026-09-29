import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/** What reconciling one supplier invoice found (TK-116). */
export type Gstr2bMatchState = 'Matched' | 'Mismatched' | 'MissingInBooks' | 'MissingIn2b';

export interface Gstr2bImportView {
  gstr2bImportId: number;
  returnPeriod: string;
  periodFrom: string;
  periodTo: string;
  fileName: string;
  importedAt: string | null;
  matchedCount: number;
  mismatchedCount: number;
  missingInBooksCount: number;
  missingIn2bCount: number;
  /** Credit claimed in the books that 2B does not support. */
  itcAtRisk: number;
}

export interface Gstr2bLineView {
  gstr2bLineId: number;
  matchState: Gstr2bMatchState;
  supplierGstin: string;
  supplierName: string | null;
  invoiceNo: string;
  invoiceDate: string;
  reverseCharge: boolean;
  itcAvailable: boolean | null;
  twoBTaxable: number | null;
  twoBTax: number | null;
  booksTaxable: number | null;
  booksTax: number | null;
  billId: number | null;
  billNo: string | null;
  note: string | null;
}

export interface Gstr2bImportDetail extends Gstr2bImportView {
  lines: Gstr2bLineView[];
}

/** GSTR-2B reconciliation (TK-116): import the portal's JSON and read what matching found. */
@Injectable({ providedIn: 'root' })
export class Gstr2bService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/purchase/gstr2b';

  list(): Promise<Gstr2bImportView[]> {
    return firstValueFrom(this.http.get<Gstr2bImportView[]>(this.apiUrl));
  }

  get(id: number): Promise<Gstr2bImportDetail> {
    return firstValueFrom(this.http.get<Gstr2bImportDetail>(`${this.apiUrl}/${id}`));
  }

  /** Uploads the file as multipart; the server answers with the reconciled import. */
  import(file: Blob, fileName: string): Promise<Gstr2bImportDetail> {
    const form = new FormData();
    form.append('file', file, fileName);
    return firstValueFrom(this.http.post<Gstr2bImportDetail>(this.apiUrl, form));
  }

  remove(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.apiUrl}/${id}`));
  }
}

/** The four groups in the order the screen shows them, with their headings. */
export const GSTR2B_GROUPS: { state: Gstr2bMatchState; label: string; hint: string }[] = [
  { state: 'MissingIn2b', label: 'In the books, not in 2B', hint: 'Credit at risk until the supplier reports these.' },
  { state: 'MissingInBooks', label: 'In 2B, not in the books', hint: 'Supplier invoices no bill records yet.' },
  { state: 'Mismatched', label: 'Mismatched', hint: 'In both, but the value, tax or date differs.' },
  { state: 'Matched', label: 'Matched', hint: 'In both and agreeing within a rupee.' },
];
