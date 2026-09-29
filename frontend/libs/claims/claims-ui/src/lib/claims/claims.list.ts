import { CommonModule } from '@angular/common';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MoneyInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ClaimCategoryView,
  ClaimStatus,
  ClaimsApiService,
  CreateExpenseClaim,
  ExpenseClaimView,
  SaveExpenseClaimLine,
} from '@bill-book/claims-core';

@Component({
  standalone: true,
  selector: 'bb-claims-list',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    DateInputComponent,
    MoneyInputComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './claims.list.html',
  styleUrl: '../claims-page.scss',
})
export class ClaimsListPage implements OnInit {
  protected readonly claimsColumns: ColumnDef[] = [
    { field: 'expenseClaimId', header: 'Claim No' },
    { field: 'employeeName', header: 'Employee' },
    { field: 'claimDate', header: 'Date' },
    { field: 'totalAmount', header: 'Total Amount', numeric: true },
    { field: 'payoutMode', header: 'Payout Mode' },
    { field: 'claimStatus', header: 'Status' },
    { field: 'currentStepLabel', header: 'Step / Approver' },
    { field: 'col7', header: 'Actions', sortable: false, filterable: false },
  ];

  protected readonly claimCategoryOptions = computed(() =>
    this.categories().map((cat) => ({ value: cat.claimCategoryId, label: `${cat.name}` })),
  );

  protected readonly payoutModeOptions = [
    { value: 'Direct', label: 'Direct (Accounting Spend Money)' },
    { value: 'Payroll', label: 'Payroll (Next Payroll Run)' },
  ];

  private readonly api = inject(ClaimsApiService);

  readonly loading = signal(false);
  readonly claims = signal<ExpenseClaimView[]>([]);
  readonly categories = signal<ClaimCategoryView[]>([]);
  readonly selectedStatus = signal<string>('');
  readonly showCreateModal = signal(false);

  newClaim: CreateExpenseClaim = {
    employeeId: 1,
    claimDate: new Date().toISOString().substring(0, 10),
    payoutMode: 'Direct',
    lines: [],
  };

  newLine: SaveExpenseClaimLine = {
    claimCategoryId: 0,
    expenseDate: new Date().toISOString().substring(0, 10),
    description: '',
    amount: 100,
    receiptAttachmentKey: '',
  };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [list, cats] = await Promise.all([
        this.api.claims(undefined, this.selectedStatus() || undefined),
        this.api.categories(),
      ]);
      this.claims.set(list);
      this.categories.set(cats);
      if (cats.length > 0 && !this.newLine.claimCategoryId) {
        this.newLine.claimCategoryId = cats[0].claimCategoryId;
      }
    } catch {
      // Empty fallback
    } finally {
      this.loading.set(false);
    }
  }

  onFilterStatus(status: string): void {
    this.selectedStatus.set(status);
    void this.load();
  }

  openCreate(): void {
    this.newClaim = {
      employeeId: 1,
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
    this.showCreateModal.set(true);
  }

  closeCreate(): void {
    this.showCreateModal.set(false);
  }

  addLine(): void {
    this.newClaim.lines.push({
      claimCategoryId: this.categories()[0]?.claimCategoryId ?? 1,
      expenseDate: new Date().toISOString().substring(0, 10),
      description: '',
      amount: 0,
      receiptAttachmentKey: '',
    });
  }

  removeLine(index: number): void {
    if (this.newClaim.lines.length > 1) {
      this.newClaim.lines.splice(index, 1);
    }
  }

  async saveClaim(): Promise<void> {
    if (this.newClaim.lines.some((l) => l.amount <= 0 || !l.description)) {
      alert('Please fill out descriptions and positive amounts for all lines.');
      return;
    }
    this.loading.set(true);
    try {
      await this.api.createClaim(this.newClaim);
      this.closeCreate();
      await this.load();
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Failed to create claim');
    } finally {
      this.loading.set(false);
    }
  }

  async submitClaim(id: number): Promise<void> {
    this.loading.set(true);
    try {
      await this.api.submitClaim(id);
      await this.load();
    } catch (e: any) {
      alert(e?.error?.message || e?.message || 'Submit failed');
    } finally {
      this.loading.set(false);
    }
  }
}
