import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface BankCardAccount {
  id: string;
  name: string;
  number: string;
  ccy: string;
  sym: string;
  tag: string;
  kind: string;
  bal: string;
  stmtBal: string;
  baseBal?: string;
  baseStmtBal?: string;
  rate?: string;
  rateOn?: string;
  book: string;
  stmt: string;
  meta: string;
  fromLabel: string;
  toLabel: string;
}

export interface CardMenuGroup {
  name: string;
  items: { label: string; action: (acct: BankCardAccount) => void }[];
}

interface BankAccountApiResponse {
  bankAccountId: number;
  bankName?: string;
  accountName: string;
  accountNumber: string;
  currencyCode: string;
  accountType?: string;
  isDefault?: boolean;
  isActive?: boolean;
  isLedgerLinked?: boolean;
}

const DEFAULT_ACCOUNTS: BankCardAccount[] = [
  {
    id: 'hdfc',
    name: 'HDFC · Current 4471',
    number: '004471000149',
    ccy: 'INR',
    sym: '₹',
    tag: '3 to reconcile',
    kind: 'tag-accent',
    bal: '3,42,180',
    stmtBal: '3,29,640',
    book: '8,46 50,38 92,42 134,26 176,20 212,12',
    stmt: '8,52 50,45 92,47 134,35 176,29 212,23',
    meta: '₹12,540 unreconciled · imported 10 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'icici',
    name: 'ICICI · Current 9032',
    number: '903200088142',
    ccy: 'INR',
    sym: '₹',
    tag: '2 to reconcile',
    kind: 'tag-accent',
    bal: '1,86,540',
    stmtBal: '1,84,900',
    book: '8,30 50,38 92,33 134,44 176,36 212,41',
    stmt: '8,34 50,41 92,37 134,47 176,41 212,44',
    meta: '₹1,640 unreconciled · imported 10 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'hsbc',
    name: 'HSBC · EEFC 7702',
    number: '770200914004',
    ccy: 'USD',
    sym: '$',
    tag: '2 to reconcile',
    kind: 'tag-accent',
    bal: '28,450.00',
    stmtBal: '27,980.00',
    baseBal: '24,71,742',
    baseStmtBal: '24,30,904',
    rate: '86.88',
    rateOn: '11 Aug 2026',
    book: '8,42 50,36 92,40 134,28 176,24 212,18',
    stmt: '8,47 50,42 92,45 134,34 176,31 212,26',
    meta: '$470.00 unreconciled · imported 11 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · USD',
  },
  {
    id: 'adcb',
    name: 'ADCB · Dubai 5518',
    number: '551800230117',
    ccy: 'AED',
    sym: 'د.إ',
    tag: 'Reconciled',
    kind: 'tag-neutral',
    bal: '96,300.00',
    stmtBal: '96,300.00',
    baseBal: '22,47,642',
    baseStmtBal: '22,47,642',
    rate: '23.34',
    rateOn: '11 Aug 2026',
    book: '8,36 50,40 92,31 134,35 176,26 212,29',
    stmt: '8,37 50,41 92,32 134,36 176,27 212,30',
    meta: 'No difference · imported 11 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · AED',
  },
  {
    id: 'kotak',
    name: 'Kotak · OD 2210',
    number: '221000640233',
    ccy: 'INR',
    sym: '₹',
    tag: '2 to reconcile',
    kind: 'tag-accent',
    bal: '52,900',
    stmtBal: '47,300',
    book: '8,20 50,28 92,38 134,36 176,48 212,54',
    stmt: '8,25 50,31 92,43 134,40 176,52 212,59',
    meta: '₹5,600 unreconciled · imported 09 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'spm',
    name: 'SPM · Sweep 6620',
    number: '662001180450',
    ccy: 'INR',
    sym: '₹',
    tag: '4 to reconcile',
    kind: 'tag-accent',
    bal: '2,14,600',
    stmtBal: '2,06,900',
    book: '8,52 50,44 92,47 134,33 176,28 212,21',
    stmt: '8,57 50,50 92,52 134,41 176,36 212,31',
    meta: '₹7,700 unreconciled · imported 10 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'rcm',
    name: 'RCM · Collections 8145',
    number: '814500927716',
    ccy: 'INR',
    sym: '₹',
    tag: '1 to reconcile',
    kind: 'tag-accent',
    bal: '1,42,380',
    stmtBal: '1,41,020',
    book: '8,24 50,32 92,28 134,37 176,31 212,38',
    stmt: '8,27 50,35 92,31 134,40 176,34 212,40',
    meta: '₹1,360 unreconciled · imported 11 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'stmt',
    name: 'Statement account · 3307',
    number: '330700411285',
    ccy: 'INR',
    sym: '₹',
    tag: 'Reconciled',
    kind: 'tag-neutral',
    bal: '88,240',
    stmtBal: '88,240',
    book: '8,40 50,43 92,36 134,42 176,34 212,37',
    stmt: '8,41 50,44 92,37 134,43 176,35 212,38',
    meta: 'No difference · imported 05 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
  {
    id: 'till',
    name: 'Till cash',
    number: 'CASH-0001',
    ccy: 'INR',
    sym: '₹',
    tag: 'Reconciled',
    kind: 'tag-neutral',
    bal: '26,700',
    stmtBal: '26,700',
    book: '8,48 50,46 92,45 134,44 176,46 212,45',
    stmt: '8,48 50,46 92,45 134,44 176,46 212,45',
    meta: 'Cash physically verified · 11 Aug 2026',
    fromLabel: 'Mar',
    toLabel: 'Aug · INR',
  },
];

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './bank-dashboard.page.html',
  styleUrl: './bank-dashboard.page.scss',
})
export class BankDashboardPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly base = signal(false);
  readonly baseLabel = computed(() => (this.base() ? 'Showing base INR' : 'Show base INR'));
  readonly openMenuId = signal<string | null>(null);
  readonly accounts = signal<BankCardAccount[]>(DEFAULT_ACCOUNTS);

  readonly menuGroups: CardMenuGroup[] = [
    {
      name: 'Manage',
      items: [
        { label: 'Edit account details', action: () => { void this.router.navigate(['/banking/accounts']); } },
        { label: 'Deactivate account', action: () => { void this.router.navigate(['/banking/accounts']); } },
        { label: 'Delete account', action: () => { void this.router.navigate(['/banking/accounts']); } },
      ],
    },
    {
      name: 'Find',
      items: [
        { label: 'Account transactions', action: () => { void this.router.navigate(['/accounting/ledger']); } },
        { label: 'Bank statements', action: () => { void this.router.navigate(['/banking/statements']); } },
        { label: 'Reconciliation report', action: () => { void this.router.navigate(['/reports/r-reconciliation-report']); } },
      ],
    },
    {
      name: 'Transact',
      items: [
        { label: 'Receive money', action: () => { void this.router.navigate(['/banking/receive-money']); } },
        { label: 'Spend money', action: () => { void this.router.navigate(['/banking/spend-money']); } },
        { label: 'Transfer money', action: () => { void this.router.navigate(['/banking/transfer-money']); } },
      ],
    },
    {
      name: 'Reconcile',
      items: [
        { label: 'Reconcile account', action: () => { void this.router.navigate(['/accounting/reconciliation']); } },
        { label: 'Bank rules', action: () => { void this.router.navigate(['/banking/statements']); } },
        { label: 'Import a statement', action: () => { void this.router.navigate(['/banking/statements']); } },
      ],
    },
  ];

  @HostListener('document:click')
  onDocumentClick(): void {
    this.openMenuId.set(null);
  }

  ngOnInit(): void {
    this.http.get<BankAccountApiResponse[]>('/api/bank-accounts').subscribe({
      next: (list) => {
        if (list && list.length > 0) {
          const mapped: BankCardAccount[] = list.map((item) => {
            const sym = item.currencyCode === 'USD' ? String.fromCharCode(36) : item.currencyCode === 'AED' ? 'د.إ' : item.currencyCode === 'EUR' ? '€' : item.currencyCode === 'GBP' ? '£' : '₹';
            const maskedNo = item.accountNumber && item.accountNumber.length > 4
              ? '••••' + item.accountNumber.slice(-4)
              : item.accountNumber || '—';
            return {
              id: String(item.bankAccountId),
              name: item.accountName,
              number: maskedNo,
              ccy: item.currencyCode || 'INR',
              sym,
              tag: item.isDefault ? 'Primary' : (item.isActive ? 'Active' : 'Inactive'),
              kind: item.isDefault ? 'tag-accent' : 'tag-neutral',
              bal: '0.00',
              stmtBal: '0.00',
              baseBal: item.currencyCode !== 'INR' ? '0.00' : undefined,
              baseStmtBal: item.currencyCode !== 'INR' ? '0.00' : undefined,
              book: '8,50 50,50 92,50 134,50 176,50 212,50',
              stmt: '8,50 50,50 92,50 134,50 176,50 212,50',
              meta: item.bankName ? (item.bankName + ' · ' + (item.accountType ?? 'Account')) : (item.accountType ?? 'Bank account'),
              fromLabel: 'Current',
              toLabel: item.currencyCode || 'INR',
            };
          });
          this.accounts.set(mapped);
        } else {
          this.accounts.set([]);
        }
      },
      error: () => {
        this.accounts.set([]);
      },
    });
  }

  toggleBase(): void {
    this.base.update((v) => !v);
  }

  toggleMenu(id: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenuId.update((cur) => (cur === id ? null : id));
  }

  closeMenu(): void {
    this.openMenuId.set(null);
  }

  onItemClick(item: { label: string; action: (acct: BankCardAccount) => void }, acct: BankCardAccount, event: MouseEvent): void {
    event.stopPropagation();
    this.closeMenu();
    item.action(acct);
  }

  formatBal(acct: BankCardAccount): string {
    const showBase = this.base() && !!acct.baseBal;
    const sym = showBase ? '₹' : acct.sym;
    const val = showBase ? acct.baseBal : acct.bal;
    return `${sym} ${val}`;
  }

  formatStmt(acct: BankCardAccount): string {
    const showBase = this.base() && !!acct.baseStmtBal;
    const sym = showBase ? '₹' : acct.sym;
    const val = showBase ? acct.baseStmtBal : acct.stmtBal;
    return `Statement ${sym} ${val}`;
  }

  formatSeriesLabel(acct: BankCardAccount): string {
    const showBase = this.base() && !!acct.baseBal;
    return showBase ? 'Book (INR)' : `Book (${acct.ccy})`;
  }

  formatMeta(acct: BankCardAccount): string {
    if (this.base() && acct.baseBal && acct.rate) {
      return `${acct.meta} · 1 ${acct.ccy} = ₹${acct.rate} on ${acct.rateOn}`;
    }
    return acct.meta;
  }
}
