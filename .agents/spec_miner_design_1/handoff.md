# Handoff Report: Design Specification Mining

**Agent**: Design Spec Miner (`spec_miner_design_1`)  
**Parent / Caller**: Orchestrator (`cc978969-df66-403f-b02a-6feb6cefd6fe` / `81ce1b4e-8b82-482d-87dd-d3c3263fc136`)  
**Status**: Task Complete (Hard Handoff)  
**Artifact Path**: `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_design_1\analysis.md`

---

## 1. Observation

1. **Design Tokens Source**:
   - `styles.css` (lines 4–66) defines `:root` variables including base colors (`--color-bg: #f3f2f2;`, `--color-surface: #eae9e9;`, `--color-text: #201f1d;`, `--color-accent: #b68235;`, `--color-divider: color-mix(in srgb, #201f1d 16%, transparent);`), 100–900 OKLCH ramps for neutral and accent palettes, typography (`--font-heading: "Cormorant Garamond"`, `--font-heading-weight: 600`, `--font-body: "Lora"`), spacing scale (`--space-1: 4.6px` to `--space-8: 36.8px`), radii (`--radius-sm: 2px`, `--radius-md: 4px`, `--radius-lg: 7px`), and whisper shadows (`--shadow-sm`, `--shadow-md`, `--shadow-lg`).
2. **App Shell Overrides and Structure**:
   - `Shell.dc.html` (lines 14–15) sets compact space tokens (`--space-1: 3px`, `--space-2: 7px`, `--space-3: 10px`, `--space-4: 13px`, `--space-6: 18px`, `--space-8: 24px`) and brand accent ramp (`--color-accent: #f06311`, `--color-accent-400: #f7853f`, `--color-accent-600: #c94d08`, `--color-accent-700: #a03d05`, `--color-accent-800: #7a2f04`, `--color-accent-100: #fdefe4`, `--color-ink: #2f353f`).
   - Fixed left rail is 56px wide (`nav[aria-label='Modules']`, line 97) with dark background `var(--color-ink)`. Active module state (line 22) uses an inset cutout: `background: var(--color-bg); margin: 0 -4px 0 -4px; padding-right: 4px; padding-left: 4px; box-shadow: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32,31,29,.55);`.
   - Top bar header (line 117) has height 46px, `z-index: 6`, housing searchable organization dropdown (lines 121–145) and ghost button action group (`.topacts`, lines 146–160).
   - Breadcrumb navigation (line 162) sits at `z-index: 4`, replaces page `<h1>` headings, and hosts module-level action buttons (Export, Import, Base currency toggle, lines 173–193).
3. **Data Table Architecture**:
   - Sticky header (line 41): `.listwrap .table thead th { position: sticky; top: 0; z-index: 3; background: var(--color-surface); background-clip: padding-box; border-bottom: 0; box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent); color: var(--color-accent-800); }`.
   - Row rules (line 46, 84): Hairline `1px solid var(--color-divider)` without zebra striping; compact cell vertical padding of 5px; interactive row height >= 32px; body text >= 12.5px.
   - Column filtering & sorting (lines 69–72, 920–936): Sorting buttons inside `<th>` with active opacity 1 and inactive opacity 0.25; optional sticky secondary filter row (`tr.fltrow`) at `top: 30px`.
4. **CSS-Only Interaction Rule**:
   - `styles.css` (lines 106–107) and `_auth.scss` enforce `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }` and CSS transitions without JavaScript animation.
5. **Naming & Strict Prohibitions**:
   - `ANTIGRAVITY-UI-PROMPT.md` (lines 35–37) and `AGENTS.md` mandate that the UI label for `accounting` is **Accounts**, and "Accounting" must never appear as a user-visible string.

---

## 2. Logic Chain

1. **Token Integration**: Because `styles.css` provides the authoritative design token foundation, all variables must be mapped to `:root` in `libs/shared/theming/src/lib/_tokens.scss` and imported by `apps/web/src/styles.scss`. Hardcoded hex codes, raw pixel dimensions, and browser-default blue outlines are strictly prohibited.
2. **Shell Decomposition**: `Shell.dc.html` organizes the shell into four distinct functional units: root container grid, module rail, top header bar, and breadcrumb strip. Translating this directly into four standalone Angular components (`ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent`) cleanly separates concerns and adheres to Angular v20 standalone component rules.
3. **Scrolling & Layer Stacking Integrity**: To ensure no content bleed or overlapping occurs during high-density vertical scrolling:
   - Header sits at `z-index: 6`
   - Left Rail sits at `z-index: 5`
   - Breadcrumb Strip sits at `z-index: 4`
   - Table Sticky Header sits at `z-index: 3` with opaque `background: var(--color-surface)` and `box-shadow: inset 0 -1px 0 color-mix(...)`
   - Table Data Rows sit at `z-index: 1`
   This layer hierarchy completely prevents rows from showing behind or overlapping sticky chrome.
4. **Data Table Reusability**: All module registers (`sales`, `purchase`, `inventory`, `contacts`, `accounting`) share identical table structural requirements (sticky headers, inline column search, right-aligned tabular numbers, pagination). A unified `bb-data-table` in `libs/shared/ui-components` avoids duplication.

---

## 3. Caveats

- **Mono Palette Accent Role**: In `styles.css`, `--color-accent-2-*` variables are machine-derived stand-ins that match the primary accent in lightness weight. Both sets resolve safely, but the primary `--color-accent` (`#f06311` brand orange or `#b68235` gold) should be used for all interaction outlines and active rules.
- **Icon Set Dependency**: The design system uses Lucide SVG icons directly embedded or rendered as standalone SVGs. No external font icon library is needed.

---

## 4. Conclusion

The design specification mining for Bill-Book is complete and fully documented in `analysis.md`. All design tokens, CSS custom properties, grid layouts, component structures, table rules, and interaction constraints have been thoroughly discovered and cataloged for immediate translation into `libs/shared/theming`, `libs/app-shell`, and `libs/shared/ui-components`.

---

## 5. Verification Method

To verify these findings against the authoritative files:
1. Inspect token definitions: View `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css` (lines 4–66).
2. Inspect shell markup and styles: View `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html` (lines 14–90, 97–195, 917–960).
3. Inspect complete specification analysis: View `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_design_1\analysis.md`.
