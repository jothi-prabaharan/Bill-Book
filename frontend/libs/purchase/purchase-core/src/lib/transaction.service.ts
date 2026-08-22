import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PurchaseTransactionListItem } from './transaction.models';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private http = inject(HttpClient);

  list(type?: string, from?: string, to?: string): Observable<PurchaseTransactionListItem[]> {
    let params = new HttpParams();
    if (type) params = params.set('type', type);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    
    return this.http.get<PurchaseTransactionListItem[]>('/api/purchase/transactions', { params });
  }
}
