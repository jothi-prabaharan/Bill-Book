import { Component, computed, inject, signal, ElementRef, HostListener } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, AccessibleOrg } from '@bill-book/auth';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  /**
   * The module this entry leads to, or null for one every role can reach.
   * Drawn only when the user holds `{module}.view`.
   */
  module: string | null;
}

/**
 * Teams-style shell. Desktop (≥768px): far-left icon rail + main area.
 * Mobile: bottom tab bar with the top 4 modules + "More" — the Teams-mobile
 * pattern. Same nav model both ways; only the chrome changes.
 */
@Component({
  selector: 'bb-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef);

  private readonly all: NavItem[] = [
    { path: '/dashboard', label: 'Home', icon: 'home', module: null },
    { path: '/contacts', label: 'Contacts', icon: 'contacts', module: 'contacts' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory', module: 'inventory' },
    { path: '/purchase', label: 'Purchase', icon: 'purchase', module: 'purchase' },
    { path: '/sales', label: 'Sales', icon: 'sales', module: 'sales' },
    { path: '/banking', label: 'Banking', icon: 'banking', module: 'banking' },
    { path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' },
    { path: '/reports', label: 'Reports', icon: 'reports', module: 'reports' },
    { path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
  ];

  /**
   * What this user can actually open. Offering a screen that answers 403 is a
   * menu lying about the product; the server was already refusing these, so
   * this only changes what is drawn.
   */
  readonly nav = computed(() =>
    this.all.filter((item) => item.module === null || this.auth.canView(item.module)),
  );

  // Organization Switcher State
  orgOpen = signal(false);
  orgQuery = signal('');
  private readonly allOrgs = signal<AccessibleOrg[]>([]);

  // New Transaction Popup State
  newOpen = signal(false);
  
  // Favourites Popup State
  favOpen = signal(false);

  // Navigation / Crumbs State
  crumbs = signal<{label: string, isLink: boolean, isLast: boolean, path?: string}[]>([]);
  isHome = computed(() => this.router.url === '/dashboard' || this.router.url === '/');
  isRegister = computed(() => this.router.url.includes('/sales') || this.router.url.includes('/purchase') || this.router.url.includes('/inventory') || this.router.url.includes('/contacts'));
  
  // Dashboard Actions State (mock)
  base = signal(false);
  baseLabel = signal('Accrual basis');
  editing = signal(false);
  notEditing = computed(() => !this.editing());

  newGroups = [
    { name: 'Sales', docs: [
      { label: 'Invoice', code: 'INV' },
      { label: 'Sales order', code: 'SOR' },
      { label: 'Quote', code: 'QOT' },
      { label: 'Delivery challan', code: 'DLC' },
      { label: 'Credit note', code: 'CRN' },
      { label: 'POS sale', code: 'POS' }
    ] },
    { name: 'Purchase', docs: [
      { label: 'Bill', code: 'BIL' },
      { label: 'Purchase order', code: 'POR' },
      { label: 'Goods receipt', code: 'GRN' },
      { label: 'Debit note', code: 'DBN' }
    ] },
    { name: 'Banking', docs: [
      { label: 'Receive money', code: 'REC' },
      { label: 'Spend money', code: 'PAY' },
      { label: 'Transfer money', code: 'TRF' }
    ] }
  ];

  readonly currentOrgId = computed(() => localStorage.getItem('bb.orgId'));
  readonly currentOrgName = computed(() => {
    const orgs = this.allOrgs();
    const id = this.currentOrgId();
    const current = orgs.find((o: AccessibleOrg) => o.orgId === id);
    return current ? current.orgName : 'Eternal Pathway'; // fallback to mock if loading
  });
  readonly currentOrgRole = computed(() => {
    const orgs = this.allOrgs();
    const id = this.currentOrgId();
    const current = orgs.find((o: AccessibleOrg) => o.orgId === id);
    return current ? current.roleName : 'Head Office'; // fallback to mock if loading
  });

  readonly filteredOrgs = computed(() => {
    const query = this.orgQuery().toLowerCase();
    if (!query) return this.allOrgs();
    return this.allOrgs().filter((o: AccessibleOrg) => 
      o.orgName.toLowerCase().includes(query) || 
      o.roleName.toLowerCase().includes(query)
    );
  });

  constructor() {
    // Load organizations when shell boots
    this.auth.accessibleOrganizations().then(orgs => {
      this.allOrgs.set(orgs);
    });
  }

  toggleOrg(): void {
    this.orgOpen.update((v: boolean) => !v);
    if (this.orgOpen()) {
      this.orgQuery.set('');
    }
  }

  setOrgQuery(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.orgQuery.set(input.value);
  }

  async pickOrg(orgId: string): Promise<void> {
    if (orgId === this.currentOrgId()) {
      this.orgOpen.set(false);
      return;
    }
    await this.auth.switchOrganization(orgId);
    this.orgOpen.set(false);
    // Reload the page to refresh context for the new org
    window.location.reload();
  }

  openNew(): void {
    this.newOpen.set(true);
  }

  closeNew(): void {
    this.newOpen.set(false);
  }

  // Dashboard specific actions
  toggleBase(): void {
    this.base.update(v => !v);
    this.baseLabel.set(this.base() ? 'Cash basis' : 'Accrual basis');
  }

  startEdit(): void {
    this.editing.set(true);
  }

  resetLayout(): void {
    // Reset layout logic
  }

  stopEdit(): void {
    this.editing.set(false);
  }

  // Register specific actions
  openExport(): void {
    // Open export dialog logic
  }

  openImport(): void {
    // Open import dialog logic
  }

  openFav(): void {
    this.favOpen.set(true);
  }

  closeFav(): void {
    this.favOpen.set(false);
  }

  @HostListener('document:click', ['$event.target'])
  onClickOutside(target: HTMLElement) {
    if (this.orgOpen()) {
      const container = this.elementRef.nativeElement.querySelector('.org-dropdown-container');
      if (container && !container.contains(target)) {
        this.orgOpen.set(false);
      }
    }
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.orgOpen.set(false);
    this.newOpen.set(false);
    this.favOpen.set(false);
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
