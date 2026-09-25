import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ActClaimApproval,
  ClaimsApiService,
  ExpenseClaimView,
  PayoutClaim,
} from '@bill-book/claims-core';

@Component({
  standalone: true,
  selector: 'bb-claim-detail',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './claim.page.html',
  styleUrl: '../claims-page.scss',
})
export class ClaimDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ClaimsApiService);

  readonly loading = signal(false);
  readonly claim = signal<ExpenseClaimView | null>(null);
  readonly showApprovalModal = signal(false);
  readonly showPayoutModal = signal(false);

  approvalForm: ActClaimApproval = {
    action: 'Approve',
    comments: '',
  };

  payoutForm: PayoutClaim = {
    payoutMode: 'Direct',
    bankAccountId: 1,
    paymentReference: '',
  };

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      void this.load(id);
    }
  }

  async load(id: number): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.api.getClaim(id);
      this.claim.set(data);
      if (data.payoutMode) {
        this.payoutForm.payoutMode = data.payoutMode;
      }
    } catch {
      // Handled
    } finally {
      this.loading.set(false);
    }
  }

  async submitClaim(): Promise<void> {
    const c = this.claim();
    if (!c) return;
    this.loading.set(true);
    try {
      const updated = await this.api.submitClaim(c.expenseClaimId);
      this.claim.set(updated);
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Submit failed');
    } finally {
      this.loading.set(false);
    }
  }

  openApprovalModal(action: 'Approve' | 'Reject' | 'SendBack'): void {
    this.approvalForm = { action, comments: '' };
    this.showApprovalModal.set(true);
  }

  closeApprovalModal(): void {
    this.showApprovalModal.set(false);
  }

  async submitApproval(): Promise<void> {
    const c = this.claim();
    if (!c) return;
    this.loading.set(true);
    try {
      const updated = await this.api.actApproval(c.expenseClaimId, this.approvalForm);
      this.claim.set(updated);
      this.closeApprovalModal();
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Approval action failed');
    } finally {
      this.loading.set(false);
    }
  }

  openPayoutModal(): void {
    this.showPayoutModal.set(true);
  }

  closePayoutModal(): void {
    this.showPayoutModal.set(false);
  }

  async submitPayout(): Promise<void> {
    const c = this.claim();
    if (!c) return;
    this.loading.set(true);
    try {
      const updated = await this.api.payoutClaim(c.expenseClaimId, this.payoutForm);
      this.claim.set(updated);
      this.closePayoutModal();
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Payout failed');
    } finally {
      this.loading.set(false);
    }
  }
}
