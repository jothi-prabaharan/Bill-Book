import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MonthlyUsage, PlanSummary, TransactionKind, UpgradeRequestInput } from './plan.model';

/**
 * The customer's plan (TK-209): what it allows the signed-in branch, and asking
 * for a higher one. There is no payment in the app; a request is recorded, and
 * a platform operator changes the plan.
 */
@Injectable({ providedIn: 'root' })
export class PlanService {
  private readonly http = inject(HttpClient);

  /** The plan and its limits, with the user places the branch has taken. */
  summary(): Promise<PlanSummary> {
    return firstValueFrom(this.http.get<PlanSummary>('/api/plan'));
  }

  /** What the signed-in branch has created of Sales or Purchase documents this month, against its plan's limit. */
  usage(kind: TransactionKind): Promise<MonthlyUsage> {
    return firstValueFrom(
      this.http.get<MonthlyUsage>(kind === 'PurchaseTransactions' ? '/api/purchase/usage' : '/api/sales/usage'),
    );
  }

  /** Asks for a higher plan. Resolves with the server's own words about what happens next. */
  async requestUpgrade(input: UpgradeRequestInput): Promise<string> {
    const answer = await firstValueFrom(this.http.post<{ message: string }>('/api/plan/upgrade-requests', input));
    return answer.message;
  }
}
