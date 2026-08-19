import { Component, computed, inject, signal, ElementRef, HostListener } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService, AccessibleOrg } from '@bill-book/auth';
import { ShellNavComponent, NavItem } from '../nav/shell-nav.component';
import { ShellTopbarComponent } from '../topbar/shell-topbar.component';
import { ShellBreadcrumbComponent, BreadcrumbItem } from '../breadcrumb/shell-breadcrumb.component';

/**
 * Root CSS Grid layout orchestrator (`bb-shell`).
 * Coordinates fixed 56px left rail (`bb-shell-nav`), 46px top bar (`bb-shell-topbar`),
 * sticky breadcrumb strip (`bb-shell-breadcrumb`), and scrolling content viewport (`<router-outlet />`).
 */
@Component({
  selector: 'bb-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    ShellNavComponent,
    ShellTopbarComponent,
    ShellBreadcrumbComponent,
  ],
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
    { path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' }, // STRICT UI RULE: Accounts
    { path: '/reports', label: 'Reports', icon: 'reports', module: 'reports' },
    { path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
  ];

  /**
   * What this user can actually open based on active permissions.
   */
  readonly nav = computed(() =>
    this.all.filter((item) => item.module === null || this.auth.canView(item.module)),
  );

  // Organization Switcher State
  readonly orgOpen = signal(false);
  readonly orgQuery = signal('');
  readonly allOrgs = signal<AccessibleOrg[]>([]);

  // New Transaction Popup State
  readonly newOpen = signal(false);

  // Favourites Popup State
  readonly favOpen = signal(false);

  // Navigation / Crumbs State
  readonly crumbs = signal<BreadcrumbItem[]>([]);
  readonly isHome = computed(() => this.router.url === '/dashboard' || this.router.url === '/');
  readonly isRegister = computed(
    () =>
      this.router.url.includes('/sales') ||
      this.router.url.includes('/purchase') ||
      this.router.url.includes('/inventory') ||
      this.router.url.includes('/contacts'),
  );

  // Dashboard Actions State
  readonly base = signal(false);
  readonly baseLabel = signal('Accrual basis');
  readonly editing = signal(false);
  readonly notEditing = computed(() => !this.editing());

  readonly newGroups = [
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

  readonly currentOrgName = computed(() => 'Eternal Pathway');

  readonly currentOrgRole = computed(() => {
    const orgs = this.allOrgs();
    const id = this.currentOrgId();
    const current = orgs.find((o: AccessibleOrg) => o.orgId === id);
    return current ? current.orgName : 'Head Office';
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
    // Load organizations when shell boots
    void this.auth.accessibleOrganizations().then((orgs) => {
      this.allOrgs.set(orgs);
    });

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.updateCrumbs(event.urlAfterRedirects);
      });

    // Initialize crumbs
    setTimeout(() => this.updateCrumbs(this.router.url), 0);
  }

  updateCrumbs(url: string): void {
    if (url === '/' || url.startsWith('/dashboard')) {
      this.crumbs.set([]);
      return;
    }

    const parts = url.split('?')[0].split('/').filter((p) => p);
    const result: BreadcrumbItem[] = [];
    let currentPath = '';

    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      currentPath += '/' + part;

      let label = part.replace(/-/g, ' ');
      label = label.charAt(0).toUpperCase() + label.slice(1);

      // Special cases
      if (label.toLowerCase() === 'coa') {
        label = 'Chart of Accounts';
      } else if (label.toLowerCase() === 'accounting') {
        label = 'Accounts'; // STRICT: "Accounts"
      }

      const isLast = i === parts.length - 1;
      result.push({
        label,
        path: currentPath,
        isLink: !isLast,
        isLast,
      });
    }

    this.crumbs.set(result);
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

  toggleBase(): void {
    this.base.update((v) => !v);
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

  openExport(): void {
    // Open export dialog
  }

  openImport(): void {
    // Open import dialog
  }

  openFav(): void {
    this.favOpen.set(true);
  }

  closeFav(): void {
    this.favOpen.set(false);
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

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
