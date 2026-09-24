import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import {
  APP_ID,
  APP_LABELS,
  AccessibleOrg,
  AuthService,
  SessionContextService,
  isLicenceOpen,
} from '@bill-book/auth';
import { APP_URLS } from '../app-urls';
import { FavouritesService } from '../favourites.service';
import { MenuService } from '../menu.service';
import { ShellNotificationsService } from '../notifications.service';
import {
  SHELL_SCREENS,
  ShellScreen,
  ShellScreenGroup,
  groupScreens,
} from '../shell-screens';

/** One creatable document in the New popover. */
export interface DocGroupItem {
  readonly label: string;
  readonly code: string;
  /** Where "new" actually goes. Every entry points at a route that exists. */
  readonly path: string;
}

export interface DocGroup {
  readonly name: string;
  readonly docs: readonly DocGroupItem[];
}

/** Which of the top bar's panels is open. Only ever one. */
type Panel = 'org' | 'new' | 'search' | 'fav' | 'notif' | null;

/**
 * The 46px top bar.
 *
 * Left: the branch switcher and the display-only financial-year tag. Right: the
 * action group, each button owning an anchored popover — New, Search,
 * Favourites, Notifications — plus Help and Sign out.
 *
 * Every panel is anchored to its own button rather than centred over the page,
 * and only one is open at a time: opening any panel closes the rest, Escape
 * closes all of them and clears their queries, a pointerdown outside the header
 * closes them, and so does navigating.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-topbar',
  standalone: true,
  imports: [],
  templateUrl: './shell-topbar.component.html',
  styleUrl: './shell-topbar.component.scss',
})
export class ShellTopbarComponent {
  protected readonly auth = inject(AuthService);
  private readonly session = inject(SessionContextService);
  private readonly appId = inject(APP_ID);
  private readonly appUrls = inject(APP_URLS);
  protected readonly favourites = inject(FavouritesService);
  private readonly menuService = inject(MenuService);
  protected readonly notifications = inject(ShellNotificationsService);
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef);

  readonly financialYear = input<string>('FY 2026-27');
  readonly userDisplayName = input<string>('Praba');
  readonly userRoleName = input<string>('Owner');

  readonly organizationChange = output<string>();
  readonly quickAction = output<string>();
  readonly logout = output<void>();

  /** The one open panel, or null. */
  private readonly panel = signal<Panel>(null);

  readonly orgOpen = computed(() => this.panel() === 'org');
  readonly newOpen = computed(() => this.panel() === 'new');
  readonly searchOpen = computed(() => this.panel() === 'search');
  readonly favOpen = computed(() => this.panel() === 'fav');
  readonly notifOpen = computed(() => this.panel() === 'notif');

  readonly orgQuery = signal('');
  readonly newQuery = signal('');
  readonly searchQuery = signal('');
  readonly favQuery = signal('');

  readonly allOrgs = signal<AccessibleOrg[]>([]);

  /**
   * Every document a person can raise. Each `path` is a route that exists in
   * `app.routes.ts` — a tile that leads nowhere is worse than no tile.
   */
  readonly newGroups: readonly DocGroup[] = [
    {
      name: 'Sales',
      docs: [
        { label: 'Invoice', code: 'INV', path: '/sales/invoices/new' },
        { label: 'Sales order', code: 'SOR', path: '/sales/sales-orders/new' },
        { label: 'Quote', code: 'QOT', path: '/sales/quotes/new' },
        { label: 'Delivery challan', code: 'DLC', path: '/sales/delivery-challans/new' },
        { label: 'Credit note', code: 'CRN', path: '/sales/credit-notes/new' },
      ],
    },
    {
      name: 'Purchase',
      docs: [
        { label: 'Bill', code: 'BIL', path: '/purchase/bills/new' },
        { label: 'Purchase order', code: 'POR', path: '/purchase/purchase-orders/new' },
        { label: 'Goods receipt', code: 'GRN', path: '/purchase/goods-receipts/new' },
        { label: 'Debit note', code: 'DBN', path: '/purchase/debit-notes/new' },
      ],
    },
    {
      name: 'Banking',
      docs: [
        { label: 'Receive money', code: 'REC', path: '/banking/receive-money' },
        { label: 'Spend money', code: 'PAY', path: '/banking/spend-money' },
        { label: 'Transfer money', code: 'TRF', path: '/banking/transfer-money' },
      ],
    },
  ];

  readonly currentOrgId = computed(() => readOrgId());

  /**
   * The app switcher (TK-44): the other apps this user holds a role in, in this
   * branch, from the session context. An app with no URL configured for this
   * deployment is listed but cannot be opened.
   */
  readonly otherApps = computed(() =>
    (this.session.context()?.apps ?? [])
      .filter((a) => a.app !== this.appId)
      .map((a) => ({
        app: a.app,
        label: APP_LABELS[a.app] ?? a.app,
        url: this.appUrls[a.app] ?? null,
        open: isLicenceOpen(a.licenseStatus),
      })),
  );

  /**
   * Opens another app on the same branch. That app's shell mints its own token
   * for the branch when it finds this one (`pageGuard`), so nothing is minted
   * here.
   */
  openApp(url: string | null): void {
    this.closeAll();
    if (url !== null && typeof window !== 'undefined') {
      window.location.assign(url);
    }
  }

  /**
   * The API returns one name per accessible org and it is the branch name —
   * there is no separate company field on `AccessibleOrg` — so the trigger sets
   * the branch as the primary line and the role beneath it.
   */
  readonly currentOrgName = computed(() => {
    const current = this.allOrgs().find((o) => o.orgId === this.currentOrgId());
    return current ? current.orgName : 'Head Office';
  });

  readonly currentOrgBranch = computed(() => {
    const current = this.allOrgs().find((o) => o.orgId === this.currentOrgId());
    return current ? current.roleName : '';
  });

  readonly orgCount = computed(() => this.allOrgs().length);

  readonly filteredOrgs = computed(() => {
    const query = this.orgQuery().trim().toLowerCase();
    if (!query) return this.allOrgs();
    return this.allOrgs().filter(
      (o) =>
        o.orgName.toLowerCase().includes(query) || o.roleName.toLowerCase().includes(query),
    );
  });

  readonly orgEmpty = computed(() => this.filteredOrgs().length === 0);

  /**
   * Screens this role may actually open — what Search and Favourites are drawn
   * from. The server's menu answers when it has loaded; the static registry
   * covers the moment before it does, and is filtered by `canView` because it
   * knows nothing about this role.
   */
  private readonly visibleScreens = computed<readonly ShellScreen[]>(() => {
    const fromServer = this.menuService.screens();
    if (fromServer.length > 0) return fromServer;
    return SHELL_SCREENS.filter((s) => s.module === null || this.auth.canView(s.module));
  });

  readonly newFilteredGroups = computed<readonly DocGroup[]>(() => {
    const query = this.newQuery().trim().toLowerCase();
    if (!query) return this.newGroups;
    return this.newGroups
      .map((group) => ({
        name: group.name,
        docs: group.docs.filter(
          (doc) =>
            doc.label.toLowerCase().includes(query) || doc.code.toLowerCase().includes(query),
        ),
      }))
      .filter((group) => group.docs.length > 0);
  });

  readonly newCount = computed(() =>
    this.newFilteredGroups().reduce((sum, g) => sum + g.docs.length, 0),
  );

  readonly newEmpty = computed(() => this.newCount() === 0);

  /**
   * Search covers the screens the chrome knows about. Documents, contacts,
   * items and accounts want a server-side index the API does not expose yet;
   * when it does, its hits join these groups.
   */
  readonly searchGroups = computed<ShellScreenGroup[]>(() => {
    const query = this.searchQuery().trim().toLowerCase();
    if (!query) return [];
    return groupScreens(
      this.visibleScreens().filter((s) => s.label.toLowerCase().includes(query)),
    );
  });

  readonly searchEmpty = computed(
    () => this.searchQuery().trim().length > 0 && this.searchGroups().length === 0,
  );

  readonly favGroups = computed<ShellScreenGroup[]>(() => {
    const starred = this.favourites.starred();
    const query = this.favQuery().trim().toLowerCase();
    const screens = this.visibleScreens().filter(
      (s) => starred.includes(s.path) && (!query || s.label.toLowerCase().includes(query)),
    );
    return groupScreens(screens);
  });

  readonly favCount = computed(() =>
    this.favGroups().reduce((sum, g) => sum + g.items.length, 0),
  );

  readonly favEmpty = computed(() => this.favCount() === 0);

  constructor() {
    void this.auth.accessibleOrganizations().then((orgs) => this.allOrgs.set(orgs));

    // Navigating closes whatever was open — a panel left hanging over a screen
    // the person has already moved on from is just in the way.
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) this.closeAll();
    });
  }

  /** Opening one panel closes the others; clicking the same button closes it. */
  private toggle(next: Exclude<Panel, null>): void {
    const closing = this.panel() === next;
    this.clearQueries();
    this.panel.set(closing ? null : next);
  }

  toggleOrg(): void {
    this.toggle('org');
  }

  toggleNew(): void {
    this.toggle('new');
  }

  toggleSearch(): void {
    this.toggle('search');
  }

  toggleFav(): void {
    this.toggle('fav');
  }

  toggleNotif(): void {
    this.toggle('notif');
  }

  closeAll(): void {
    this.panel.set(null);
    this.clearQueries();
  }

  private clearQueries(): void {
    this.orgQuery.set('');
    this.newQuery.set('');
    this.searchQuery.set('');
    this.favQuery.set('');
  }

  setOrgQuery(value: string): void {
    this.orgQuery.set(value);
  }

  setNewQuery(value: string): void {
    this.newQuery.set(value);
  }

  setSearchQuery(value: string): void {
    this.searchQuery.set(value);
  }

  setFavQuery(value: string): void {
    this.favQuery.set(value);
  }

  /** Two letters from the branch name, for the row's stroke-drawn avatar. */
  initials(name: string): string {
    const words = name.trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) return '—';
    if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
    return (words[0][0] + words[words.length - 1][0]).toUpperCase();
  }

  async pickOrg(orgId: string): Promise<void> {
    if (orgId === this.currentOrgId()) {
      this.closeAll();
      return;
    }
    await this.auth.switchOrganization(orgId);
    this.organizationChange.emit(orgId);
    this.closeAll();
    try {
      if (typeof window !== 'undefined' && typeof window.location?.reload === 'function') {
        window.location.reload();
      }
    } catch {
      // Ignored in non-browser/test environments.
    }
  }

  goTo(path: string): void {
    this.closeAll();
    void this.router.navigateByUrl(path);
  }

  manageOrganizations(): void {
    this.goTo('/settings/branches');
  }

  selectDoc(doc: DocGroupItem): void {
    this.quickAction.emit(doc.code);
    this.goTo(doc.path);
  }

  unstar(screen: ShellScreen, event: Event): void {
    event.stopPropagation();
    this.favourites.unstar(screen.path);
  }

  markAllRead(): void {
    this.notifications.markAllRead();
  }

  /**
   * Pointerdown rather than click: a panel should be gone by the time the
   * pointer lifts, and click would also fire for a press that began inside.
   */
  @HostListener('document:pointerdown', ['$event.target'])
  onPointerDownOutside(target: EventTarget | null): void {
    if (this.panel() === null) return;
    const host = this.elementRef.nativeElement as HTMLElement;
    if (target instanceof Node && host.contains(target)) return;
    this.closeAll();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeAll();
  }

  doLogout(): void {
    this.logout.emit();
    // Revokes the session server-side too, so the refresh token cannot be spent
    // after the user has walked away from the machine.
    void this.auth.signOut();
    void this.router.navigateByUrl('/login');
  }
}

/** Storage can throw in a private window; a missing org id is not fatal here. */
function readOrgId(): string | null {
  try {
    return localStorage.getItem('bb.orgId');
  } catch {
    return null;
  }
}
