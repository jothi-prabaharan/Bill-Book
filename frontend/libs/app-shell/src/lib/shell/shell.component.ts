import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { AuthService } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import { ShellNavComponent, NavItem } from '../nav/shell-nav.component';
import { ShellTopbarComponent } from '../topbar/shell-topbar.component';
import { TiltDirective, RouteMotionDirective } from '@bill-book/ui-components';
import { filter } from 'rxjs/operators';
import { ShellSubpanelComponent } from '../subpanel/shell-subpanel.component';
import {
  ShellBreadcrumbComponent,
  BreadcrumbItem,
  buildCrumbs,
} from '../breadcrumb/shell-breadcrumb.component';
import { ShellBoardService } from '../board-state.service';
import { FavouritesService } from '../favourites.service';
import { MenuService } from '../menu.service';
import { ScanToOpenService } from '../scan-to-open.service';
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
    ShellTopbarComponent, TiltDirective, RouteMotionDirective,
    ShellSubpanelComponent,
    ShellBreadcrumbComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  readonly isDashboard = signal<boolean>(false);
  readonly isMasterScreen = signal<boolean>(false);
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

  /**
   * The trail: from the menu tree — module › screen › form or record — once it
   * has loaded, and spelled out from the URL before then or for a screen the
   * tree does not list (TK-189).
   */
  readonly crumbs = computed<BreadcrumbItem[]>(() => {
    const url = this.url();
    return this.menuService.crumbsFor(url) ?? buildCrumbs(url);
  });

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
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe((e: any) => {
      this.isDashboard.set(e.urlAfterRedirects.includes('/dashboard'));
    });

    // A printed document code scanned anywhere opens its document (TK-162).
    inject(ScanToOpenService).listen(inject(DestroyRef));

    // The rail, the search index and every "may I create here" answer come from
    // this one call. Nothing waits on it: the shell paints from the fallback and
    // swaps to the server's tree when it lands.
    void this.menuService.load();

    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.url.set(event.urlAfterRedirects);
      }
    });

    this.url.set(this.router.url);
  }

  /** Points the trail at a URL, as a navigation does. */
  updateCrumbs(url: string): void {
    this.url.set(url);
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

  logout(): void {
    void this.auth.signOut();
    void this.router.navigateByUrl('/login');
  }
}



