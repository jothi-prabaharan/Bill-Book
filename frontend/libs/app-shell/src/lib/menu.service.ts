
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MenuGroupView, MenuView, SubMenuView } from './menu.models';
import {
  FALLBACK_RAIL,
  SHELL_SCREENS,
  ShellRailItem,
  ShellScreen,
  ShellScreenGroup,
} from './shell-screens';

/** One step of the breadcrumb trail, as `crumbsFor` builds it from the tree. */
export interface MenuCrumb {
  readonly label: string;
  readonly path: string;
  readonly isLink: boolean;
  readonly isLast: boolean;
}

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


  private readonly menus = signal<readonly MenuView[]>([]);

  /** True once a load has finished, whether it succeeded or fell back. */
  readonly loaded = signal(false);

  /** True when the tree on screen is the fallback rather than the server's. */
  readonly usingFallback = signal(false);

  readonly tree = computed(() => this.menus());

  /**
   * The rail, derived from the tree. An entry's `path` is where its module lands:
   * the module's own route, or else the first screen in its panel. Clicking an
   * entry with a secondary menu opens that menu rather than going there (TK-189,
   * `ShellNavigationService.pickRail`); the path is for one with no menu, and for
   * opening the module in a new tab.
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
        // The fallback tree has no icons; borrow the static rail's so a failed
        // menu call still draws them.
        const icon =
          menu.icon ??
          FALLBACK_RAIL.find((r) => r.code === menu.code || r.label === menu.name)?.icon ??
          '';
        return path
          ? { code: menu.code, path, label: menu.name, icon, module: menu.module }
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
      const menus = await firstValueFrom(
        this.http.get<MenuView[]>('/api/menu'),
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

  /** The menu with this code, or null. */
  menuByCode(code: string): MenuView | null {
    return this.menus().find((menu) => menu.code === code) ?? null;
  }

  /** How many screens a menu's panel lists. */
  rowCount(menu: MenuView): number {
    return menu.groups.reduce((total, group) => total + group.subMenus.length, 0);
  }

  /**
   * The row a URL is on, or sits under (TK-189).
   *
   * The row itself first (`subMenuFor`), then the row the URL is a child of — a
   * form at `/sales/invoices/new` or a record at `/sales/invoices/42` belongs to
   * the Invoices row, so the panel keeps that row marked while it is open. Only a
   * row with no query of its own can be a parent: a typed register shares its path
   * with its siblings, so a prefix says nothing about which of them owns a child.
   */
  rowForPath(url: string): SubMenuView | null {
    const own = this.subMenuFor(url);
    if (own) return own;

    const path = url.split('?')[0];
    let best: SubMenuView | null = null;
    for (const row of this.menus().flatMap((menu) => menu.groups.flatMap((group) => group.subMenus))) {
      const route = row.routePath;
      if (!route || route.includes('?') || !path.startsWith(`${route}/`)) continue;
      if (!best || route.length > (best.routePath?.length ?? 0)) best = row;
    }
    return best;
  }

  /**
   * The breadcrumb trail for a URL, from the tree: module › screen › the form or
   * record under it (TK-189). Null for a URL the tree does not know, so the caller
   * can fall back to spelling out the path. Home has no trail of its own: the
   * strip always starts with Home.
   */
  crumbsFor(url: string): MenuCrumb[] | null {
    const [path, query] = url.split('?');
    if (path === '/' || path.startsWith('/dashboard')) return [];

    const menu = this.menuForPath(url);
    if (!menu) return null;

    const landing = (menu.routePath ?? this.firstRoute(menu) ?? path).split('?')[0];
    const trail: MenuCrumb[] = [{ label: menu.name, path: landing, isLink: true, isLast: false }];

    const row = this.rowForPath(url);
    const rowPath = row?.routePath?.split('?')[0];
    if (row && rowPath) {
      const atRow = path === rowPath;
      trail.push({ label: row.name, path: row.routePath as string, isLink: !atRow, isLast: atRow });
      if (!atRow) {
        trail.push({ label: this.childLabel(path.slice(rowPath.length), query, row), path, isLink: false, isLast: true });
      }
      return trail;
    }

    if (menu.routePath && path === menu.routePath.split('?')[0]) {
      return [{ label: menu.name, path, isLink: false, isLast: true }];
    }

    const last = path.split('/').filter(Boolean).pop() ?? '';
    trail.push({ label: humanise(last), path, isLink: false, isLast: true });
    return trail;
  }

  /**
   * Where starting a new record on a screen goes — the secondary menu's + and the
   * top bar's New menu both ask here, so they cannot disagree (TK-189).
   *
   * A document with a form of its own goes to that form: `/sales/invoices/new`.
   * The rows carry no create route, so this maps the codes that have one. Any
   * other screen opens its form in place, so it is asked to with
   * `?action=create`, which the register heading (and `onCreateRequest`) answers.
   * Appending `/new` to a path that has no such route is how the top bar's New
   * menu used to land on the dashboard.
   */
  createPath(sub: SubMenuView): string | null {
    const known: Record<string, string> = {
      qot: '/sales/quotes/new',
      sor: '/sales/sales-orders/new',
      dlc: '/sales/delivery-challans/new',
      inv: '/sales/invoices/new',
      crn: '/sales/credit-notes/new',
      por: '/purchase/purchase-orders/new',
      grn: '/purchase/goods-receipts/new',
      bil: '/purchase/bills/new',
      dbn: '/purchase/debit-notes/new',
      // Forms with a page of their own (TK-188 and the HR/School builds): the
      // list's ?action=create would only bounce on to these.
      both: '/contacts/new',
      cus: '/contacts/new?role=customer',
      ven: '/contacts/new?role=vendor',
      emp: '/hrm/employees/new',
      stu: '/sis/students/new',
      apl: '/admission/applications/new',
    };
    if (known[sub.code]) return known[sub.code];
    if (NO_CREATE_FORM.has(sub.code)) return null;
    return sub.routePath ? `${sub.routePath.split('?')[0]}?action=create` : null;
  }

  /** The first screen in a menu's panel. */
  private firstRoute(menu: MenuView): string | null {
    return menu.groups.flatMap((group) => group.subMenus).find((sub) => !!sub.routePath)?.routePath ?? null;
  }

  /**
   * What the crumb after a screen says: "New invoice" for its form, "New vendor"
   * for a contact in a role (TK-188), "Edit invoice" for an edit route, and the
   * record's kind for a record.
   */
  private childLabel(rest: string, query: string | undefined, row: SubMenuView): string {
    const parts = rest.split('/').filter(Boolean);
    const kind = (row.singularName ?? row.name).trim();
    if (parts[0] === 'new') {
      const role = new URLSearchParams(query ?? '').get('role');
      if (role) return `New ${role === 'jobworker' ? 'job worker' : role.toLowerCase()}`;
      return `New ${kind.toLowerCase()}`;
    }
    if (parts.includes('edit')) return `Edit ${kind.toLowerCase()}`;
    return row.singularName ? kind : humanise(parts[parts.length - 1] ?? '');
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

/**
 * Rows Master's seed marks creatable whose screens have no form for one record,
 * so the + would open the list and nothing else (TK-189): fee demands are raised
 * in bulk from a fee structure, and every dish already has a recipe row to edit.
 */
const NO_CREATE_FORM: ReadonlySet<string> = new Set(['fdm', 'rst-recipes']);

/** A path segment as words: `hsn-sac` → `Hsn sac`. */
function humanise(segment: string): string {
  const words = decodeURIComponent(segment).replace(/-/g, ' ');
  return words.charAt(0).toUpperCase() + words.slice(1);
}
