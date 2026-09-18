# Menu from the backend

The navigation tree now lives in the database and is served by `GET /api/menu`.
Before this change the endpoint existed and was seeded, but nothing called it: the
rail was a hardcoded array declared twice in the Angular shell, and the screen
registry added for Search and Favourites was a third copy.

Source of the tree: the shell design (`Shell.dc.html` — `subMenu`, `subGroups`,
`REPORT_GROUPS`, `SETTINGS_GROUPS`, `PT_DOCS`), matched row by row against the
routes the Angular app actually serves.

## Schema

**One table.** `Menu` holds all three levels, told apart by `Type`
(`Rail` | `Group` | `Item`) and joined by `ParentId`. `MenuPermission` hangs off it.

```
Menu(MenuId, ParentId?, Type, Code, Name?, Icon?, Module?, RoutePath?,
     IsSearchable, CanCreate, SingularName?, DisplayOrder, IsActive)
MenuPermission(MenuPermissionId, MenuId, PermissionCode, Action, Module)
```

The three levels share a shape — code, name, icon, order, active flag, a parent —
so three tables meant three sets of the same columns, three joins to read one menu,
and a migration every time the design grew a level. `Type` and `ParentId` carry that
structure instead, and a deeper tree now costs a row rather than a schema change.

Which columns matter depends on the type:

| Type | Parent | Uses |
|---|---|---|
| `Rail` | none | `Icon`, `Module`, `IsSearchable`; `RoutePath` only when the module has no panel (Home) |
| `Group` | a Rail | `Name` (null = unnamed section, drawn as plain rows), `Icon` |
| `Item` | a Group | `RoutePath`, `Module`, `Icon`, `CanCreate`, `SingularName`, permissions |

`Icon` is a **Lucide name** (`shopping-cart`, `users-round`), not markup, per
`DESIGN_SYSTEM.md`. The rail's `@switch` cases were renamed to match. The design
gives icons at rail level only, so the group and item icons are a choice made here
and easy to change — they are one column in the seed.

`Code` is unique among siblings. Postgres treats NULLs as distinct in a unique
index, so rail modules (whose `ParentId` is null) get a second, filtered unique
index to hold them to the same rule.

Ids are blocked by level — rails 1–99, groups 100–999, items from 1000 — so a row's
level is readable from its id while debugging.

### Placement and permission are separate

The design files HSN/SAC, UOM types and metal purity under **Inventory**, and
reconciliation under **Banking**. Those routes live under `/settings` and
`/accounting` and are guarded accordingly. The row sits where the design puts it;
`Module` carries the route's own guard. Without that split the menu would offer a
screen the router then refuses.

## Seed

138 rows — 9 rails, 17 groups, 112 items — and 403 permission rows.

| Module | Icon | Groups | Screens | With a route |
|---|---|---:|---:|---:|
| Home | house | — | direct to `/dashboard` | 1 |
| Contacts | users-round | 1 | 3 | 1 |
| Inventory | boxes | 1 | 10 | 9 |
| Purchase | package | 1 | 5 | 1 |
| Sales | shopping-cart | 1 | 6 | 3 |
| Banking | landmark | 1 | 9 | 7 |
| Accounts | book-open | 1 | 5 | 4 |
| Reports | chart-no-axes-combined | 7 | 46 | 46 |
| Settings | settings | 4 | 28 | 11 |

**22 rows have no route and are seeded `IsActive = false`.** These are screens the
design covers that are not built: account types, units of measure, licences,
permissions, user organisation roles, login history, the banking dashboard and
transaction screens, and the 12 print templates. `MenuService` filters on `IsActive`, so none of them reach a
user; switching one on later is a data change, not a code change.

All 46 reports have working URLs because `reporting.routes.ts` has a `:reportKey`
host. Four point at dedicated pages instead: Balance Sheet and Profit & Loss at
`/reports/statements/…`, Trial Balance and General ledger at `/accounting/…`.

