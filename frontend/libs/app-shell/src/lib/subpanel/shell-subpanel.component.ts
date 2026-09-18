import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MenuGroupView, SubMenuView } from '../menu.models';
import { MenuService } from '../menu.service';
import { ShellPanelService } from '../panel-state.service';

/**
 * The secondary menu: the module's own screens, beside the rail.
 *
 * Which module it shows follows the URL rather than a click — whatever screen you
 * are on, the panel is that module's. That keeps the panel and the content from
 * ever disagreeing, which is the failure the design's own note about the rail warns
 * against.
 *
 * Sections collapse one at a time, an unnamed section is always open and draws no
 * header, a row reveals its create button on hover, and Reports gets a search box
 * because forty-six rows is too many to scan. The right edge drags to resize.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-subpanel',
  standalone: true,
  imports: [],
  templateUrl: './shell-subpanel.component.html',
  styleUrl: './shell-subpanel.component.scss',
})
export class ShellSubpanelComponent {
  private readonly menuService = inject(MenuService);
  private readonly router = inject(Router);
  protected readonly panel = inject(ShellPanelService);

  /** The current URL, as a signal so the panel follows navigation. */
  private readonly url = signal('/');

  readonly menu = computed(() => this.menuService.menuForPath(this.url()));

  readonly label = computed(() => this.menu()?.name ?? '');

  readonly searchable = computed(() => this.menu()?.isSearchable ?? false);

  readonly query = signal('');

  /**
   * The sections, with the search applied. Searching flattens nothing — it only
   * removes rows that do not match, and then sections left empty — so a hit keeps
   * the heading that explains where it sits.
   */
  readonly groups = computed<MenuGroupView[]>(() => {
    const menu = this.menu();
    if (!menu) return [];

    const q = this.query().trim().toLowerCase();
    if (!q) return [...menu.groups];

    return menu.groups
      .map((group) => ({
        ...group,
        subMenus: group.subMenus.filter((sub) =>
          `${sub.name} ${group.name ?? ''}`.toLowerCase().includes(q),
        ),
      }))
      .filter((group) => group.subMenus.length > 0);
  });

  readonly count = computed(() =>
    this.groups().reduce((total, group) => total + group.subMenus.length, 0),
  );

  /** Nothing to show means no panel at all, not an empty one. */
  readonly visible = computed(() => !this.panel.collapsed() && this.groups().length > 0);

  constructor() {
    this.url.set(this.router.url);

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.url.set(event.urlAfterRedirects);
        // A search is about finding one screen. Once you are on it, it has done
        // its job and should not still be narrowing the list you come back to.
        this.query.set('');
        this.openSectionForCurrentUrl();
      });

    this.openSectionForCurrentUrl();
  }

  setQuery(value: string): void {
    this.query.set(value);
  }

  isOpen(group: MenuGroupView): boolean {
    // While searching every section stands open: hiding a hit behind a collapsed
    // header is the opposite of what searching asked for.
    if (this.query().trim()) return true;
    return this.panel.isGroupOpen(group.code, !!group.name);
  }

  toggleGroup(group: MenuGroupView): void {
    if (!group.name) return;
    this.panel.toggleGroup(group.code);
  }

  /**
   * The one row the current URL is on, as a route rather than a boolean.
   *
   * Resolved once for the whole panel instead of asked of each row, because the
   * answer is not local: several rows share a path and differ only by their query
   * — `/sales/transactions?type=Quote` and its siblings are all the same register
   * filtered differently. A row-by-row test that strips the query marks every one
   * of them at once, which is the bug this replaces.
   */
  private readonly currentRoute = computed<string | null>(() => {
    const url = this.url();
    const rows = (this.menu()?.groups ?? []).flatMap((group) => group.subMenus);

    const exact = rows.find((s) => s.routePath === url);
    if (exact?.routePath) return exact.routePath;

    // Falling back to the bare path is only safe for a row that carries no query
    // of its own; a typed register has to be matched on its type or not at all.
    const path = url.split('?')[0];
    const bare = rows.find((s) => !!s.routePath && !s.routePath.includes('?') && s.routePath === path);
    return bare?.routePath ?? null;
  });

  isCurrent(sub: SubMenuView): boolean {
    return !!sub.routePath && sub.routePath === this.currentRoute();
  }

  go(sub: SubMenuView): void {
    if (!sub.routePath) return;
    void this.router.navigateByUrl(sub.routePath);
  }

  /**
   * The create route for a row, derived from its own: the design's `+` opens the
   * new-document form for that register. Only offered where the tree says the
   * person may create, so a role without it never sees the button.
   */
  create(sub: SubMenuView, event: Event): void {
    event.stopPropagation();
    const path = this.createPath(sub);
    if (path) void this.router.navigateByUrl(path);
  }

  canCreate(sub: SubMenuView): boolean {
    return sub.canCreate && sub.allowedActions.includes('create') && !!this.createPath(sub);
  }

  createLabel(sub: SubMenuView): string {
    return `New ${(sub.singularName ?? sub.name).toLowerCase()}`;
  }

  /**
   * `/sales/transactions?type=Invoice` creates at `/sales/invoices/new`, so the
   * form path cannot be derived from the register's path by appending. The tree
   * does not carry a create route, so this maps the document codes it does carry.
   */
  private createPath(sub: SubMenuView): string | null {
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
    };
    if (known[sub.code]) return known[sub.code];

    // Everything else creates in place: a master-data screen opens its own form.
    return sub.routePath ? `${sub.routePath.split('?')[0]}?action=create` : null;
  }

  /** Open the section holding the screen you just landed on. */
  private openSectionForCurrentUrl(): void {
    const group = this.groups().find((g) => g.subMenus.some((s) => this.isCurrent(s)));
    if (group?.name) this.panel.openGroup.set(group.code);
  }

  // --- Resize ---------------------------------------------------------------

  /**
   * Pointer events rather than mouse: the same handler then works for a trackpad,
   * a touch screen and a pen, and capture means the drag survives the pointer
   * leaving the 5px handle, which it does immediately.
   */
  startResize(event: PointerEvent): void {
    if (event.button !== 0) return;
    event.preventDefault();

    const handle = event.target as HTMLElement;
    const startX = event.clientX;
    const startWidth = this.panel.width();

    handle.setPointerCapture?.(event.pointerId);

    const move = (e: PointerEvent) => this.panel.setWidth(startWidth + e.clientX - startX);
    const up = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', up);
      handle.removeEventListener('pointercancel', up);
      handle.releasePointerCapture?.(event.pointerId);
    };

    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', up);
    handle.addEventListener('pointercancel', up);
  }
}
