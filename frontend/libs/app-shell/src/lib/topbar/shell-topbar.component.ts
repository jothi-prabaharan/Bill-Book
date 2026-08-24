import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, input, output, signal, ElementRef, HostListener } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SearchInputComponent } from '@bill-book/ui-components';
import { AuthService, AccessibleOrg } from '@bill-book/auth';

export interface DocGroupItem {
  label: string;
  code: string;
}

export interface DocGroup {
  name: string;
  docs: DocGroupItem[];
}

/**
 * 46px sticky bar (z-index: 6).
 * Contains searchable organization switcher dropdown, display-only FY tag,
 * and action group buttons (`New`, `Favourites`, `Help`, `Sign out`).
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-topbar',
  standalone: true,
  imports: [RouterLink, FormsModule, SearchInputComponent],
  templateUrl: './shell-topbar.component.html',
  styleUrl: './shell-topbar.component.scss',
})
export class ShellTopbarComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef);

  readonly financialYear = input<string>('FY 2026-27');
  readonly userDisplayName = input<string>('Praba');
  readonly userRoleName = input<string>('Owner');

  readonly organizationChange = output<string>();
  readonly quickAction = output<string>();
  readonly logout = output<void>();

  // Organization Switcher State
  readonly orgOpen = signal(false);
  readonly orgQuery = signal('');
  readonly allOrgs = signal<AccessibleOrg[]>([]);

  // New Transaction Popup State
  readonly newOpen = signal(false);

  // Favourites Popup State
  readonly favOpen = signal(false);

  readonly newGroups: DocGroup[] = [
    {
      name: 'Sales',
      docs: [
        { label: 'Invoice', code: 'INV' },
        { label: 'Sales order', code: 'SOR' },
        { label: 'Quote', code: 'QOT' },
        { label: 'Delivery challan', code: 'DLC' },
        { label: 'Credit note', code: 'CRN' },
        { label: 'POS sale', code: 'POS' },
      ],
    },
    {
      name: 'Purchase',
      docs: [
        { label: 'Bill', code: 'BIL' },
        { label: 'Purchase order', code: 'POR' },
        { label: 'Goods receipt', code: 'GRN' },
        { label: 'Debit note', code: 'DBN' },
      ],
    },
    {
      name: 'Banking',
      docs: [
        { label: 'Receive money', code: 'REC' },
        { label: 'Spend money', code: 'PAY' },
        { label: 'Transfer money', code: 'TRF' },
      ],
    },
  ];

  readonly currentOrgId = computed(() => localStorage.getItem('bb.orgId'));

  readonly currentOrgName = computed(() => {
    return 'Eternal Pathway'; // Fallback company name since API lacks CustomerName
  });

  readonly currentOrgBranch = computed(() => {
    const orgs = this.allOrgs();
    const id = this.currentOrgId();
    const current = orgs.find((o: AccessibleOrg) => o.orgId === id);
    return current ? current.orgName : 'Head Office'; // API orgName is the Branch Name
  });

  readonly filteredOrgs = computed(() => {
    const query = this.orgQuery().toLowerCase();
    if (!query) return this.allOrgs();
    return this.allOrgs().filter(
      (o: AccessibleOrg) =>
        o.orgName.toLowerCase().includes(query) ||
        o.roleName.toLowerCase().includes(query),
    );
  });

  constructor() {
    void this.auth.accessibleOrganizations().then((orgs) => {
      this.allOrgs.set(orgs);
    });
  }

  toggleOrg(): void {
    this.orgOpen.update((v) => !v);
    if (this.orgOpen()) {
      this.orgQuery.set('');
    }
  }

  setOrgQuery(value: string): void {
    this.orgQuery.set(value);
  }

  async pickOrg(orgId: string): Promise<void> {
    if (orgId === this.currentOrgId()) {
      this.orgOpen.set(false);
      return;
    }
    await this.auth.switchOrganization(orgId);
    this.organizationChange.emit(orgId);
    this.orgOpen.set(false);
    try {
      if (typeof window !== 'undefined' && window.location && typeof window.location.reload === 'function') {
        window.location.reload();
      }
    } catch {
      // Ignored in non-browser/test environments
    }
  }

  openNew(): void {
    this.newOpen.set(true);
  }

  closeNew(): void {
    this.newOpen.set(false);
  }

  openFav(): void {
    this.favOpen.set(true);
  }

  closeFav(): void {
    this.favOpen.set(false);
  }

  selectDoc(code: string): void {
    this.quickAction.emit(code);
    this.closeNew();
  }

  @HostListener('document:click', ['$event.target'])
  onClickOutside(target: HTMLElement): void {
    if (this.orgOpen()) {
      const container = this.elementRef.nativeElement.querySelector('.org-dropdown-container');
      if (container && !container.contains(target)) {
        this.orgOpen.set(false);
      }
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.orgOpen.set(false);
    this.newOpen.set(false);
    this.favOpen.set(false);
  }

  doLogout(): void {
    this.logout.emit();
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}

