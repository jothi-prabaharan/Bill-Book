import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import {
  CurrencyInputComponent,
  DateInputComponent,
  NumberInputComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

interface CapitalizeAssetModel {
  fixedAssetCategoryId: number | null;
  assetCode: string;
  assetName: string;
  purchaseBillId: number | null;
  purchasePrice: number | null;
  purchaseDate: string;
}

/**
 * Capitalize an asset into the register.
 *
 * <b>The category, not the asset, carries the GL mapping</b> — Fixed Asset,
 * Accumulated Depreciation and Depreciation Expense all hang off it, which is
 * why the category is required here and why per-asset mapping was never
 * offered.
 *
 * Written against this workspace's own inputs rather than Ionic's, which the
 * first version imported and which has never been a dependency here.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fixed-asset-form',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    NumberInputComponent,
    CurrencyInputComponent,
    DateInputComponent,
  ],
  templateUrl: './fixed-asset-form.component.html',
  styleUrl: './fixed-asset-form.component.scss',
})
export class FixedAssetFormComponent {
  private readonly http = inject(HttpClient);

  /** True when something was actually capitalized, so the list knows to reload. */
  @Output() readonly closed = new EventEmitter<boolean>();

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected model: CapitalizeAssetModel = {
    fixedAssetCategoryId: null,
    assetCode: '',
    assetName: '',
    purchaseBillId: null,
    purchasePrice: null,
    purchaseDate: '',
  };

  protected close(): void {
    this.closed.emit(false);
  }

  protected async save(): Promise<void> {
    const invalid = this.firstProblem();

    if (invalid) {
      this.error.set(invalid);
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      await this.post('/api/accounting/fixed-assets/capitalize', this.model);
      this.closed.emit(true);
    } catch {
      this.error.set('The asset could not be capitalized. Try again.');
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * The first thing wrong, in the order the form reads. A rule about the
   * document goes to the message box; the field-level constraints are on the
   * inputs themselves.
   */
  private firstProblem(): string | null {
    if (!this.model.fixedAssetCategoryId) {
      return 'Choose the asset category — it decides which accounts this posts to.';
    }
    if (!this.model.assetCode.trim()) {
      return 'Enter an asset code.';
    }
    if (!this.model.assetName.trim()) {
      return 'Enter an asset name.';
    }
    if (!this.model.purchasePrice || this.model.purchasePrice <= 0) {
      return 'Enter what the asset cost.';
    }
    if (!this.model.purchaseDate) {
      return 'Enter the purchase date.';
    }

    return null;
  }

  private post(url: string, body: unknown): Promise<unknown> {
    return new Promise((resolve, reject) => {
      this.http.post(url, body).subscribe({ next: resolve, error: reject });
    });
  }
}
