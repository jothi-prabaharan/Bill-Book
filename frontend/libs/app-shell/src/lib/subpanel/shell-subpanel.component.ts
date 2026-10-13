import { FormsModule } from '@angular/forms';
import { SearchInputComponent } from '@bill-book/ui-components';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MenuGroupView, SubMenuView } from '../menu.models';
import { MenuService } from '../menu.service';
import { ShellPanelService } from '../panel-state.service';
import { ShellNavigationService } from '../shell-navigation.service';

/**
 * The secondary menu: the module's own screens, beside the rail, or a bottom
 * sheet on a phone.
 *
 * It shows the module `ShellNavigationService` says is open: the one picked on
 * the rail, else the one that owns the screen on view. Picking a row navigates,
 * which hands the menu back to the screen's module, so the rail, the panel and
 * the content agree again after every navigation (TK-189).
 *
 * Sections collapse one at a time, an unnamed section is always open and draws no
 * header, a row reveals its create button on hover, and Reports gets a search box
 * because forty-six rows is too many to scan. The right edge drags to resize.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-subpanel',
  standalone: true,
  imports: [
    SearchInputComponent, FormsModule],
  templateUrl: './shell-subpanel.component.html',
  styleUrl: './shell-subpanel.component.scss',
})
export class ShellSubpanelComponent {
  private readonly menuService = inject(MenuService);
  private readonly router = inject(Router);
  protected readonly panel = inject(ShellPanelService);
  protected readonly navigation = inject(ShellNavigationService);

  /** The current URL, as a signal so the panel follows navigation. */
  private readonly url = signal('/');

  readonly menu = computed(() => this.navigation.panelMenu());

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

  /**
   * A module with no secondary menu has no panel at all, not an empty one. A
   * search that matches nothing keeps the panel and its search box, and says so
   * (TK-189: it used to remove the box being typed into).
   */
  private readonly hasRows = computed(() => (this.menu()?.groups.length ?? 0) > 0);

  /** The desktop panel. */
  readonly visible = computed(
    () => !this.navigation.isPhone() && !this.panel.collapsed() && this.hasRows(),
  );

  /** The phone's bottom sheet. */
  readonly sheetVisible = computed(
    () => this.navigation.isPhone() && this.navigation.sheetOpen() && this.hasRows(),
  );

  constructor() {
    this.url.set(this.router.url);

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.url.set(event.urlAfterRedirects);
        // A search is about finding one screen. Once you are on it, it has done
        // its job and should not still be narrowing the list you come back to.
        this.query.set('');
        this.openCurrentSection();
      });

    this.openCurrentSection();

    // And again when the tree arrives after the first paint, which on a reload or
    // a deep link it always does: until then there is no section to open (TK-189).
    // Only the screen's row and the menu are tracked, so opening or shutting a
    // section by hand is never undone until the screen changes.
    effect(() => {
      this.currentRoute();
      this.menu();
      untracked(() => this.openCurrentSection());
    });
  }

  /** Open the named section holding the screen on view, if the open menu has one. */
  private openCurrentSection(): void {
    const menu = this.menu();
    if (!menu || menu.groups.length === 0) return;
    const route = this.currentRoute();
    const group = route
      ? menu.groups.find((g) => g.subMenus.some((s) => s.routePath === route))
      : null;
    if (group?.name) {
      this.panel.openGroup.set(group.code);
    } else if (this.panel.openGroup() === null || !menu.groups.some((g) => g.code === this.panel.openGroup())) {
      const firstNamed = menu.groups.find((g) => !!g.name);
      if (firstNamed) {
        this.panel.openGroup.set(firstNamed.code);
      }
    }
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
    if (bare?.routePath) return bare.routePath;

    // A form or a record under a row — /sales/invoices/new, /sales/invoices/42 —
    // keeps that row marked, as the design does while a document is being made.
    const parent = this.menuService.rowForPath(url);
    return parent?.routePath && rows.includes(parent) ? parent.routePath : null;
  });

  isCurrent(sub: SubMenuView): boolean {
    return !!sub.routePath && sub.routePath === this.currentRoute();
  }

  go(sub: SubMenuView): void {
    if (!sub.routePath) return;
    // The sheet closes on the pick itself, so picking the screen already on view
    // (which navigates nowhere) still closes it.
    this.navigation.sheetOpen.set(false);
    void this.router.navigateByUrl(sub.routePath);
  }

  /**
   * The create route for a row, derived from its own: the design's `+` opens the
   * new-document form for that register. Only offered where the tree says the
   * person may create, so a role without it never sees the button.
   */
  create(sub: SubMenuView, event: Event): void {
    event.stopPropagation();
    this.navigation.sheetOpen.set(false);
    const path = this.createPath(sub);
    if (path) void this.router.navigateByUrl(path);
  }

  canCreate(sub: SubMenuView): boolean {
    return sub.canCreate && sub.allowedActions.includes('create') && !!this.createPath(sub);
  }

  createLabel(sub: SubMenuView): string {
    return `New ${(sub.singularName ?? sub.name).toLowerCase()}`;
  }

  /** Where the + goes for a row: its form's own route, or the list asked to open it. */
  private createPath(sub: SubMenuView): string | null {
    return this.menuService.createPath(sub);
  }

  /** The sheet's Back: close it, or return to More for a module that is not a tab. */
  back(): void {
    this.navigation.closeSheet();
  }

  /** A tap on the dim area above the sheet, not on the sheet itself, closes it. */
  onSheetBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.navigation.dismissSheet();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.navigation.sheetOpen()) this.navigation.dismissSheet();
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
