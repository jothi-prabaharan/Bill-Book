# Design handoff — gap audit

Source: `Claude Design / Bill-Book Design-handoff` (`bill-book-design/project`), exported 2026-09-14.
Compared against `frontend/` at `main` (`2c5ed6f`).

Authority order used here: `Shell.dc.html` (the live design, per the bundle README) >
`handoff/COMPONENT-SPEC.md` > the `DESIGN_*.md` mapping docs. Where the spec and the
design file disagree, the design file wins and the difference is noted.

Most of this design is already built. What follows is only the delta.

## Decisions taken before this audit

| Question | Decision |
|---|---|
| Accent: spec `#f06311` vs repo `#b68235` | **Switch to `#f06311`** — newest design decision, sampled from the logo |
| Fonts: spec Cormorant Garamond / Lora vs repo monospace | **Keep monospace** — `fdfc8b3` was a deliberate, later call; digits align without `tnum` and there is no webfont request |

---

## A. Tokens — `libs/shared/theming/src/lib/_tokens.scss`

| Item | Repo | Design | Action |
|---|---|---|---|
| `--color-accent` | `#b68235` | `#f06311` | change |
| `--color-accent-100` | `#fff3e4` | `#fdefe4` | change |
| `--color-accent-400 / -600 / -700 / -800` | gold ramp | `#f7853f` / `#c94d08` / `#a03d05` / `#7a2f04` | change |
| remaining ramp steps (200/300/500/900) | gold | not specified | re-derive on the orange hue |
| `--color-accent-2-*` | gold | unused by the design | leave |
| spacing, radius, shadow, z-index scales | — | — | already match |
| fonts | monospace | serif pairing | keep monospace (decision above) |

Nothing else in the token sheet drifts.

## B. App shell — top bar

1. **Action group is short three buttons.** Design order is Applications · New · Search ·
   Favourites · Notifications · Help · Sign out. Repo has New · Favourites · Help · Sign out.
   Missing: **Applications menu** (globe, 262px, header + count, `menuitem` rows with an accent
   check on the current app), **Search**, **Notifications**.
2. **New and Favourites are the wrong shape.** Design: popovers anchored to their own button
   (`top: calc(100% + 6px); right: -6px`, `min(516px, 88vw)` / `min(486px, 88vw)`,
   `max-height: min(560px, 74dvh)`), each with a bordered header carrying an uppercase title and
   a tabular count, an autofocused search input, and a two-column grid of grouped `menuitem`
   rows (New: label + document code; Favourites: label + an unstar button). Repo: centred
   `.dialog-backdrop` modals of `.btn-secondary` tiles, no search, no counts, no unstar.
   Empty states missing: "No transaction type matches that." / "Star a screen to keep it here."
3. **Org switcher is an older, flatter design.** Design: 376px, header `Branches` + count,
   search placeholder "Search organisation, branch or GSTIN", **collapsible company groups**
   (`.grouphead` with count and a rotating chevron), rows with an `.avatar` of initials, branch
   name over a muted meta line, an accent check on the current row, empty "No branch matches
   that.", and a hairline footer with `Manage organizations` + `Add new`. Trigger carries a
   building icon and sets branch over locale. Repo: 290px, flat list, `bb-search-input`, no
   header, groups, avatars, check or footer.
   *(`COMPONENT-SPEC.md` §3.1 describes an older single-level list with an italic branch line —
   `Shell.dc.html` supersedes it.)*
4. **The org dropdown, New dialog and Favourites dialog are implemented twice** — once in
   `shell.component.html` and again in `shell-topbar.component.html`. One copy is dead markup.
   Resolve to a single owner (the topbar) while rebuilding.
5. Financial-year `.tag-outline` — already correct, display-only.

## C. App shell — rail and submenu

- The rail matches: items, spacer, user item, hairline, Settings, and the notch-out active
  treatment with the inset accent rule.
- **The submenu panel does not exist in the repo.** `Shell.dc.html` carries a second-level
  `nav.subpanel`: `--color-surface` ground, hairline right border, a **drag-to-resize handle**,
  a 42px header with the module label and a tabular count, an optional search input (Reports),
  **collapsible sections** with counts on an accent-7% ground, and rows whose hover reveals a
  `+` create button. `GLOBAL_COMPONENTS.md` maps it to `app-shell/src/lib/nav`.
- The breadcrumb's leading `.iconbtn` that toggles the subpanel is also missing.

## D. Breadcrumb strip

- Home controls (base-currency `.knob` toggle, Customize / Reset / Done) and Import already match.
- **Export is a plain button; the design makes it a dropdown menu** of formats with uppercase
  extension labels (PDF / Excel / CSV). `DESIGN_SYSTEM.md` flags this pattern as repeated per
  module and not yet centralised — it should land once, in the breadcrumb.
