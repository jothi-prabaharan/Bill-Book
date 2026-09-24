import { APP_ID } from '@bill-book/auth';
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MenuGroupView, MenuView, SubMenuView } from './menu.models';
import {
  SHELL_SCREENS,
  ShellRailItem,
  ShellScreen,
  ShellScreenGroup,
} from './shell-screens';

/**
 * The navigation tree, loaded once per session from `GET /api/menu`.
 *
 * The server decides what this user may see — which modules appear in the rail,
 * which screens hang under them, and which actions they hold on each. The client
 * draws what it is given; it does not re-filter, and it does not keep its own idea
 * of what screens exist.
 *
 * The one exception is the fallback. If the call fails — the endpoint is down, the
 * browser is offline, the response is unusable — the shell falls back to the static
 * registry in `shell-screens.ts` so the person can still navigate. That registry is
 * a safety net, not a second source of truth: when the call succeeds it is not
 * consulted at all.
 *
 * None of this is a security boundary. The rail is drawn from what the server sent,
 * the router guards on the same claims, and the server checks them again on every
 * request. That last check is the one that counts.
 */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly http = inject(HttpClient);
  private readonly app = inject(APP_ID);

  private readonly menus = signal<readonly MenuView[]>([]);

  /** True once a load has finished, whether it succeeded or fell back. */
  readonly loaded = signal(false);

  /** True when the tree on screen is the fallback rather than the server's. */
  readonly usingFallback = signal(false);

  readonly tree = computed(() => this.menus());

  /**
   * The rail, derived from the tree. A module with no route of its own lands on
   * the first screen in its panel — the panel is not built yet, so the rail has
   * to take the person somewhere.
   *
   * Empty until the tree loads; the components that draw a rail fall back to
   * `FALLBACK_RAIL` while it is.
   */
  readonly rail = computed<ShellRailItem[]>(() =>
    this.menus()
      .map((menu): ShellRailItem | null => {
        const firstScreen = menu.groups
          .flatMap((group) => group.subMenus)
          .find((sub) => !!sub.routePath)?.routePath;
        // A rail entry is where a module lands, so it drops any query the screen
        // carried: landing pre-filtered on one document type is not what clicking
        // the module means.
        const path = (menu.routePath ?? firstScreen)?.split('?')[0];
        return path
          ? { path, label: menu.name, icon: menu.icon ?? '', module: menu.module }
          : null;
      })
      .filter((item): item is ShellRailItem => item !== null),
  );

  /**
   * Every screen the person can reach, flattened — what Search and Favourites
   * read. Rows with no route are already filtered out by the server, but a null
   * slips through no worse than a broken link would, so guard here too.
   */
  readonly screens = computed<ShellScreen[]>(() =>
    this.menus().flatMap((menu) => {
      const direct: ShellScreen[] = menu.routePath
        ? [{ label: menu.name, path: menu.routePath, group: menu.name, module: menu.module }]
        : [];
      const nested = menu.groups.flatMap((group) =>
        group.subMenus
          .filter((sub) => !!sub.routePath)
          .map((sub) => ({
            label: sub.name,
            path: sub.routePath as string,
            group: menu.name,
            module: sub.module,
          })),
      );
      return [...direct, ...nested];
    }),
  );

  /** The same screens grouped for the panels that render headings. */
  readonly screenGroups = computed<ShellScreenGroup[]>(() =>
    this.menus()
      .map((menu) => ({
        name: menu.name,
        items: this.screens().filter((s) => s.group === menu.name),
      }))
      .filter((g) => g.items.length > 0),
  );

  /**
   * Load the tree. Safe to call more than once; the second call refreshes.
   * Never rejects — a failure lands on the fallback and says so.
   */
  async load(): Promise<void> {
    try {
      // The app is named for the logs and any cache between here and Master;
      // the token decides which app's rows come back (TK-44).
      const menus = await firstValueFrom(
        this.http.get<MenuView[]>('/api/menu', { params: { app: this.app } }),
      );
      if (!Array.isArray(menus) || menus.length === 0) {
        this.fallback();
        return;
      }
      this.menus.set(menus);
      this.usingFallback.set(false);
    } catch {
      this.fallback();
    } finally {
      this.loaded.set(true);
    }
  }

  /** The groups under one module, for its submenu panel. */
  groupsFor(menuCode: string): readonly MenuGroupView[] {
    return this.menus().find((m) => m.code === menuCode)?.groups ?? [];
  }

  /**
   * Which module a URL belongs to — what the secondary menu shows.
   *
   * Matched by longest path prefix rather than by first segment, because a module's
   * screens do not all live under one: reconciliation sits in Banking's panel while
   * its route is `/accounting/reconciliation`. The longest match wins so a screen is
   * attributed to the module that actually owns it.
   */
  menuForPath(url: string): MenuView | null {
    const clean = url.split('?')[0];
    let best: MenuView | null = null;
    let bestLength = 0;

    for (const menu of this.menus()) {
      const paths = [
        menu.routePath,
        ...menu.groups.flatMap((g) => g.subMenus.map((s) => s.routePath)),
      ].filter((p): p is string => !!p);

      for (const path of paths) {
        const candidate = path.split('?')[0];
        const matches = clean === candidate || clean.startsWith(`${candidate}/`);
        if (matches && candidate.length > bestLength) {
          best = menu;
          bestLength = candidate.length;
        }
      }
    }

    return best;
  }

  /**
   * The row that owns a route, when the tree knows about it.
   *
   * Some rows carry a query — `/sales/transactions?type=Invoice` is the invoice
   * register — so an exact match is tried first and the bare path only after.
   * Reversing that order would hand every typed register back as the mixed one.
   */
  subMenuFor(path: string): SubMenuView | null {
    const rows = this.menus().flatMap((menu) => menu.groups.flatMap((group) => group.subMenus));
    const exact = rows.find((s) => s.routePath === path);
    if (exact) return exact;

    // Only a row with no query of its own may match on the bare path. Without that
    // guard `/sales/transactions` would answer with whichever typed register came
    // first, since they all share that path.
    const clean = path.split('?')[0];
    return rows.find((s) => !!s.routePath && !s.routePath.includes('?') && s.routePath === clean) ?? null;
  }

  /**
   * Whether this user may take an action on a screen — `allows('/sales/invoices',
   * 'create')`. A screen the tree has never heard of answers false: the shell
   * should not invent a permission it was not given.
   */
  allows(path: string, action: string): boolean {
    return this.subMenuFor(path)?.allowedActions.includes(action) ?? false;
  }

  /**
   * Rebuild the tree from the static registry, shaped like the server's response
   * so nothing downstream has to know the difference. Every action is granted:
   * the fallback exists so a person is not stranded, and the server refuses
   * anything they should not do regardless of what the buttons offer.
   */
  private fallback(): void {
    const byGroup = new Map<string, ShellScreen[]>();
    for (const screen of SHELL_SCREENS) {
      const list = byGroup.get(screen.group) ?? [];
      list.push(screen);
      byGroup.set(screen.group, list);
    }

    let menuId = 0;
    let subId = 0;
    const menus: MenuView[] = [...byGroup.entries()].map(([name, screens]) => {
      menuId += 1;
      return {
        menuId,
        code: name.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
        name,
        icon: null,
        module: screens[0]?.module ?? 'dashboard',
        routePath: screens.length === 1 ? screens[0].path : null,
        isSearchable: false,
        groups:
          screens.length === 1
            ? []
            : [
                {
                  menuGroupId: menuId,
                  code: `fallback-${menuId}`,
                  name: null,
                  icon: null,
                  subMenus: screens.map((screen) => {
                    subId += 1;
                    return {
                      subMenuId: subId,
                      code: `fallback-${subId}`,
                      name: screen.label,
                      routePath: screen.path,
                      icon: null,
                      module: screen.module ?? 'dashboard',
                      canCreate: false,
                      singularName: null,
                      hasAccess: true,
                      allowedActions: ['view', 'create', 'edit', 'delete', 'print', 'export'],
                    };
                  }),
                },
              ],
      };
    });

    this.menus.set(menus);
    this.usingFallback.set(true);
  }
}
