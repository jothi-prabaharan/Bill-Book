# Milestone 1 Specification Miner Handoff Report

**Date**: 2026-08-19  
**Agent**: `spec_miner_m1_2` (Milestone 1 Explorer: Design Tokens & Theming)  
**Working Directory**: `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_m1_2`  
**Parent / Caller**: `81ce1b4e-8b82-482d-87dd-d3c3263fc136` / `cc978969-df66-403f-b02a-6feb6cefd6fe`

---

## 1. Observation

1. **Design Tokens Source**: Probed `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css`:
   - Lines 4–10: Ground `--color-bg: #f3f2f2`, surface `--color-surface: #eae9e9`, text `--color-text: #201f1d`, primary accent `--color-accent: #b68235` (retuned to `#f06311` brand orange-gold in `Shell.dc.html:15` and `_auth.scss:13`), `--color-accent-2: #ac803e`, and divider `--color-divider: color-mix(in srgb, #201f1d 16%, transparent)`.
   - Lines 14–42: 100–900 OKLCH tonal ramps for Neutral (`#f8f4f4` to `#2d2b2b`), Accent (`#fff3e4`/`#fdefe4` to `#3a270d`), and Accent-2 (`#fff3e4` to `#382810`).
   - Lines 44–48: Typography definitions `--font-heading: "Cormorant Garamond", system-ui, sans-serif`, `--font-heading-weight: 600`, `--font-body: "Lora", system-ui, sans-serif`.
   - Lines 50–55: Spacing scale `--space-1` (4.6px / 3px compact) to `--space-8` (36.8px / 24px compact).
   - Lines 57–59: Radii `--radius-sm: 2px`, `--radius-md: 4px`, `--radius-lg: 7px`.
   - Lines 63–65: Whisper shadows `--shadow-sm`, `--shadow-md`, `--shadow-lg` based on `color-mix(in srgb, #2d2b2b [14-22]%, transparent)`.
   - Lines 106–108: Focus reset `:focus { outline: none; }` and accessible `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }`.

2. **Design Philosophy and Tabular Numbers**:
   - `_ds/.../readme.md:22`: "Numbers set tabular wherever they stand as figures or columns — kickers, contents numbers, tables, charts, the display numerals (`"tnum"`; both faces keep their own figure style and gain equal widths) — while running prose keeps its text figures".
   - `_ds/.../readme.md:14`: "Apply color as borders, rules and underlines — not as filled blocks. Buttons are outlined (1px accent border on transparent), not solid-filled."

3. **Current Codebase State**:
   - `frontend/libs/shared/theming/src/` currently contains only `.gitkeep` (no SCSS partials or TypeScript exports exist yet).
   - `frontend/apps/web/src/styles.scss` has monolithic CSS copied from initial prototyping.
   - `frontend/tsconfig.base.json:37` maps `@bill-book/theming` to `libs/shared/theming/src/index.ts`.

---

## 2. Logic Chain

1. **Token Completeness**: `styles.css`, `Shell.dc.html`, and `_auth.scss` define a complete, closed design token vocabulary covering ground/surface colors, 100–900 OKLCH ramps, typography hierarchy (Cormorant Garamond 600 ceiling, Lora body), spacing (both default and compact density), radii, and whisper shadows.
2. **Modular Architecture Requirement**: To satisfy `PROJECT.md` M1 and `ORIGINAL_REQUEST.md` R1 & R5, the theming system must be structured into single-responsibility SCSS partials (`_tokens.scss`, `_typography.scss`, `_buttons.scss`, `_forms.scss`, `_cards.scss`, `_tags.scss`, `_table.scss`, `_dialog.scss`, `_layout.scss`, `index.scss`) and exported via `@bill-book/theming`.
3. **Tabular Numerals & Accessible Focus**: By establishing utility class `.tabular-nums` and assigning `font-variant-numeric: tabular-nums` to `.kpi`, numeric table cells, stepper indicators, and date fields, column jitter is eliminated. The global `:focus-visible` rule guarantees WCAG 2.1 compliance without blue outline leakage.
4. **CSS-Only Interactivity**: All button variants (`.btn-primary`, `.btn-secondary`, `.btn-ghost`), inputs, radios, knobs, and table row hovers use pure CSS transitions (120ms–150ms), fulfilling the strict constraint of zero JS hover/animation code.

---

## 3. Caveats

- **Brand Accent Convergence**: In the base `styles.css` token sheet, `--color-accent` is `#b68235` (classical gold), whereas in `_auth.scss` and `Shell.dc.html` it was sampled from the Bill-Book logo as `#f06311` (orange-gold) with `#ac803e`/`#b68235` retained as `--color-accent-2`. The specification retains `--color-accent: #f06311` as the active brand accent and `--color-accent-2: #ac803e` for secondary elements.
- No other caveats.

---

## 4. Conclusion

The specification mining for Milestone 1 (`shared/theming`) is complete. All tokens, typography rules, focus states, tabular numbers, and interaction states have been probed and documented with full SCSS code blocks in `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_m1_2\analysis.md`. The implementation agent can generate `libs/shared/theming/src/lib/` directly from this report.

---

## 5. Verification Method

1. **Inspect Analysis Report**:
   - Verify `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_m1_2\analysis.md` contains all 10 SCSS partial code blocks and TypeScript barrel definitions.
2. **Validate Variable Parity**:
   - Compare tokens against `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css`.
3. **Validate Rules Alignment**:
   - Ensure zero hard-coded hex or raw px where tokens exist.
   - Verify `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }`.
   - Verify `font-variant-numeric: tabular-nums` on financial columns and KPIs.
