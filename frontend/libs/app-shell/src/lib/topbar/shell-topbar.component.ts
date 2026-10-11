import { FormsModule } from '@angular/forms';
import { ScanComponent, SearchInputComponent } from '@bill-book/ui-components';
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
  AccessibleOrg,
  AuthService,
  SessionContextService,
} from '@bill-book/auth';

import { FavouritesService } from '../favourites.service';
import { MenuService } from '../menu.service';
import { ShellNotificationsService } from '../notifications.service';
import { ScanToOpenService } from '../scan-to-open.service';
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
  imports: [
    SearchInputComponent, ScanComponent, FormsModule],
  templateUrl: './shell-topbar.component.html',
  styleUrl: './shell-topbar.component.scss',
})
export class ShellTopbarComponent {
  protected readonly auth = inject(AuthService);
  private readonly session = inject(SessionContextService);
  protected readonly favourites = inject(FavouritesService);
  private readonly menuService = inject(MenuService);
  protected readonly notifications = inject(ShellNotificationsService);
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef);
  private readonly scanToOpen = inject(ScanToOpenService);

  /** The last scanned document code that opened nothing, said under the scan field. */
  readonly scanMiss = signal<string | null>(null);

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
  readonly newGroups = computed<readonly DocGroup[]>(() => {
    return this.menuService.tree()
      .map((menu) => {
        const docs = menu.groups
          .flatMap((g) => g.subMenus)
          .filter((s) => s.canCreate && s.routePath)
          .map((s) => ({
            label: s.singularName ?? s.name,
            code: s.code.substring(0, 3).toUpperCase(),
            path: `${s.routePath}/new`,
          }));
        return { name: menu.name, docs };
      })
      .filter((g) => g.docs.length > 0);
  });

  readonly currentOrgId = computed(() => readOrgId());





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
    return this.allOrgs().filter((o) =>
      [o.orgName, o.roleName, o.customerName, o.gstin, o.city]
        .some((field) => !!field && field.toLowerCase().includes(query)),
    );
  });

  readonly orgEmpty = computed(() => this.filteredOrgs().length === 0);

  /**
   * The group the person has opened or shut; `undefined` until they touch one,
   * which means the current branch's group is the open one.
   */
  private readonly orgSection = signal<string | null | undefined>(undefined);

  /** The group that is open when no search is running. */
  private readonly openOrgGroup = computed(() => {
    const chosen = this.orgSection();
    if (chosen !== undefined) return chosen;
    return orgGroupName(this.allOrgs().find((o) => o.orgId === this.currentOrgId()));
  });

  /**
   * Branches grouped by their organisation, in the order the API lists them
   * (TK-189). One group is open at a time; a search opens every group so no
   * match is hidden behind a fold.
   */
  readonly orgGroups = computed<readonly OrgGroup[]>(() => {
    const searching = this.orgQuery().trim().length > 0;
    const groups = new Map<string, AccessibleOrg[]>();
    for (const org of this.filteredOrgs()) {
      const name = orgGroupName(org);
      groups.set(name, [...(groups.get(name) ?? []), org]);
    }
    return [...groups].map(([name, orgs]) => ({
      name,
      orgs,
      open: searching || name === this.openOrgGroup(),
    }));
  });

  toggleOrgGroup(group: OrgGroup): void {
    this.orgSection.set(this.openOrgGroup() === group.name ? null : group.name);
  }

  /** The row's second line: trade · organisation · currency, as the design has it. */
  orgMeta(org: AccessibleOrg): string {
    return [org.businessType ?? 'Retail', org.customerName ?? org.roleName, org.baseCurrency]
      .filter(Boolean)
      .join(' · ');
  }

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
    if (!query) return this.newGroups();
    return this.newGroups()
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
    this.orgSection.set(undefined);
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

  /** A document code scanned or typed in the search panel (TK-162). */
  async openScanned(code: string): Promise<void> {
    this.scanMiss.set(null);
    const path = await this.scanToOpen.find(code);
    if (path) {
      this.goTo(path);
    } else {
      this.scanMiss.set(`No document in this branch has the number ${code}.`);
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

/** One organisation's branches in the switcher. */
export interface OrgGroup {
  readonly name: string;
  readonly orgs: readonly AccessibleOrg[];
  readonly open: boolean;
}

/** The organisation a branch is filed under; an older API without it puts every branch in one group. */
function orgGroupName(org: AccessibleOrg | undefined): string {
  return org?.customerName?.trim() || 'Branches';
}

/** Storage can throw in a private window; a missing org id is not fatal here. */
function readOrgId(): string | null {
  try {
    return localStorage.getItem('bb.orgId');
  } catch {
    return null;
  }
}
