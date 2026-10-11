import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { MenuView } from './menu.models';
import { MenuService } from './menu.service';
import { ShellPanelService } from './panel-state.service';
import { SHELL_SCREENS, ShellRailItem } from './shell-screens';

/** Below this width the rail becomes the phone tab bar and the panel a sheet. */
const PHONE_QUERY = '(max-width: 860px)';

/**
 * The design's four phone tabs, by module; More holds the rest. Home is
 * `dashboard` on the server's menu and has no module on the fallback rail.
 */
export const PHONE_TAB_MODULES: readonly string[] = ['dashboard', 'purchase', 'sales', 'banking'];

/**
 * Which module's secondary menu is open, and how the rail and the phone tabs
 * open it (TK-189), as `Shell.dc.html`'s `pick()` does:
 *
 * - **The rail opens a menu; a row opens a screen.** Clicking a module that has a
 *   secondary menu shows that menu and marks the module current; the screen stays
 *   where it is until a row is picked. Clicking the module whose menu is already
 *   open hides or shows it. A module with no menu of its own (Home, or one with a
 *   single screen) goes straight to its screen.
 * - **The menu follows the screen.** Every navigation hands the menu back to the
 *   module that owns the new URL, so a row, a search hit or a link all leave the
 *   rail, the menu and the breadcrumb agreeing.
 * - **On a phone** the same menu is a bottom sheet. A tab opens it, picking a row
 *   or tapping outside closes it, and Back from a module that is only reachable
 *   through More returns to More.
 */
@Injectable({ providedIn: 'root' })
export class ShellNavigationService {
  private readonly router = inject(Router);
  private readonly menus = inject(MenuService);
  private readonly panel = inject(ShellPanelService);

  /** The current URL, after redirects. */
  readonly url = signal(this.router.url);

  /** A module picked on the rail whose screen is not open yet; null follows the URL. */
  private readonly picked = signal<string | null>(null);

  /** Whether the viewport is phone-sized, where the rail is a tab bar. */
  readonly isPhone = signal(false);

  /** The phone's bottom sheet holding the secondary menu. */
  readonly sheetOpen = signal(false);

  /** The phone's More sheet, holding the modules that are not tabs. */
  readonly moreOpen = signal(false);

  /** The module that owns the screen on view. */
  readonly urlMenu = computed(() => this.menus.menuForPath(this.url()));

  /** The module whose secondary menu is showing: the one picked, else the screen's. */
  readonly panelMenu = computed<MenuView | null>(() => {
    const code = this.picked();
    return (code ? this.menus.menuByCode(code) : null) ?? this.urlMenu();
  });

  /** The module the rail and the tabs mark as current. */
  readonly activeCode = computed(() => this.panelMenu()?.code ?? null);

  constructor() {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.url.set(event.urlAfterRedirects);
        this.picked.set(null);
        this.sheetOpen.set(false);
        this.moreOpen.set(false);
      }
    });

    if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
      const query = window.matchMedia(PHONE_QUERY);
      this.isPhone.set(query.matches);
      const update = (e: MediaQueryListEvent) => this.isPhone.set(e.matches);
      query.addEventListener?.('change', update);
      inject(DestroyRef).onDestroy(() => query.removeEventListener?.('change', update));
    }
  }

  /** Whether a rail entry is the module currently marked. */
  isCurrent(item: ShellRailItem): boolean {
    const active = this.activeCode();
    if (active && item.code) return item.code === active;
    // Before the tree loads, the static registry knows which module a screen sits
    // under — /settings/hsn-sac is Inventory's — and the URL's first segment
    // answers for anything it does not list.
    const path = this.url().split('?')[0];
    const screen = SHELL_SCREENS
      .filter((s) => {
        const own = s.path.split('?')[0];
        return path === own || path.startsWith(`${own}/`);
      })
      .sort((a, b) => b.path.length - a.path.length)[0];
    if (screen) return item.label === screen.group || item.code === screen.group.toLowerCase();
    const segment = (p: string) => p.split('?')[0].split('/')[1] ?? '';
    return segment(item.path) !== '' && segment(item.path) === segment(path);
  }

  /**
   * A click on a rail entry or a phone tab. Returns false when the click was
   * handled here, so the caller prevents the browser following the link; a click
   * with a modifier key, or a middle click, is left to open the module in a new tab.
   */
  pickRail(item: ShellRailItem, event?: MouseEvent): boolean {
    if (event && (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)) {
      return true;
    }
    event?.preventDefault();

    const menu = item.code ? this.menus.menuByCode(item.code) : this.menus.menuForPath(item.path);
    if (!menu || this.menus.rowCount(menu) <= 1) {
      // Nothing to choose between: go to the module's screen.
      this.moreOpen.set(false);
      this.sheetOpen.set(false);
      void this.router.navigateByUrl(item.path);
      return false;
    }

    const isOpen = this.activeCode() === menu.code;
    if (this.isPhone()) {
      this.moreOpen.set(false);
      if (isOpen && this.sheetOpen()) {
        this.sheetOpen.set(false);
        return false;
      }
      this.picked.set(menu.code === this.urlMenu()?.code ? null : menu.code);
      this.sheetOpen.set(true);
      return false;
    }

    if (isOpen) {
      this.panel.toggle();
      return false;
    }
    this.picked.set(menu.code === this.urlMenu()?.code ? null : menu.code);
    this.panel.show();
    return false;
  }

  /** The breadcrumb's menu button: the panel on a desktop, the sheet on a phone. */
  togglePanel(): void {
    if (this.isPhone()) {
      this.sheetOpen.update((open) => !open);
    } else {
      this.panel.toggle();
    }
  }

  /** Whether the secondary menu is showing, in whichever form the viewport uses. */
  readonly panelShown = computed(() =>
    this.isPhone() ? this.sheetOpen() : !this.panel.collapsed(),
  );

  /**
   * The sheet's Back. A module that is a phone tab just closes its sheet; one that
   * is only reachable through More goes back to More, as the design's
   * `collapseSub` does.
   */
  closeSheet(): void {
    const module = this.panelMenu()?.module || 'dashboard';
    this.sheetOpen.set(false);
    this.picked.set(null);
    if (!PHONE_TAB_MODULES.includes(module)) this.moreOpen.set(true);
  }

  /** Tapping outside the sheet: close it and stay on the screen. */
  dismissSheet(): void {
    this.sheetOpen.set(false);
    this.picked.set(null);
  }

  toggleMore(): void {
    this.sheetOpen.set(false);
    this.moreOpen.update((open) => !open);
  }

  closeMore(): void {
    this.moreOpen.set(false);
  }
}
