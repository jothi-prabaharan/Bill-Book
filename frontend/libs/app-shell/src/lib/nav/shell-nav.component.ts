import { ChangeDetectionStrategy } from '@angular/core';
import { Component, ElementRef, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { AccessibleOrg, AuthService, SessionContextService } from '@bill-book/auth';
import { MenuService } from '../menu.service';
import { FALLBACK_RAIL, ShellRailItem } from '../shell-screens';
import { PHONE_TAB_MODULES, ShellNavigationService } from '../shell-navigation.service';
import { PwaInstallService } from '../pwa-install.service';
import { ShellIconComponent } from './shell-icon.component';

/**
 * One entry in the rail. Kept as an exported name because the shell and its specs
 * refer to it; the shape lives with the fallback it is declared alongside.
 */
export type NavItem = ShellRailItem;

/**
 * 64px fixed left rail (z-index: 5, ink ground `--color-ink`).
 * Contains module navigation items, active cutout rule with 4px left accent rule,
 * the account button (initials and role) with its menu, and the mobile bottom
 * tab bar (<860px).
 *
 * The rail is drawn from `GET /api/menu`: the server owns which modules exist,
 * their labels, their Lucide icons and where they lead. `FALLBACK_RAIL` covers the
 * gap before that call answers, and the case where it never does.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-nav',
  standalone: true,
  imports: [ShellIconComponent],
  templateUrl: './shell-nav.component.html',
  styleUrl: './shell-nav.component.scss',
})
export class ShellNavComponent {
  protected readonly auth = inject(AuthService);
  private readonly session = inject(SessionContextService);
  private readonly menuService = inject(MenuService);
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef, { optional: true });
  protected readonly navigation = inject(ShellNavigationService);
  protected readonly pwa = inject(PwaInstallService);

  readonly userDisplayName = input<string>('Praba');
  readonly userRoleName = input<string>('Owner');

  readonly logout = output<void>();

  /** Whether the account menu beside the rail is open. */
  readonly profileOpen = signal(false);

  /** The signed-in person's name, from the session; the input until it has loaded. */
  readonly userName = computed(() => this.session.context()?.displayName || this.userDisplayName());

  readonly userEmail = computed(() => this.session.context()?.email ?? '');

  /** The branches this person holds a role in; the role shown is the current branch's. */
  private readonly orgs = signal<AccessibleOrg[]>([]);

  /** The role held in the current branch. */
  readonly userRole = computed(() => {
    const current = this.orgs().find((o) => o.orgId === this.auth.activeOrgId());
    return current?.roleName || this.userRoleName();
  });

  /** Two letters for the rail's round avatar. */
  readonly userInitials = computed(() => {
    const words = this.userName().replace(/[^\p{L} ]/gu, '').split(/\s+/).filter(Boolean);
    if (words.length === 0) return '—';
    if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
    return (words[0][0] + words[words.length - 1][0]).toUpperCase();
  });

  /**
   * Mobile tabs, as the design picks them: Home, Purchase, Sales and Banking,
   * the four a counter reaches for. Everything else, Settings included, is
   * under More. Falls back to the first four when those are not on the menu.
   */
  readonly mobileTopNav = computed(() => {
    const preferred = PHONE_TAB_MODULES
      .map((module) => this.nav().find((item) => (item.module || 'dashboard') === module))
      .filter((item): item is NavItem => !!item);
    return preferred.length === PHONE_TAB_MODULES.length ? preferred : this.nav().slice(0, 4);
  });

  readonly mobileMoreNav = computed(() => {
    const top = new Set(this.mobileTopNav().map((item) => item.path));
    return this.nav().filter((item) => !top.has(item.path));
  });

  constructor() {
    void this.auth.accessibleOrganizations().then((orgs) => this.orgs.set(orgs)).catch(() => undefined);

    // Navigating closes the account menu, as it closes the top bar's panels.
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) this.profileOpen.set(false);
    });
  }

  /**
   * Whether a rail entry is the current module: the one whose secondary menu is
   * open, which is the screen's own module until another is picked on the rail
   * (TK-189). Resolved by `ShellNavigationService`, the same answer the panel,
   * the phone sheet and the breadcrumb use, so they cannot disagree. A screen
   * filed under another module's menu — /settings/hsn-sac is Inventory's — marks
   * that module.
   */
  isActive(item: NavItem): boolean {
    return this.navigation.isCurrent(item);
  }

  /** Whether the entry opens a secondary menu rather than a screen. */
  hasMenu(item: NavItem): boolean {
    const menu = item.code ? this.menuService.menuByCode(item.code) : null;
    return !!menu && this.menuService.rowCount(menu) > 1;
  }

  /**
   * A click on a rail entry or tab: open its module's menu, or go to its screen
   * when it has none (`ShellNavigationService.pickRail`). A modified or middle
   * click is left to the browser, which opens the module's screen in a new tab.
   */
  pick(item: NavItem, event: MouseEvent): void {
    this.profileOpen.set(false);
    this.navigation.pickRail(item, event);
  }

  /** The More tab is current while a module that is not a tab is. */
  readonly moreActive = computed(() => this.mobileMoreNav().some((item) => this.isActive(item)));

  /**
   * The menu's bottom edge, level with the account button's. The menu is
   * fixed rather than absolute because the rail scrolls, and would clip it.
   */
  readonly menuBottom = signal(0);

  toggleProfile(event: MouseEvent): void {
    const button = event.currentTarget as HTMLElement | null;
    if (button && typeof window !== 'undefined') {
      this.menuBottom.set(Math.max(0, window.innerHeight - button.getBoundingClientRect().bottom));
    }
    this.profileOpen.update((open) => !open);
  }

  @HostListener('document:pointerdown', ['$event.target'])
  onPointerDownOutside(target: EventTarget | null): void {
    if (!this.profileOpen()) return;
    const account = (this.elementRef?.nativeElement as HTMLElement | undefined)?.querySelector?.('.rail-account');
    if (target instanceof Node && account?.contains(target)) return;
    this.profileOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.profileOpen.set(false);
    this.navigation.closeMore();
  }

  /** A tap on the dim area around the More sheet, not on the sheet itself. */
  onMoreBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.navigation.closeMore();
  }

  /** What the rail shows until the server's tree arrives. */
  readonly allNavItems: readonly NavItem[] = FALLBACK_RAIL;

  /**
   * What this user can actually open.
   *
   * The server has already filtered its own tree to this role, so `canView`
   * changes nothing there — it still guards the fallback, which knows nothing
   * about who is signed in.
   */
  readonly nav = computed(() => {
    const fromServer = this.menuService.rail();
    const source = fromServer.length > 0 ? fromServer : this.allNavItems;
    return source.filter((item) => item.module === null || this.auth.canView(item.module));
  });

  readonly primaryNav = computed(() =>
    this.nav().filter((item) => item.module !== 'settings'),
  );

  readonly settingsItem = computed(() =>
    this.nav().find((item) => item.module === 'settings'),
  );

  onProfileClick(): void {
    this.profileOpen.set(false);
    this.navigation.closeMore();
    void this.router.navigate(['/profile']);
  }

  installPwa(): void {
    this.profileOpen.set(false);
    this.navigation.closeMore();
    void this.pwa.promptInstall();
  }

  onLogout(): void {
    this.profileOpen.set(false);
    this.logout.emit();
  }
}
