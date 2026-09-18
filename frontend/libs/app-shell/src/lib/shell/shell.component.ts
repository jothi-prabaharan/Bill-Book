import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { AuthService } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import { ShellNavComponent, NavItem } from '../nav/shell-nav.component';
import { ShellTopbarComponent } from '../topbar/shell-topbar.component';
import { ShellSubpanelComponent } from '../subpanel/shell-subpanel.component';
import {
  ShellBreadcrumbComponent,
  BreadcrumbItem,
} from '../breadcrumb/shell-breadcrumb.component';
import { ShellBoardService } from '../board-state.service';
import { FavouritesService } from '../favourites.service';
import { MenuService } from '../menu.service';
import { FALLBACK_RAIL, SHELL_SCREENS } from '../shell-screens';

/**
 * Root CSS Grid layout orchestrator (`bb-shell`).
 *
 * Owns the frame — 56px rail, 46px top bar, breadcrumb strip, scrolling outlet —
 * and the breadcrumb trail. Panels belong to the top bar; board state belongs to
 * `ShellBoardService`. Nothing about either is duplicated here.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    ShellNavComponent,
    ShellTopbarComponent,
    ShellSubpanelComponent,
    ShellBreadcrumbComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formats = inject(FormatSettingsService);
  private readonly board = inject(ShellBoardService);
  private readonly favourites = inject(FavouritesService);
  private readonly menuService = inject(MenuService);

  /** The rail before `/api/menu` answers — see `shell-screens.ts`. */
  private readonly all: readonly NavItem[] = FALLBACK_RAIL;

  /**
   * What this user can actually open.
   *
   * The server's tree is already filtered to this role, so `canView` changes
   * nothing there; it still guards the fallback, which knows nothing about who
   * is signed in.
   */
  readonly nav = computed(() => {
    const fromServer = this.menuService.rail();
    const source = fromServer.length > 0 ? fromServer : this.all;
    return source.filter((item) => item.module === null || this.auth.canView(item.module));
  });

  /** The URL as a signal, so anything derived from the route recomputes. */
  private readonly url = signal('/');

  readonly crumbs = signal<BreadcrumbItem[]>([]);

  readonly isHome = computed(() => this.url() === '/dashboard' || this.url() === '/');

  readonly base = computed(() => this.board.base());
  readonly baseLabel = computed(() => this.board.baseLabel());
  readonly editing = computed(() => this.board.editing());

  /**
   * Only a screen the menu knows about can be starred — a document being edited
   * is not one. The server's tree answers first; the static registry covers the
   * moment before it loads.
   */
  private readonly currentScreen = computed(() => {
    const url = this.url();
    const fromServer = this.menuService.screens();
    const source = fromServer.length > 0 ? fromServer : SHELL_SCREENS;

    // Exact first: a typed register — /sales/transactions?type=Invoice — is its
    // own screen and stars separately from the mixed list it shares a path with.
    // Bare-path matching is for screens that carry no query; a typed register is
    // matched exactly or not at all, or starring one would star them all.
    return (
      source.find((s) => s.path === url) ??
      source.find((s) => !s.path.includes('?') && s.path === url.split('?')[0]) ??
      null
    );
  });

  readonly currentIsStarrable = computed(() => this.currentScreen() !== null);

  readonly currentIsStarred = computed(() => {
    const screen = this.currentScreen();
    return screen !== null && this.favourites.starred().includes(screen.path);
  });

  constructor() {
    // The branch's date and money formats, fetched once for every screen under
    // the shell. Not awaited: the service starts at the shipped defaults, so a
    // page that renders first shows the common case rather than blanks. Nothing
    // resets it on an org switch because the switch reloads the window, which
    // rebuilds the service along with everything else.
    void this.formats.load();

    // The rail, the search index and every "may I create here" answer come from
    // this one call. Nothing waits on it: the shell paints from the fallback and
    // swaps to the server's tree when it lands.
    void this.menuService.load();

    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.url.set(event.urlAfterRedirects);
        this.updateCrumbs(event.urlAfterRedirects);
      }
    });

    this.url.set(this.router.url);
    this.updateCrumbs(this.router.url);
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

  toggleStar(): void {
    const screen = this.currentScreen();
    if (screen) this.favourites.toggle(screen.path);
  }

  toggleBase(): void {
    this.board.toggleBase();
  }

  startEdit(): void {
    this.board.startEdit();
  }

  stopEdit(): void {
    this.board.stopEdit();
  }

  resetLayout(): void {
    this.board.requestReset();
  }

  /**
   * Export is owned by the screen being exported, not the shell — the shell
   * only carries the control. Until a register opts in, the chosen format goes
   * nowhere.
   */
  openExport(_format: string): void {
    // Intentionally empty: wired per register.
  }

  openImport(): void {
    // Intentionally empty: wired per register.
  }

  logout(): void {
    void this.auth.signOut();
    void this.router.navigateByUrl('/login');
  }
}
