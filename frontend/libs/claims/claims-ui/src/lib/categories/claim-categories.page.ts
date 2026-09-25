import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ClaimCategoryView,
  ClaimLimitView,
  ClaimsApiService,
  CreateClaimCategory,
  SaveClaimLimit,
} from '@bill-book/claims-core';

@Component({
  standalone: true,
  selector: 'bb-claim-categories',
  imports: [CommonModule, FormsModule],
  templateUrl: './claim-categories.page.html',
  styleUrl: '../claims-page.scss',
})
export class ClaimCategoriesPage implements OnInit {
  private readonly api = inject(ClaimsApiService);

  readonly loading = signal(false);
  readonly categories = signal<ClaimCategoryView[]>([]);
  readonly limits = signal<ClaimLimitView[]>([]);
  readonly showCategoryForm = signal(false);
  readonly showLimitForm = signal(false);

  categoryForm: CreateClaimCategory = {
    code: '',
    name: '',
    isReceiptRequired: true,
    isTaxable: false,
    isActive: true,
  };

  limitForm: SaveClaimLimit = {
    claimCategoryId: 0,
    limitPeriod: 'PerClaim',
    amount: 1000,
  };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [cats, lims] = await Promise.all([
        this.api.categories(true),
        this.api.limits(),
      ]);
      this.categories.set(cats);
      this.limits.set(lims);
      if (cats.length > 0 && !this.limitForm.claimCategoryId) {
        this.limitForm.claimCategoryId = cats[0].claimCategoryId;
      }
    } catch {
      // Handled by global interceptor / empty fallback
    } finally {
      this.loading.set(false);
    }
  }

  openCreateCategory(): void {
    this.categoryForm = {
      code: '',
      name: '',
      isReceiptRequired: true,
      isTaxable: false,
      isActive: true,
    };
    this.showCategoryForm.set(true);
  }

  closeCategoryForm(): void {
    this.showCategoryForm.set(false);
  }

  async saveCategory(): Promise<void> {
    if (!this.categoryForm.code || !this.categoryForm.name) return;
    this.loading.set(true);
    try {
      await this.api.createCategory(this.categoryForm);
      this.closeCategoryForm();
      await this.load();
    } finally {
      this.loading.set(false);
    }
  }

  openCreateLimit(categoryId?: number): void {
    if (categoryId) this.limitForm.claimCategoryId = categoryId;
    this.showLimitForm.set(true);
  }

  closeLimitForm(): void {
    this.showLimitForm.set(false);
  }

  async saveLimit(): Promise<void> {
    if (!this.limitForm.claimCategoryId || this.limitForm.amount <= 0) return;
    this.loading.set(true);
    try {
      await this.api.saveLimit(this.limitForm);
      this.closeLimitForm();
      await this.load();
    } finally {
      this.loading.set(false);
    }
  }

  async deleteLimit(limitId: number): Promise<void> {
    if (!confirm('Are you sure you want to delete this limit?')) return;
    this.loading.set(true);
    try {
      await this.api.deleteLimit(limitId);
      await this.load();
    } finally {
      this.loading.set(false);
    }
  }
}