- Missing the subpanel toggle button described above.

## E. Home board

- The widgets themselves match the design's cards.
- **`Customize` is a dead button.** `dashboard.page.html` renders its own button with no click
  handler, while `ShellBreadcrumbComponent` already emits `startEdit` / `stopEdit` /
  `resetLayout` / `toggleBase` that nothing consumes.
- Missing customize mode entirely: `.board.editing` dashed accent outline and `padding-top: 38px`,
  the per-card `.wbar` with `.wbtn` move ← →, resize ⇔ and remove ✕, the span cycle
  `[3, 4, 6, 8, 12]`, the accent-100 "Add a widget" tray of removed widgets, persistence to
  `localStorage` key `billbook.dashboard.layout.v11` (`{order, spans, hidden}`, validated on
  read), and `Reset`.
- **The page renders `<h2>Dashboard</h2>` plus its own date line and Customize button.**
  Spec §5 and acceptance check 2: the breadcrumb replaces page titles, and module controls live
  in the breadcrumb strip, never on the page.
- The base-currency toggle is emitted but not consumed — foreign-currency account cards should
  respond to it with the rate and as-of date in their meta line.

## F. Register (list) pages

- Tab groups with the `+`-to-create button already match (`sales-list` is the reference).
- **No period filter bar.** Design: a hairline card with `This month` / `Last month` /
  `This financial year` / `Custom` as `aria-pressed` buttons, from/to date inputs disabled
  unless Custom, and right-aligned behind a hairline a `Total in INR` kicker over a 21px tabular
  figure summing the visible rows.
- **No column-filter row.** `_table.scss` already styles `thead tr.fltrow`, but
  `bb-data-grid` never renders one. The design makes it permanent and sticky under the header.
  Note the SCSS sets `top: 28px` where the spec says `30px` — reconcile against the real header
  height rather than trusting either number.
- **List-level search boxes exist where the design forbids them** — `sub-accounts`, `items`,
  `stock`, `contacts`, `hsn-sac`, `invoice-list`, `sales-order-list`. Spec §7.3: no list-level
  search box, no status select, no column-filter toggle. Global search is the top-bar Search
  popover; status filters from its own column input.
- Missing the `Clear` ghost button (shown while anything is filtered or sorted) and the
  visible-row count above the table.
- Header background disagrees between `_table.scss` (`--color-bg`) and
  `apps/web/src/styles.scss` (`--color-surface`, which matches the design). The app-level
  override duplicates and contradicts the theming lib — collapse to one.

## G. Page titles

Acceptance check 2 is "no `<h1>` page title anywhere". Roughly twenty module pages still render
one (`account-ledger`, `allocation-workspace`, `bank-accounts`, `banks`, `chart-of-accounts`,
`closing-dates`, `fixed-assets`, `journals`, `money-document`, and siblings). Dialog and section
`<h2>`s are fine; the page-level heading is not.

## H. Flagged, not in this pass

- **The auth handoff is stale.** `handoff/auth/*.page.html` predates the repo's
  `bb-email-input` / `bb-password-input` / `bb-checkbox` controls. Applying it verbatim would
  regress the auth screens. The tokens and layout it describes are already live.
- `design/print-template-backend-*.md` — an eighth backend service, designed but not built.
- `handoff/reports.json` (114 KB) — report definitions, not cross-checked against
  `reporting-ui`.
- Unmatched design screens with no Angular owner: Account types, Units of measure (distinct from
  UOM types), Permissions, User organisation roles, Login history, License, Branches. Listed in
  `UNMATCHED_DESIGN_PAGES.md` — these need your decision, not a unilateral one.
- Module-boundary mismatches: the design puts HSN/SAC under Inventory (Angular: Master) and
  Number series under Settings (Angular: `accounting-ui`). Reported, not moved.

---

# Status after the first implementation pass

## Closed

**Tokens.** `--color-accent` is `#f06311` with the spec's anchors at 100/400/500/600/700/800.
The three steps the spec doesn't name (200, 300, 900) were derived from the design's own
OKLCH ramp in `Shell.dc.html` — `oklch(89% .08 48)`, `oklch(81% .12 48)`, `oklch(35% .12 48)` —
rather than invented, giving `#ffccad`, `#ffa97b`, `#691e00`. The TypeScript mirror
(`theming/src/index.ts`) and its contract spec were brought back in step, and the stale
`var(--color-accent, #b68235)` fallbacks across eight component stylesheets now name the
orange. Two fallbacks in `statement-upload-form.component.scss` were still a blue from an
older theme entirely (`#3548c7`, `#eaecfb`); those are fixed too.

