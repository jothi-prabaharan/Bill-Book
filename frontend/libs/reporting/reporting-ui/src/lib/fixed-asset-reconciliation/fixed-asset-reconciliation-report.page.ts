import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  FixedAssetReconciliationAccountDto,
  FixedAssetReconciliationReportRequest,
  FixedAssetReconciliationReportResponse,
  FixedAssetReconciliationReportService,
} from '@bill-book/reporting-core';
import {
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate';

interface ComparisonRow {
  side: 'Register' | 'General Ledger' | 'Difference';
  opening: number;
  debits: number;
  credits: number;
  closing: number;
  isDiff?: boolean;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fixed-asset-reconciliation-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './fixed-asset-reconciliation-report.page.html',
  styleUrl: './fixed-asset-reconciliation-report.page.scss',
})
export class FixedAssetReconciliationReportPageComponent implements OnInit {
  private readonly service = inject(FixedAssetReconciliationReportService);

  protected readonly narrow = signal(false);

  private readonly initialRange = rangeForPreset('this-financial-year');

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.initialRange.from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<FixedAssetReconciliationReportResponse | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  protected readonly comparisonColumns: ColumnDef[] = [
    { field: 'side', header: 'Source / Side', width: '12rem' },
    { field: 'opening', header: 'Opening Balance', numeric: true, width: '10rem' },
    { field: 'debits', header: 'Debits (Additions)', numeric: true, width: '10rem' },
    { field: 'credits', header: 'Credits (Disposals/Dep)', numeric: true, width: '10rem' },
    { field: 'closing', header: 'Closing Balance', numeric: true, width: '11rem' },
  ];

  ngOnInit(): void {
    this.updateViewport();
    this.run();
  }

  @HostListener('window:resize')
  protected onResize(): void {
    this.updateViewport();
  }

  private updateViewport(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 800);
    }
  }

  protected err(field: FilterName, label: string): string {
    const apiErr = this.fieldErrors()[field];
    if (apiErr) {
      return apiErr;
    }
    const ctrl = this.filters.get(field);
    if (ctrl?.hasError('required') && ctrl.touched) {
      return `${label} is required.`;
    }
    return '';
  }

  protected run(): void {
    this.fieldErrors.set({});
    this.failure.set(null);

    const from = this.filters.value.fromDate;
    const to = this.filters.value.toDate;

    const errors: Partial<Record<FilterName, string>> = {};
    if (!from) {
      errors.fromDate = 'From date is required.';
    }
    if (!to) {
      errors.toDate = 'To date is required.';
    }
    if (from && to && to < from) {
      errors.toDate = 'To date cannot be earlier than From date.';
    }

    if (Object.keys(errors).length > 0) {
      this.fieldErrors.set(errors);
      return;
    }

    const req: FixedAssetReconciliationReportRequest = {
      from: from!,
      to: to!,
    };

    this.loading.set(true);
    this.service.getReport(req).subscribe({
      next: (res) => {
        this.reportData.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 400 && err.error?.errors) {
          const apiErrors: Partial<Record<FilterName, string>> = {};
          const keys = Object.keys(err.error.errors);
          for (const key of keys) {
            const lower = key.toLowerCase();
            if (lower.includes('from')) {
              apiErrors.fromDate = err.error.errors[key][0];
            } else if (lower.includes('to')) {
              apiErrors.toDate = err.error.errors[key][0];
            }
          }
          this.fieldErrors.set(apiErrors);
        } else {
          this.failure.set(err.error?.detail || err.error?.message || 'Failed to load fixed asset reconciliation report.');
        }
      },
    });
  }

  protected rowsForAccount(acc: FixedAssetReconciliationAccountDto): ComparisonRow[] {
    const isAccum = acc.accountType === 'AccumulatedDepreciation';

    const regOpening = isAccum ? acc.register.openingAccumDep : acc.register.openingCost;
    const regDebits = isAccum ? acc.register.accumDepDebits : acc.register.costDebits;
    const regCredits = isAccum ? acc.register.accumDepCredits : acc.register.costCredits;
    const regClosing = acc.register.closingBalance;

    const ledOpening = isAccum ? acc.ledger.openingAccumDep : acc.ledger.openingCost;
    const ledDebits = isAccum ? acc.ledger.accumDepDebits : acc.ledger.costDebits;
    const ledCredits = isAccum ? acc.ledger.accumDepCredits : acc.ledger.costCredits;
    const ledClosing = acc.ledger.closingBalance;

    return [
      {
        side: 'Register',
        opening: regOpening,
        debits: regDebits,
        credits: regCredits,
        closing: regClosing,
      },
      {
        side: 'General Ledger',
        opening: ledOpening,
        debits: ledDebits,
        credits: ledCredits,
        closing: ledClosing,
      },
      {
        side: 'Difference',
        opening: regOpening - ledOpening,
        debits: regDebits - ledDebits,
        credits: regCredits - ledCredits,
        closing: regClosing - ledClosing,
        isDiff: true,
      },
    ];
  }
}
