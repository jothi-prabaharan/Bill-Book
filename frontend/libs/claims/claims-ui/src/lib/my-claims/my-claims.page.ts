import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApplyClaimSelf,
  ClaimCategoryView,
  ClaimsApiService,
  ExpenseClaimView,
  SaveExpenseClaimLine,
} from '@bill-book/claims-core';

@Component({
  standalone: true,
  selector: 'bb-my-claims',
  imports: [CommonModule, FormsModule],
  templateUrl: './my-claims.page.html',
  styleUrl: '../claims-page.scss',
})
export class MyClaimsPage implements OnInit {
  private readonly api = inject(ClaimsApiService);

  readonly loading = signal(false);
  readonly myClaims = signal<ExpenseClaimView[]>([]);
  readonly categories = signal<ClaimCategoryView[]>([]);
  readonly showApplyModal = signal(false);

  applyForm: ApplyClaimSelf = {
    claimDate: new Date().toISOString().substring(0, 10),
    payoutMode: 'Direct',
    lines: [],
  };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [claims, cats] = await Promise.all([
        this.api.myClaims(),
        this.api.categories(),
      ]);
      this.myClaims.set(claims);
      this.categories.set(cats);
    } catch {
      // Empty fallback
    } finally {
      this.loading.set(false);
    }
  }

  openApply(): void {
    this.applyForm = {
      claimDate: new Date().toISOString().substring(0, 10),
      payoutMode: 'Direct',
      lines: [
        {
          claimCategoryId: this.categories()[0]?.claimCategoryId ?? 1,
          expenseDate: new Date().toISOString().substring(0, 10),
          description: '',
          amount: 0,
          receiptAttachmentKey: '',
        },
      ],
    };
    this.showApplyModal.set(true);
  }

  closeApply(): void {
    this.showApplyModal.set(false);
  }

  addLine(): void {
    this.applyForm.lines.push({
      claimCategoryId: this.categories()[0]?.claimCategoryId ?? 1,
      expenseDate: new Date().toISOString().substring(0, 10),
      description: '',
      amount: 0,
      receiptAttachmentKey: '',
    });
  }

  removeLine(index: number): void {
    if (this.applyForm.lines.length > 1) {
      this.applyForm.lines.splice(index, 1);
    }
  }

  async submitNewClaim(): Promise<void> {
    if (this.applyForm.lines.some((l) => l.amount <= 0 || !l.description)) {
      alert('Please fill out descriptions and amounts for all items.');
      return;
    }
    this.loading.set(true);
    try {
      const created = await this.api.applyMyClaim(this.applyForm);
      await this.api.submitMyClaim(created.expenseClaimId);
      this.closeApply();
      await this.load();
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Failed to submit claim');
    } finally {
      this.loading.set(false);
    }
  }
}
