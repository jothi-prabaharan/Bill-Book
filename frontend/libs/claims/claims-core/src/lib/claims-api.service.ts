import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplyClaimSelf,
  ActClaimApproval,
  ClaimCategoryView,
  ClaimLimitView,
  CreateClaimCategory,
  CreateExpenseClaim,
  ExpenseClaimView,
  PayoutClaim,
  SaveClaimLimit,
  UpdateExpenseClaim,
} from './claims.models';

@Injectable({ providedIn: 'root' })
export class ClaimsApiService {
  private readonly http = inject(HttpClient);

  // Categories
  categories(includeInactive = false): Promise<ClaimCategoryView[]> {
    return firstValueFrom(this.http.get<ClaimCategoryView[]>(`/api/clm/categories?includeInactive=${includeInactive}`));
  }

  getCategory(id: number): Promise<ClaimCategoryView> {
    return firstValueFrom(this.http.get<ClaimCategoryView>(`/api/clm/categories/${id}`));
  }

  createCategory(body: CreateClaimCategory): Promise<ClaimCategoryView> {
    return firstValueFrom(this.http.post<ClaimCategoryView>('/api/clm/categories', body));
  }

  updateCategory(id: number, body: CreateClaimCategory): Promise<ClaimCategoryView> {
    return firstValueFrom(this.http.put<ClaimCategoryView>(`/api/clm/categories/${id}`, body));
  }

  // Limits
  limits(categoryId?: number): Promise<ClaimLimitView[]> {
    const url = categoryId ? `/api/clm/limits?categoryId=${categoryId}` : '/api/clm/limits';
    return firstValueFrom(this.http.get<ClaimLimitView[]>(url));
  }

  saveLimit(body: SaveClaimLimit): Promise<ClaimLimitView> {
    return firstValueFrom(this.http.post<ClaimLimitView>('/api/clm/limits', body));
  }

  deleteLimit(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/clm/limits/${id}`));
  }

  // Claims
  claims(employeeId?: number, status?: string): Promise<ExpenseClaimView[]> {
    const params = new URLSearchParams();
    if (employeeId) params.set('employeeId', employeeId.toString());
    if (status) params.set('status', status);
    const qs = params.toString();
    return firstValueFrom(this.http.get<ExpenseClaimView[]>(`/api/clm/claims${qs ? '?' + qs : ''}`));
  }

  getClaim(id: number): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.get<ExpenseClaimView>(`/api/clm/claims/${id}`));
  }

  createClaim(body: CreateExpenseClaim): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>('/api/clm/claims', body));
  }

  updateClaim(id: number, body: UpdateExpenseClaim): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.put<ExpenseClaimView>(`/api/clm/claims/${id}`, body));
  }

  deleteClaim(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/clm/claims/${id}`));
  }

  submitClaim(id: number): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>(`/api/clm/claims/${id}/submit`, {}));
  }

  payoutClaim(id: number, body: PayoutClaim): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>(`/api/clm/claims/${id}/payout`, body));
  }

  actApproval(claimId: number, body: ActClaimApproval): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>(`/api/clm/approvals/${claimId}/act`, body));
  }

  // Self-Service
  myClaims(): Promise<ExpenseClaimView[]> {
    return firstValueFrom(this.http.get<ExpenseClaimView[]>('/api/me/claims'));
  }

  getMyClaim(id: number): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.get<ExpenseClaimView>(`/api/me/claims/${id}`));
  }

  applyMyClaim(body: ApplyClaimSelf): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>('/api/me/claims', body));
  }

  submitMyClaim(id: number): Promise<ExpenseClaimView> {
    return firstValueFrom(this.http.post<ExpenseClaimView>(`/api/me/claims/${id}/submit`, {}));
  }
}
