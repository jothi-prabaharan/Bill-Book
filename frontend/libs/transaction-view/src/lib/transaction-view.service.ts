import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TransactionKind, transactionViewApiPath } from './transaction-kinds';
import { TransactionView } from './transaction-view.model';

/**
 * Reads a transaction laid out by its print template. The document's own service builds its data and Printing
 * lays it out under the caller's own token, so a change to the template in Settings › Print templates changes
 * how every document reads here. Needs the module's `view` permission, not its `print` one: reading a
 * document on screen is not handing it to somebody outside the business.
 */
@Injectable({ providedIn: 'root' })
export class TransactionViewService {
  private readonly http = inject(HttpClient);

  view(kind: TransactionKind, id: number): Promise<TransactionView> {
    return firstValueFrom(this.http.get<TransactionView>(transactionViewApiPath(kind, id)));
  }
}
