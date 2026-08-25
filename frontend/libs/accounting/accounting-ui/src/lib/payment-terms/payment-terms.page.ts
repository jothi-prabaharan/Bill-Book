import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

type TermType = 'DueOnReceipt' | 'Net' | 'EndOfMonth' | 'DayOfNextMonth';

interface PaymentTerm {
  paymentTermId: number;
  termSystemName: string | null;
  termName: string;
  termType: TermType;
  dueDays: number;
  dueDayOfMonth: number | null;
  discountPercent: number;
  discountDays: number;
  isSales: boolean;
  isPurchase: boolean;
  isDefault: boolean;
  isSystem: boolean;
  isActive: boolean;
  displayOrder: number;
  sampleDueDate: string;
}

type FormModel = Pick<
  PaymentTerm,
  | 'termName'
  | 'termType'
  | 'dueDays'
  | 'dueDayOfMonth'
  | 'discountPercent'
  | 'discountDays'
  | 'isSales'
  | 'isPurchase'
  | 'isActive'
>;

/**
 * Settings › Payment terms. Owned by Accounting because Sales and Purchase both
 * need it, so it cannot live inside either.
 *
 * The list shows the due date each term produces for a document dated today —
 * "Net 30" is unambiguous, but "End of Month" and "day of next month" are not,
 * and a worked example settles it faster than any label.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-payment-terms-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent, NumberInputComponent],
  templateUrl: './payment-terms.page.html',
  styleUrl: './payment-terms.page.scss',
})
export class PaymentTermsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<PaymentTerm[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editing = signal<number | null>(null);
  protected readonly formOpen = signal(false);
  protected readonly editingIsSystem = signal(false);

  columns: ColumnDef[] = [
    { field: 'handle', header: '', width: '40px' },
    { field: 'termName', header: 'Name' },
    { field: 'rule', header: 'Rule' },
    { field: 'sampleDueDate', header: 'Due for a bill dated today' },
    { field: 'discount', header: 'Discount' },
    { field: 'usedOn', header: 'Used on' },
    { field: 'default', header: 'Default' },
    { field: 'isActive', header: 'Active' },
    { field: 'actions', header: '' }
  ];

  showInactive = false;
  form: FormModel = this.blank();

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.get<PaymentTerm[]>(`/api/payment-terms?includeInactive=${this.showInactive}`),
      );
    } catch {
      this.fail('Could not load payment terms.');
    } finally {
      this.busy.set(false);
    }
  }

  openAdd(): void {
    this.editing.set(null);
    this.editingIsSystem.set(false);
    this.form = this.blank();
    this.formOpen.set(true);
  }

  openEdit(row: PaymentTerm): void {
    this.editing.set(row.paymentTermId);
    this.editingIsSystem.set(row.isSystem);
    this.form = {
      termName: row.termName,
      termType: row.termType,
      dueDays: row.dueDays,
      dueDayOfMonth: row.dueDayOfMonth,
      discountPercent: row.discountPercent,
      discountDays: row.discountDays,
      isSales: row.isSales,
      isPurchase: row.isPurchase,
      isActive: row.isActive,
    };
    this.formOpen.set(true);
  }

  onTermTypeChange(): void {
    if (this.form.termType === 'DueOnReceipt') {
      this.form.dueDays = 0;
    }

    this.form.dueDayOfMonth = this.form.termType === 'DayOfNextMonth' ? (this.form.dueDayOfMonth ?? 1) : null;
  }

  async save(): Promise<void> {
    if (!this.validateForm()) {
      return;
    }

    const body: FormModel = {
      ...this.form,
      termName: this.form.termName.trim(),
    };

    this.busy.set(true);
    try {
      const id = this.editing();
      if (id === null) {
        await this.send('POST', '/api/payment-terms', body);
      } else {
        await this.send('PUT', `/api/payment-terms/${id}`, body);
      }
      this.formOpen.set(false);
      this.succeed('Payment term saved.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not save that payment term.'));
    } finally {
      this.busy.set(false);
    }
  }

  async makeDefault(row: PaymentTerm): Promise<void> {
    await this.act(`/api/payment-terms/${row.paymentTermId}/default`, 'PUT', 'Default changed.');
  }

  async deactivate(row: PaymentTerm): Promise<void> {
    await this.act(`/api/payment-terms/${row.paymentTermId}`, 'DELETE', 'Payment term deactivated.');
  }

  async onDrop(event: CdkDragDrop<PaymentTerm[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    this.busy.set(true);
    try {
      await this.send('PATCH', '/api/payment-terms/reorder', {
        movedId: moved.paymentTermId,
        previousId: list[event.currentIndex - 1]?.paymentTermId ?? null,
        nextId: list[event.currentIndex + 1]?.paymentTermId ?? null,
      });
      await this.load();
    } catch {
      this.fail('Could not save the new order.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  protected get canReorder(): boolean {
    return !this.showInactive;
  }

  /** Plain-English summary of the rule, so the grid does not need four columns for it. */
  protected describe(row: PaymentTerm): string {
    switch (row.termType) {
      case 'DueOnReceipt':
        return 'Due immediately';
      case 'Net':
        return `${row.dueDays} days from the invoice date`;
      case 'EndOfMonth':
        return row.dueDays > 0
          ? `End of month, plus ${row.dueDays} days`
          : 'End of the invoice month';
      case 'DayOfNextMonth':
        return `Day ${row.dueDayOfMonth} of the following month`;
      default:
        return '';
    }
  }

  private validateForm(): boolean {
    if (!this.hasText(this.form.termName)) {
      this.fail('Term name is required.');
      return false;
    }

    if (!this.form.isSales && !this.form.isPurchase) {
      this.fail('Select Sales, Purchase, or both.');
      return false;
    }

    if (this.form.termType === 'DayOfNextMonth' && this.form.dueDayOfMonth === null) {
      this.fail('Day of next month is required for this term type.');
      return false;
    }

    if (this.form.termType === 'Net' && this.form.discountDays > this.form.dueDays) {
      this.fail('Discount days cannot be greater than due days for Net terms.');
      return false;
    }

    if (this.form.discountPercent > 0 && this.form.discountDays <= 0) {
      this.fail('Discount days are required when discount percent is greater than zero.');
      return false;
    }

    return true;
  }

  private blank(): FormModel {
    return {
      termName: '',
      termType: 'Net',
      dueDays: 30,
      dueDayOfMonth: null,
      discountPercent: 0,
      discountDays: 0,
      isSales: true,
      isPurchase: true,
      isActive: true,
    };
  }

  private async act(url: string, method: 'PUT' | 'DELETE', ok: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.send(method, url, {});
      this.succeed(ok);
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'That did not work.'));
    } finally {
      this.busy.set(false);
    }
  }

  private succeed(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private messageOf(err: unknown, fallback: string): string {
    const anyErr = err as { error?: { message?: string } };
    return anyErr?.error?.message ?? fallback;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}