**Every document type has its own register.** The type travels in the URL —
`/sales/transactions?type=Quote`, `/purchase/transactions?type=Bill` — so a filtered
list is linkable, bookmarkable and reachable with the back button, which it was not
while the type lived only in a clicked tab. `SalesListComponent` and
`PurchaseListPage` read the parameter on init and push it back when a tab changes,
so the address bar and the screen cannot disagree.

Two rows keep dedicated pages instead: **Sales orders** and **Invoices** have list
components that filter by fulfilment and by overdue, which the mixed register cannot
do — `sales.routes.ts` says as much in its own comments.

Sales and Purchase also keep an **All transactions** row for the untyped view, which
is the design's own "All transactions" tab.

Because some routes now carry a query, two places in the shell match on the full URL
before falling back to the bare path: `MenuService.subMenuFor` and the breadcrumb's
favourites star. Reversing that order would hand every typed register back as the
mixed one. Rail entries strip the query — landing pre-filtered on one document type
is not what clicking a module means.

Rows that are **not** in the design but are real reachable screens: Stock and Price
lists (Inventory), Banks and Bank accounts (Banking), Contact person roles
(Settings). They would otherwise have no way in.

Customers and Vendors are seeded inactive: the contacts page filters by role in its
own state, not from the URL, so those two rows have nowhere to point until it reads
a query parameter.

### Permission rows

Every `{module}.{action}` pair already exists — `AdminDbContext.PermissionModules`
seeds 12 modules × 10 actions. Action sets per screen kind:

- documents: view, create, edit, delete, print, export, void, approve
- banking money documents: as above without approve
- master data: view, create, edit, delete, export
- read-only and reports: view, export
- settings: view, create, edit, delete

## The API

`GET /api/menu` returns module → group → screen — the response shape is unchanged
by the move to one table, so the client did not have to follow the schema. The
service reads the rows flat in a single query and assembles the levels in memory:
about a hundred rows, cheaper to shape than to join three times. The server filters:
a screen the caller holds no permission on is dropped, an empty group is dropped,
and a module left with nothing is dropped with it. Each screen carries `hasAccess` and
`allowedActions`, so a screen can hide the buttons a role cannot use — `canView`
alone could never do that.

## The client

`MenuService` (`libs/app-shell`) loads it once on shell boot and exposes `rail`,
`screens`, `groupsFor`, `subMenuFor` and `allows(path, action)`. The rail, Search
and Favourites all read it.

`shell-screens.ts` is now a **fallback**, used only when the call fails — otherwise
a failed request would leave an empty rail. `FALLBACK_RAIL` is declared there once
and shared by both components that draw a rail, rather than each keeping its own.

A module with no route of its own lands on the first screen in its panel, because
the design's submenu panel is not built yet. When it is, the rail opens the panel
instead and `groupsFor(code)` already returns what it needs.

## Before this runs

Nothing here was compiled or tested — the workspace could not mount the repo, so
`dotnet build`, `nx build` and the test suites did not run. In order:

1. `dotnet ef migrations add SingleTableMenu --project backend/Api/Master/Master.Repository`
   — this drops `MenuGroups`, `SubMenus` and `SubMenuPermissions` and rebuilds
   `Menus` with `ParentId`/`Type` plus `MenuPermissions`. The seed ids changed, so
   expect a large diff and a full reseed.
2. `dotnet build backend/Bill-Book.sln`
3. `npx nx run-many -t lint,test -p app-shell` from `frontend/`
4. `npx nx build web`

Then sign in as a non-owner role and confirm the rail matches what that role holds.

## Still open

- The submenu panel itself is not built; the tree it needs is now available.
- `AllowedActions` reaches the client but no screen reads it yet — every New and
  Delete button is still drawn for anyone who can open the page.
- `/sales`, `/purchase` and `/reports` carry no `data.permission` in
  `app.routes.ts`, so a typed URL reaches them whatever the rail shows.