*Fonts stayed monospace, and the design agrees with that more than its own spec does:*
`Shell.dc.html`'s override block sets both `--font-heading` and `--font-body` to
`'IBM Plex Mono'`. `COMPONENT-SPEC.md` §1.2 still describes the Cormorant/Lora pairing and
is out of date on this point. The one loose end is `TOKENS.typography` in
`theming/src/index.ts`, which still names Cormorant Garamond and Lora while `_tokens.scss`
ships the mono stack — the two should agree.

**App shell.** The top bar now carries New, Search, Favourites and Notifications, each as a
popover anchored to its own button rather than a centred dialog, each with a bordered header
and count, an autofocused search, and grouped rows. The org switcher is the 376px panel with
its `Branches` header, count, search, initials avatars, an accent check on the current row,
and a `Manage organizations` footer. One panel is open at a time; Escape closes every panel
and clears every query; a pointerdown outside the bar closes them; so does navigating.

The duplication is gone: the org dropdown, New and Favourites existed twice, in
`shell.component.html` and `shell-topbar.component.html`, with the shell's copies dead
(nothing ever set the signals that gated them). Panels now belong to the top bar alone.
`ShellComponent` also stopped carrying board state — `ShellBoardService` owns it, which is
what lets a control in the breadcrumb strip drive a widget on the page.

The breadcrumb's `isHome` and `isRegister` were writable signals nothing ever set, so the
Home and register controls never rendered at all. Both now derive from the router.
Export became the format menu the design specifies (PDF / Excel / CSV), emitting the chosen
extension. A star was added beside the trail: Favourites had no way to add anything, which
made the panel decorative.

**Home board.** `Customize` works. Each card grows a move/resize/remove bar, widths cycle
`[3, 4, 6, 8, 12]`, removed widgets collect in an accent tray, `Reset` restores the defaults,
and the arrangement persists to `billbook.dashboard.layout.v11` — validated on read, so a
hand-edited or stale key falls back to defaults rather than throwing. The page's own
`<h2>Dashboard</h2>`, date line and dead Customize button are gone; the breadcrumb is the
title, as acceptance check 2 requires.

**Registers.** `bb-data-grid` renders the permanent sticky column-filter row (the styles had
been sitting unused in `_table.scss`), plus the `Clear` button and visible-row count above the
table. A new `bb-period-filter-bar` carries the period presets, the Custom-only date inputs,
and the `Total in INR` figure over the rows on screen; it is wired into the sales and purchase
registers. The sticky header ground moved to `--color-surface` in the theming layer and the
contradicting copy in `apps/web/src/styles.scss` was deleted.

## Deliberately not done

**The list-level search boxes stay.** The design forbids them (§7.3) because global search is
meant to be the top-bar Search popover — but that popover can only search *screens* today. The
API exposes no document, contact, item or account search (the only notification-shaped endpoint
in the Postman collection is outbound email). The boxes on `items`, `contacts`, `stock`,
`sub-accounts`, `hsn-sac`, `invoice-list` and `sales-order-list` are wired to **server-side**
filtering; the grid's column filters only narrow the page already loaded. Removing them now
would trade real capability for a rule. They should go in the same change that gives the Search
popover a document index to read.

**The Applications menu** in the design's action group is not built: it switches between
deployed apps and nothing in the repo says what those URLs are.

**Notifications have no feed.** `ShellNotificationsService` is the seam and the popover renders
its empty state honestly; when a feed exists it pushes into `set()` and no chrome changes.

**Favourites are device-local** (`billbook.favourites.v1`), for the same reason.

## Still open

- **The submenu panel does not exist.** `Shell.dc.html` carries a resizable second-level
  `nav.subpanel` next to the rail — header with count, optional search, collapsible sections,
  a `+` on row hover — plus the breadcrumb toggle that shows it. This is the largest remaining
  piece of the design and it was outside this pass.
- The period bar and column filters reached sales and purchase only. Inventory, contacts and
  accounts registers still want the same treatment.
- Roughly twenty module pages still render an `<h1>` page title.
- `TOKENS.typography` disagrees with `_tokens.scss` on the font stack (above).
- `_table.scss` sticks the filter row at `top: 28px` where the spec says `30px`. Neither was
  verified against the rendered header height — worth measuring once in the browser rather
  than trusting either number.
- The dashboard re-declares `.card`, `.table` and `.tag` locally instead of using the theming
  layer's versions.

## Verification

`npx ngc -p apps/web/tsconfig.json --noEmit` compiles clean — full AOT, templates included,
across every lib the web app reaches. ESLint is clean on everything touched. Tests: app-shell
91 passing, theming 55, data-grid 55, period filter bar 10, dashboard 12, sales-list 11,
integration 8.

One caveat on the test run: it was executed against the repo over a mounted filesystem, where
`CHAL-M1-13` (which walks the whole frontend reading files) takes ~8.7s and trips its 5s
timeout. It passes with `--testTimeout`, and should pass unaided on local disk — but worth
confirming.
