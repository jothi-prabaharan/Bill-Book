import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DataGridComponent, ColumnDef , DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';

interface TaxRate {
  taxMasterId: number;
  taxGroupId: number;
  taxSystemName: string | null;
  taxName: string;
  totalRate: number;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  isSales: boolean;
  isPurchase: boolean;
  isActive: boolean;
  isCurrent: boolean;
  isSystemRate: boolean;
}

type Mode = 'create' | 'revise' | 'rename';

/**
 * GST rates. Rates are effective-dated: changing one supersedes it rather than
 * overwriting, so a document dated before the change still resolves the rate
 * that applied then. Renaming is separate, because it is display-only and
 * therefore allowed on seeded rates.
 */
@Component({
  selector: 'bb-tax-master-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './tax-master.page.html',
  styleUrl: './tax-master.page.scss',
})
export class TaxMasterPage implements OnInit {
  columns: ColumnDef[] = [
    { field: 'name', header: 'Name' },
    { field: 'total', header: 'Total' },
    { field: 'cgst', header: 'CGST' },
    { field: 'sgst', header: 'SGST' },
    { field: 'igst', header: 'IGST' },
    { field: 'appliesTo', header: 'Applies to' },
    { field: 'effective', header: 'Effective' },
    { field: 'actions', header: '' },
  ];
  private readonly http = inject(HttpClient);

  protected readonly rates = signal<TaxRate[]>([]);
  protected readonly mode = signal<Mode | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  showHistory = false;
  private editingId: number | null = null;

  form = {
    taxName: '',
    totalRate: 0,
    cessRate: 0,
    effectiveFrom: this.today(),
    isSales: true,
    isPurchase: true,
    isActive: true,
  };

  ngOnInit(): void {
    void this.load();
  }

  /** CGST and SGST are each half the total — shown live as the user types. */
  protected half(): number {
    return Math.round(((this.form.totalRate || 0) / 2) * 100) / 100;
  }

  protected recalc(): void {
    // The split is derived server-side too; this only mirrors it for the user.
  }

  protected valid(): boolean {
    if (this.mode() === 'rename') {
      return this.form.taxName.trim().length > 0;
    }

    return (
      this.form.taxName.trim().length > 0 &&
      this.form.effectiveFrom.length > 0 &&
      (this.form.isSales || this.form.isPurchase) &&
      this.form.totalRate >= 0
    );
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rates.set(
        await this.req<TaxRate[]>('GET', `/api/tax-masters?includeHistory=${this.showHistory}`),
      );
    } catch {
      this.show('Could not load tax rates.', true);
    } finally {
      this.busy.set(false);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.form = {
      taxName: '',
      totalRate: 0,
      cessRate: 0,
      effectiveFrom: this.today(),
      isSales: true,
      isPurchase: true,
      isActive: true,
    };
    this.mode.set('create');
  }

  startRevise(rate: TaxRate): void {
    this.editingId = rate.taxMasterId;
    this.form = {
      taxName: rate.taxName,
      totalRate: rate.totalRate,
      cessRate: rate.cessRate,
      // Defaults to today; must be after the version being replaced.
      effectiveFrom: this.today(),
      isSales: rate.isSales,
      isPurchase: rate.isPurchase,
      isActive: rate.isActive,
    };
    this.mode.set('revise');
  }

  startRename(rate: TaxRate): void {
    this.editingId = rate.taxMasterId;
    this.form = { ...this.form, taxName: rate.taxName };
    this.mode.set('rename');
  }

  async save(): Promise<void> {
    if (!this.valid()) {
      this.show('Fill all mandatory fields before saving.', true);
      return;
    }

    this.busy.set(true);
    this.message.set(null);
    const body = { ...this.form, taxName: this.form.taxName.trim() };
    try {
      if (this.mode() === 'create') {
        await this.req('POST', '/api/tax-masters', body);
      } else if (this.mode() === 'revise') {
        await this.req('POST', `/api/tax-masters/${this.editingId}/revise`, body);
      } else {
        await this.req('PUT', `/api/tax-masters/${this.editingId}/name`, {
          taxName: body.taxName,
        });
      }

      this.mode.set(null);
      this.show('Saved.', false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not save the rate.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(rate: TaxRate): Promise<void> {
    if (!confirm(`Deactivate "${rate.taxName}"? Its sub-accounts are deactivated too.`)) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/tax-masters/${rate.taxMasterId}`);
      await this.load();
    } catch {
      this.show('Could not deactivate the rate.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
