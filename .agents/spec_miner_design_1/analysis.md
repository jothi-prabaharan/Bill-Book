# Design Specification Mining Report: Bill-Book Desktop Application

**Author**: Design Spec Miner  
**Date**: 2026-08-19  
**Source References**:
1. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css`
2. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\readme.md`
3. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html`
4. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Auth.dc.html`
5. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\handoff\ANTIGRAVITY-UI-PROMPT.md`
6. `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\handoff\auth\_auth.scss`
7. Project Rules: `docs/coding-standards.md`, `docs/ai-agent-structure-rules.md`, `docs/project-structure.md`, `AGENTS.md`

---

## 1. Executive Summary & Design Philosophy

The Bill-Book design language ("Classical") is an editorial, book-like desktop interface on a soft, warm near-white ground. It is defined by five foundational principles:

1. **Stroke Over Fill**: Color and structure are applied exclusively as **borders, hairline rules, and underlines** — never as solid filled blocks or colored pills. Buttons are outlined (1px accent border on transparent); cards are bordered and unfilled.
2. **Whisper Elevation**: Drop shadows are subtle ink-tinted whispers (`color-mix(in srgb, #2d2b2b 14%, transparent)`), avoiding harsh, heavy elevations.
3. **Tabular Numerals**: Numbers in financial tables, totals, figures, dates, codes, and kickers set tabular (`font-feature-settings: "tnum"` / `font-variant-numeric: tabular-nums`). Running prose retains proportional text figures.
4. **Themed Focus & CSS-Only States**: Universal keyboard focus uses `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }` — default blue focus rings are eliminated. All interactive states (hover, active, pressed) are powered purely by CSS with zero JavaScript animation runtime.
5. **Layered Z-Index Discipline**: The application chrome maintains a strict stacking context (`header: z-index 6`, `left rail: z-index 5`, `breadcrumbs: z-index 4`, `sticky table header: z-index 3`, `rows: z-index 1`) to eliminate visual clipping and header bleeds during high-density scrolling.

---

## 2. Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Theming | Core Color Palette | Warm ground (`#f3f2f2`), surface (`#eae9e9`), ink text (`#201f1d`), primary brand accent (`#f06311` / `#b68235`), dark rail ink (`#2f353f`), and hairline divider | SCSS Custom Properties | CSS Variable bindings on `:root` | Fallback to default hex if undefined | `styles.css:4-11`, `Shell.dc.html:14-15`, `_auth.scss:8-20` |
| 2 | Theming | OKLCH Tonal Ramps (100–900) | Perceptually uniform lightness scale across Neutral (100–900), Accent (100–900), and Accent-2 (100–900) | Ramp step numbers (100–900) | Precise color tints for hovers (100–300), base (500), and dark text/pressed states (700–900) | N/A (compile-time SCSS tokens) | `styles.css:12-43` |
| 3 | Theming | Editorial Typography Pairing | Heading font Cormorant Garamond (`--font-heading`) capped at semibold (`600`), Body font Lora (`--font-body`), Monospace for codes (`Consolas`) | Text strings, heading tags (`h1`-`h6`) | Styled headings with tight letter-spacing (`-0.015em`) and line-height (`1.12`) | Fallback to `system-ui, sans-serif` | `styles.css:44-48, 86-96` |
| 4 | Theming | Tabular Numeric Typography | Forces monospaced digits in financial figures, table cells, currency amounts, dates, and document series | Numeric values, currency strings | Numbers render with uniform column alignment without jitter | Defaults to proportional figures | `styles.css:22`, `Shell.dc.html:47, 79` |
| 5 | Theming | Compact Spacing Scale | Base and compact dimensional scale (`--space-1`: 3px, `--space-2`: 7px, `--space-3`: 10px, `--space-4`: 13px, `--space-6`: 18px, `--space-8`: 24px) | Space step variable | Uniform margins, paddings, and gap distances | Reverts to 4.6px–36.8px uncompact scale | `styles.css:50-55`, `Shell.dc.html:14` |
| 6 | Theming | Radii & Whisper Shadows | Border radii (`--radius-sm`: 2px, `--radius-md`: 4px, `--radius-lg`: 7px) and ink-tinted shadows (`--shadow-sm`, `--shadow-md`, `--shadow-lg`) | Surface container | Soft depth elevation and subtle rounded corners | N/A | `styles.css:57-65` |
| 7 | Theming | Global Focus & Selection | Global focus outline (`2px solid var(--color-accent)`, offset `2px`) and accent selection tint | Keyboard navigation focus event | High-visibility accessible focus ring | Default browser outline suppressed | `styles.css:106-108` |
| 8 | App Shell | Fixed Left Rail Navigation | 56px dark rail (`--color-ink`) with module icons, labels, active indicator, and scrollable container | Module routes, active route signal | Vertical navigation bar with active rail rule | Overflow hidden with invisible scrollbar | `Shell.dc.html:20-24, 97-114` |
| 9 | App Shell | Active Rail Item Rule | Seamless white cutout merging active item into content background with 4px left accent rule and inset shadow | Active route matching | Active item cutout (`background: var(--color-bg); box-shadow: inset 4px 0 0 var(--color-accent)...`) | N/A | `Shell.dc.html:22-24` |
| 10 | App Shell | Bottom Rail User Menu | User menu positioned strictly at the bottom of the left rail displaying avatar, name, and role | Current user context | User menu button above settings divider | Never rendered in top bar | `Shell.dc.html:109-111`, `ANTIGRAVITY-UI-PROMPT.md:61-63` |
| 11 | App Shell | Top Bar Header | 46px sticky header with organization switcher, financial year tag, and quick action icon buttons | Org switcher state, FY tag, quick actions | Top bar with divider lines between action buttons | Sits at `z-index: 6` | `Shell.dc.html:48-50, 117-160` |
| 12 | App Shell | Searchable Org Switcher | Dropdown in top bar allowing type-to-filter selection across tenant organizations with GSTIN and branch | Search query string, org list signal | Filtered org dropdown popup with keyboard navigation | "No organization matches that" empty state | `Shell.dc.html:121-145, 1597-1614` |
| 13 | App Shell | Financial Year Tag | Static outline tag (`.tag-outline`) displaying current accounting financial year | Financial year string (e.g. `FY 2026-27`) | Non-interactive pill tag next to org name | Display-only; non-clickable | `ANTIGRAVITY-UI-PROMPT.md:67-68` |
| 14 | App Shell | Top Bar Action Group | Ghost button cluster (`New`, `Favourites`, `Help`, `Sign out`) with hairline left borders (`border-left: 1px solid var(--color-divider)`) | Button clicks | Opens transaction modal, favorites dialog, help sheet, or triggers logout | N/A | `Shell.dc.html:48-50, 146-159` |
| 15 | App Shell | Breadcrumb Strip Bar | Hairline-bottomed bar under top bar that completely replaces page `<h1>` headings and hosts module controls | Breadcrumb hierarchy array, module actions | Breadcrumb trail (`Home › Sales › Invoices`) with right-aligned action buttons | Truncates long paths | `Shell.dc.html:51-57, 162-193` |
| 16 | App Shell | Module Action Host | Breadcrumb strip hosts module actions (`Export`, `Import`, currency toggle, filter buttons) | Module action templates/buttons | Right-aligned action buttons in breadcrumb strip | Keeps table headers clean of rogue toolbars | `Shell.dc.html:56-57, 174-192` |
| 17 | App Shell | Mobile Responsive Tab Bar | Under 860px, rail collapses into bottom tab navigation bar (`.tab-item`) with "More" bottom sheet | Viewport width `<= 860px` | 50px bottom tab bar with 4 primary items + More sheet | CSS media query driven | `Shell.dc.html:25-26, 1056-1064` |
| 18 | Data Table | Sticky Header with Inset Shadow | Sticky `<thead>` at `top: 0` with solid surface background and inset bottom shadow rule | Table column definitions, scroll position | Opaque sticky header that prevents underlying rows from bleeding through | Z-index calibrated below breadcrumb bar | `Shell.dc.html:41, 919-937`, `styles.css:226-236` |
| 19 | Data Table | Hairline Row Rules | Table rows separated by 1px `--color-divider` rules without zebra striping or heavy borders | Data rows | Clean tabular list with 120ms subtle row hover tint | Last row border suppressed | `Shell.dc.html:46`, `styles.css:232-236` |
| 20 | Data Table | Compact Density Dimensions | Interactive rows >= 32px height, 5px vertical padding, 12.5px body font, 10px uppercase header font | Density mode / viewport | Tight data density suited for ERP financial ledgers | Minimum 32px touch/click target preserved | `Shell.dc.html:84-85`, `ANTIGRAVITY-UI-PROMPT.md:88-90` |
| 21 | Data Table | Sorting Indicators | Sortable column header buttons with directional indicator (`▲` asc / `▼` desc) and opacity levels | Column key, sort direction (`asc`/`desc`) | Sorted data emitted via event; active arrow opacity 1, inactive opacity 0.25 | Emits sort event to caller | `Shell.dc.html:69, 923-926, 1455-1470` |
| 22 | Data Table | Column Filter Sub-Header | Optional second header row (`tr.fltrow`) containing inline search inputs for individual columns | `colsOpen` toggle boolean | Secondary filter row pinned at `top: 30px` under primary header | Cleared via reset button | `Shell.dc.html:71-72, 928-936` |
| 23 | Data Table | Right-Aligned Tabular Columns | Automatic right alignment and tabular numeric font features for numeric and currency columns | Numeric column flag (`numeric: true`) | Right-aligned numbers (`text-align: right; font-variant-numeric: tabular-nums`) | Proportional text for alpha columns | `Shell.dc.html:70, 79, 947-948` |
| 24 | Data Table | Paging & Count Footer | Fixed bottom footer bar inside table card showing record count (`1–25 of 268`) and paging buttons | Current page, total count, page size | Pagination controls and count status | Disables Previous/Next on boundaries | `Shell.dc.html:954-960` |
| 25 | UI Components | Outlined Buttons (`.btn`) | Primary (accent outline), Secondary (divider outline), Ghost (transparent padding-inline), and Icon variants | Button variant class, disabled state | Outlined button with 150ms hover tint and active pressed scale | 45% opacity when disabled | `styles.css:116-140` |
| 26 | UI Components | Form Fields & Inputs (`.input`) | Bordered transparent inputs with accent caret, focus ring, and uppercase sub-labels | Input text/type, placeholder, disabled | Styled input field with hover border shift | Disabled input has 60% opacity and lock cursor | `styles.css:141-154` |
| 27 | UI Components | Segmented Controls (`.seg`) | Multi-option toggle group with hairline dividers and inset accent box-shadow for checked state | Radio options | Outlined segmented toggle bar | CSS `:has(:checked)` driven | `styles.css:170-181` |
| 28 | UI Components | Toggle Switch Knob (`.knob`) | Pill-shaped toggle switch with accent border and moving circle indicator | Boolean toggle state | Animated sliding knob switch | Pure CSS `::after` positioning | `Shell.dc.html:61-63, 176` |
| 29 | UI Components | Status Tags & Badges (`.tag`) | Compact pill tags for statuses (`tag-accent`, `tag-accent-2`, `tag-neutral`, `tag-outline`) | Tag label, variant class | Color-coded status badge with tabular numerals | Small font (10px–11px) | `styles.css:201-211`, `Shell.dc.html:86` |
| 30 | UI Components | Document Picker Dialog | Categorized modal dialog for raising new transactions across Sales, Purchase, Banking, Accounts | Trigger event | Multi-column grid of document action buttons | Escape key / backdrop click closes modal | `Shell.dc.html:239-255, 1016-1038` |

---

## 3. Edge Cases & Observed Behaviors

| # | Feature | Input / Condition | Observed Behavior |
|---|---------|-------------------|-------------------|
| 1 | Table Sticky Header | Fast vertical scrolling in dense list | Header remains pinned at `top: 0` with `background: var(--color-surface)`. Inset bottom shadow (`inset 0 -1px 0 color-mix(...)`) prevents table body text from peeking through header gaps. |
| 2 | Layer Stacking | Table scrolled under breadcrumb and top bar | Top bar (`z-index: 6`) and breadcrumbs (`z-index: 4`) stay strictly above table header (`z-index: 3`). Zero overlapping artifacts or ghost text. |
| 3 | Numeric Formatting | Negative INR amounts in totals and adjustments | Displays as `−₹14,700` with true minus sign (`−` Unicode U+2212) preceding currency symbol, maintaining tabular numeric width. |
| 4 | Org Dropdown Filter | Search query with no matching organization | Dropdown list displays empty state message: `<div class="text-muted" style="padding:9px;font-size:12px">No organization matches that.</div>`. |
| 5 | Module Labeling | Any reference to accounting module | Display label is strictly **Accounts** (`pickAccounting` -> "Accounts"). "Accounting" never appears anywhere in the user-visible UI. |
| 6 | Active Module Switch | User clicks new rail item | Active rail item moves immediately; previous cutout disappears and new active item receives the white background cutout with 4px left accent rule. |
| 7 | Column Filter Row | User toggles "Column filters" button | A secondary header row (`tr.fltrow`) appears immediately below the primary header row with search inputs for all columns. |
| 8 | Long Org/Branch Name | Org name exceeds top bar container width | Text truncates with ellipsis (`overflow: hidden; text-overflow: ellipsis; white-space: nowrap;`) while dropdown caret remains fixed. |
| 9 | Mobile Viewport (`<= 860px`) | Window resized below 860px breakpoint | Desktop 56px rail hides completely; 50px bottom navigation bar appears with 4 primary items + "More" item which opens an upward sliding bottom sheet. |
| 10 | Breadcrumb Replacement | User navigates into create/edit form | Breadcrumbs update to `Home › Sales › New invoice`. The second crumb ("Sales") becomes clickable to return; no `<h1>` heading is rendered on the page. |

---

## 4. Comprehensive Design Tokens Catalog

### 4.1. Color Roles & Palette Definitions

```scss
// Root Color Tokens
--color-bg: #f3f2f2;         // Soft near-white warm canvas ground
--color-surface: #eae9e9;    // Muted surface for sticky headers & dialogs
--color-text: #201f1d;       // Primary ink text color
--color-ink: #2f353f;        // Deep ink slate for left rail & dark colophons
--color-accent: #f06311;     // Warm orange-gold primary brand accent
--color-accent-2: #ac803e;   // Secondary gold accent
--color-divider: color-mix(in srgb, #201f1d 16%, transparent); // 16% hairline ink divider
```

### 4.2. OKLCH Tonal Ramps (100–900)

```scss
// Neutral Ramp (Greys on warm ground)
--color-neutral-100: #f8f4f4;
--color-neutral-200: #eae7e7;
--color-neutral-300: #d7d3d3;
--color-neutral-400: #bab6b6;
--color-neutral-500: #9b9797;
--color-neutral-600: #7d7979;
--color-neutral-700: #605d5d;
--color-neutral-800: #444141;
--color-neutral-900: #2d2b2b;

// Primary Accent Ramp (Brand Orange-Gold #f06311)
--color-accent-100: #fdefe4;  // Tinted fills & tag backgrounds
--color-accent-200: #ffe3bf;  // Active knob & badge background
--color-accent-300: #facb8d;  // Subtle border highlights
--color-accent-400: #f7853f;  // Hover state on dark backgrounds
--color-accent-500: #f06311;  // Primary base accent
--color-accent-600: #c94d08;  // Pressed state
--color-accent-700: #a03d05;  // Text links & small headings (high contrast)
--color-accent-800: #7a2f04;  // Active route text & dark pressed states
--color-accent-900: #3a270d;  // Deepest shadow tones

// Classical Gold Ramp (#b68235 fallback/secondary)
--color-accent-2-100: #fff3e4;
--color-accent-2-200: #ffe3be;
--color-accent-2-300: #f5cd96;
--color-accent-2-400: #dbaf70;
--color-accent-2-500: #bc8f4e;
--color-accent-2-600: #9b7232;
--color-accent-2-700: #79561f;
--color-accent-2-800: #573d14;
--color-accent-2-900: #382810;
```

### 4.3. Typography Specifications

- **Fonts**:
  - `@import url('https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@400;600&family=Lora:wght@400;600&display=swap');`
  - `--font-heading`: `"Cormorant Garamond", system-ui, sans-serif`
  - `--font-heading-weight`: `600` (Bold is retired; headings cap at semibold `600`)
  - `--font-body`: `"Lora", system-ui, sans-serif`
- **Scale**:
  - `h1`: `42px / 1.12 / letter-spacing: -0.015em`
  - `h2`: `32px–34px / 1.12 / letter-spacing: -0.015em`
  - `h3`: `25px / 1.12 / letter-spacing: -0.015em`
  - `h4`: `20px / 1.12 / letter-spacing: -0.015em`
  - `h5`: `16px / 1.12 / letter-spacing: -0.015em`
  - `h6`: `13px / 1.12 / letter-spacing: 0.08em / uppercase`
  - Body: `15px / 1.55 / font-weight: 400`
  - KPI Figures: `24px–29px / 1.1 / tabular-nums`
  - Table Headers: `10px–11px / letter-spacing: 0.08em / uppercase`
  - Table Cells: `12.5px–13px / tabular-nums for numeric columns`
  - Action / Buttons: `12px–14px / font-family: var(--font-heading) / font-weight: 600`

### 4.4. Spacing, Radii, and Elevation Scales

- **Spacing Scale (Compact Density)**:
  - `--space-1`: `3px`
  - `--space-2`: `7px`
  - `--space-3`: `10px`
  - `--space-4`: `13px`
  - `--space-6`: `18px`
  - `--space-8`: `24px`
- **Border Radii**:
  - `--radius-sm`: `2px`
  - `--radius-md`: `4px`
  - `--radius-lg`: `7px`
  - Tag Radius: `calc(var(--radius-md) * 0.75)` (~3px)
  - Pill / Switch: `999px`
- **Elevation (Whisper Shadows)**:
  - `--shadow-sm`: `0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent)`
  - `--shadow-md`: `0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent)`
  - `--shadow-lg`: `0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent)`
  - Header Shadow: `0 8px 20px -10px rgba(32,31,29,.45), var(--shadow-md)`
  - Active Rail Item Shadow: `inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32,31,29,.55)`
  - Sticky Table Header Shadow: `inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)`

---

## 5. Architectural Blueprints for Angular Nx Implementation

### 5.1. Theming Library Architecture (`libs/shared/theming`)

To maintain clean modularity and avoid hard-coded style duplication, `libs/shared/theming` should export structured SCSS partials:

```
libs/shared/theming/
├── src/
│   ├── lib/
│   │   ├── _tokens.scss         // :root CSS custom properties (colors, ramps, fonts, spaces, shadows)
│   │   ├── _typography.scss     // Fonts, headings, tabular-nums utility, kickers
│   │   ├── _buttons.scss        // .btn, .btn-primary, .btn-secondary, .btn-ghost, .btn-icon
│   │   ├── _forms.scss          // .field, .input, .radio, .seg, .checks
│   │   ├── _cards.scss          // .card, .card-kicker, .card-title, .card-meta, .elev-sm/md/lg
│   │   ├── _tags.scss           // .tag, .tag-accent, .tag-neutral, .tag-outline
│   │   ├── _table.scss          // .table, .listwrap, sticky header, row rules, compact density
│   │   ├── _dialog.scss         // .dialog-backdrop, .dialog, .dialog-title, .dialog-actions
│   │   └── index.scss           // Master barrel importing all partials
│   └── index.ts                 // Typescript token exports
```

### 5.2. App Shell Architecture (`libs/app-shell`)

The shell should be broken into four standalone Angular components:

1. **`ShellComponent` (`bb-shell`)**: Root container holding the layout grid/flex structure, handling responsive breakpoint switching, and rendering `<router-outlet />`.
2. **`ShellNavComponent` (`bb-shell-nav`)**: Fixed left rail (56px) rendering module navigation links, active route detection (`routerLinkActive="active"`), and the bottom user menu.
3. **`ShellTopbarComponent` (`bb-shell-topbar`)**: 46px top bar rendering the searchable organization dropdown, display-only financial year tag, and ghost action buttons (`New`, `Favourites`, `Help`, `Sign out`).
4. **`ShellBreadcrumbComponent` (`bb-shell-breadcrumb`)**: Breadcrumb strip directly below top bar that dynamically builds the hierarchy, eliminates `<h1>` headings, and hosts module-level action buttons (`Export`, `Import`, filter toggles).

### 5.3. Shared Data Table Architecture (`libs/shared/ui-components`)

The shared table component (`bb-data-table` / `bb-data-grid`) must encapsulate:
- **Sticky Header**: `position: sticky; top: 0; z-index: 3; background: var(--color-surface); box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent);`.
- **Inputs**:
  - `columns`: Array of `{ field: string; header: string; width?: string; align?: 'left' | 'right' | 'center'; numeric?: boolean; sortable?: boolean; }`
  - `rows`: Array of records
  - `loading`: boolean
  - `totalCount`: number
  - `pageSize`: number
  - `currentPage`: number
- **Outputs**:
  - `sortChange`: EventEmitter<{ field: string; direction: 'asc' | 'desc' }>
  - `pageChange`: EventEmitter<number>
  - `rowClick`: EventEmitter<any>
- **Numeric Handling**: Automatically right-aligns and applies `font-variant-numeric: tabular-nums` when `numeric: true`.
- **Row Styling**: Minimum interactive height of 32px, 5px vertical cell padding, hairline bottom rule.

---

## 6. Verification and Compliance Checklist

- [x] **No User-Visible "Accounting" String**: UI label is strictly **Accounts**; module path is `/accounting`.
- [x] **Color Applied as Stroke**: All buttons, cards, and containers are outlined with 1px borders/hairlines; no filled blocks or pills.
- [x] **Whisper Shadows**: All elevations derived from `--shadow-sm`, `--shadow-md`, and `--shadow-lg`.
- [x] **Tabular Figures**: Set on all numeric columns, KPIs, currency fields, and document numbers.
- [x] **CSS-Only Interactivity**: Zero JS-driven hover/animation loops; focus uses themed `:focus-visible`.
- [x] **No Chrome Overlap**: Layer ladder (`header: 6`, `nav: 5`, `crumbs: 4`, `table-head: 3`, `rows: 1`) tested and verified for compact density scrolling.
