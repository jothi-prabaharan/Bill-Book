/**
 * The navigation tree as `GET /api/menu` returns it.
 *
 * The server has already filtered this for the caller: a screen they hold no
 * permission on is gone, an empty group is gone, and a module left with nothing
 * to show is gone with it. Nothing here needs filtering again on the client — the
 * shape is what this user may actually reach.
 */
export interface MenuView {
  readonly menuId: number;
  readonly code: string;
  readonly name: string;
  readonly icon: string | null;
  readonly module: string;
  /** Set when the rail entry navigates directly instead of opening a panel. */
  readonly routePath: string | null;
  readonly isSearchable: boolean;
  readonly groups: readonly MenuGroupView[];
}

/** A section of a module's panel. `name` is null for a flat list. */
export interface MenuGroupView {
  readonly menuGroupId: number;
  readonly code: string;
  readonly name: string | null;
  /** Lucide icon name for the section heading. */
  readonly icon: string | null;
  readonly subMenus: readonly SubMenuView[];
}

export interface SubMenuView {
  readonly subMenuId: number;
  readonly code: string;
  readonly name: string;
  readonly routePath: string | null;
  readonly icon: string | null;
  readonly module: string;
  readonly canCreate: boolean;
  /** What one record is called — "Invoice" — for the create button's label. */
  readonly singularName: string | null;
  readonly hasAccess: boolean;
  /**
   * What this user may do here: view, create, edit, delete, print, export, void,
   * approve. A screen can hide the buttons the role cannot use.
   */
  readonly allowedActions: readonly string[];
}
