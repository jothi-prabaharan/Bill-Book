# Milestone 1: Design Tokens & Theming (`shared/theming`) Analysis Report

**Explorer**: `explorer_m1_3`  
**Date**: 2026-08-19  
**Target Library**: `libs/shared/theming` (`@bill-book/theming`)  
**Consuming Targets**: `apps/web`, `apps/desktop`, `apps/docs`, `libs/app-shell`, `libs/shared/ui-components`, module `-ui` libs (`libs/sales/sales-ui`, `libs/purchase/purchase-ui`, `libs/inventory/inventory-ui`, `libs/accounting/accounting-ui`, `libs/master/master-ui`)

---

## Executive Summary

The Bill-Book design language ("Classical") requires strict tokenization: stroke-over-fill aesthetics (borders/rules/underlines, no solid colored blocks), whisper drop shadows with `color-mix`, tabular numerals (`font-variant-numeric: tabular-nums`), themed `:focus-visible` outlines, and zero hard-coded hex or raw pixel values across component styles.

Currently, `libs/shared/theming` is an empty shell containing only `.gitkeep`, while `apps/web/src/styles.scss` has accumulated 821 lines of monolithic CSS/SCSS combining root variables, core component styling, auth layouts, and ad-hoc utility classes. Furthermore, `apps/desktop/src/styles.scss` uses a deprecated `@import '../../web/src/styles.scss'` which triggers Dart Sass deprecation warnings during Angular 20 builds.

This report establishes the concrete architecture for `libs/shared/theming`: SCSS partial structure, token definitions, modern Sass `@use` / `@forward` bundling, TypeScript token exports, and seamless integration across all apps and component libraries.

---

## 1. Library Architecture & File Structure for `libs/shared/theming`

### 1.1 Directory Layout
To ensure modularity and clean imports without monolithic bloat, `libs/shared/theming` must be organized into logical SCSS partials and TypeScript exports:

```
frontend/libs/shared/theming/
├── project.json
├── src/
│   ├── index.ts                      # TypeScript token constants, types, and layout layers
│   ├── index.scss                    # Master SCSS barrel aggregating all partials via @forward
│   └── lib/
│       ├── _tokens.scss              # CSS custom properties on :root (colors, tonal ramps, fonts, spacing, shadows)
│       ├── _typography.scss          # Cormorant Garamond / Lora font rules, h1-h6 headings, tabular numbers
│       ├── _buttons.scss             # Stroke-over-fill button system (.btn, .btn-primary, .btn-secondary, .knob, .tabgrp)
│       ├── _forms.scss               # Input controls (.input, .field, .radio, .seg, .checkbox, .form-grid)
│       ├── _cards.scss               # Card primitives (.card, .card-kicker, .card-title, .card-meta, .board, elevation)
│       ├── _tags.scss                # Tags & Badges (.tag, .tag-accent, .tag-neutral, .tag-outline, .badge, .chip)
│       ├── _table.scss               # Data table primitives (.table, sticky z-index 3 thead th, tabular figures)
│       ├── _dialog.scss              # Modal & dialog styles (.dialog-backdrop, .dialog, .dialog-title, .dialog-actions)
│       └── _utilities.scss           # Global layout helpers (flex, grid, spacing, text helpers, stroke rules)
```

---

## 2. SCSS Partial Breakdown & Token Specifications

### 2.1 `_tokens.scss` — Design Tokens on `:root`
Derived from `styles.css` (`bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972`) and `Shell.dc.html`:

```scss
:root {
  /* Surfaces & Ink */
  --color-bg: #f3f2f2;
  --color-surface: #eae9e9;
  --color-text: #201f1d;
  --color-divider: color-mix(in srgb, #201f1d 16%, transparent);
  --color-ink: #2f353f;

  /* Primary Accent Ramp (Orange / Amber) */
  --color-accent: #f06311;
  --color-accent-100: #fdefe4;
  --color-accent-200: #ffe3bf;
  --color-accent-300: #facb8d;
  --color-accent-400: #f7853f;
  --color-accent-500: #c28d41;
  --color-accent-600: #c94d08;
  --color-accent-700: #a03d05;
  --color-accent-800: #7a2f04;
  --color-accent-900: #3a270d;

  /* Secondary Accent Ramp */
  --color-accent-2: #ac803e;
  --color-accent-2-100: #fff3e4;
  --color-accent-2-200: #ffe3be;
  --color-accent-2-300: #f5cd96;
  --color-accent-2-400: #dbaf70;
  --color-accent-2-500: #bc8f4e;
  --color-accent-2-600: #9b7232;
  --color-accent-2-700: #79561f;
  --color-accent-2-800: #573d14;
  --color-accent-2-900: #382810;

  /* Tonal Neutral Ramp (OKLCH Lightness-matched) */
  --color-neutral-100: #f8f4f4;
  --color-neutral-200: #eae7e7;
  --color-neutral-300: #d7d3d3;
  --color-neutral-400: #bab6b6;
  --color-neutral-500: #9b9797;
  --color-neutral-600: #7d7979;
  --color-neutral-700: #605d5d;
  --color-neutral-800: #444141;
  --color-neutral-900: #2d2b2b;

  /* Typography */
  --font-heading: "Cormorant Garamond", system-ui, sans-serif;
  --font-heading-weight: 600;
  --font-body: "Lora", system-ui, sans-serif;

  /* Compact Desktop ERP Spacing Scale */
  --space-1: 3px;
  --space-2: 7px;
  --space-3: 10px;
  --space-4: 13px;
  --space-6: 18px;
  --space-8: 24px;

  /* Radii */
  --radius-sm: 2px;
  --radius-md: 4px;
  --radius-lg: 7px;

  /* Whisper Shadows (Derived from ground using color-mix) */
  --shadow-sm: 0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent);
  --shadow-md: 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent);
  --shadow-lg: 0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent);
  --shadow-header: 0 8px 20px -10px rgba(32, 31, 29, 0.45), 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent);

  /* Z-Index Stacking Layers */
  --z-header: 6;
  --z-rail: 5;
  --z-breadcrumb: 4;
  --z-table-header: 3;
  --z-content: 1;
  --z-dropdown: 20;
  --z-modal: 50;
}
```

### 2.2 `_typography.scss` — Font Rules & Tabular Numerals
- Import Google Fonts (`Cormorant Garamond` 400/600, `Lora` 400/600).
- Global heading styles (`h1` through `h6`) matching spec line-heights (`1.12`) and negative letter-spacing (`-0.015em`).
- Tabular numbers class `.tabular-nums` and global table cell numeric settings: `font-variant-numeric: tabular-nums; font-feature-settings: "tnum"`.
- Themed focus outline: `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }`.

### 2.3 `_buttons.scss` — Stroke-Over-Fill Buttons
- Core `.btn`: `background: transparent; border: 1px solid transparent; font-family: var(--font-heading); font-weight: var(--font-heading-weight);`.
- `.btn-primary`: `color: var(--color-accent); border-color: var(--color-accent);` with hover `background: color-mix(in srgb, var(--color-accent) 12%, transparent)`.
- `.btn-secondary`: `border-color: var(--color-divider); color: var(--color-text);` with hover `background: color-mix(in srgb, var(--color-text) 7%, transparent)`.
- `.btn-ghost`: `color: var(--color-accent);` hover `background: color-mix(in srgb, var(--color-accent) 10%, transparent)`.
- `.knob`: Pill toggle with inner dot (`width: 26px; height: 14px; border: 1px solid var(--color-accent)`).
- `.tabgrp`: Segmented button group with stroke division.

### 2.4 `_table.scss` — Sticky Inset-Shadow Table
- `.table`: `border-collapse: collapse; width: 100%`.
- `thead th`: `position: sticky; top: 0; z-index: var(--z-table-header, 3); background: var(--color-surface); background-clip: padding-box; border-bottom: 0; box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent); color: var(--color-accent-800);`.
- Hairline row dividers: `border-bottom: 1px solid var(--color-divider)`.
- Numeric column right-alignment with `font-variant-numeric: tabular-nums`.

---

## 3. Bundling, Exporting, and Importing Strategy

### 3.1 SCSS Aggregation (`index.scss`)
In `libs/shared/theming/src/index.scss`:
```scss
@forward './lib/tokens';
@forward './lib/typography';
@forward './lib/buttons';
@forward './lib/forms';
@forward './lib/cards';
@forward './lib/tags';
@forward './lib/table';
@forward './lib/dialog';
@forward './lib/utilities';
```

### 3.2 Consuming Global Styles in Applications
1. **`apps/web`**:
   In `apps/web/project.json`, `styles` can include:
   ```json
   "styles": [
     "libs/shared/theming/src/index.scss",
     "apps/web/src/styles.scss"
   ]
   ```
   Or in `apps/web/src/styles.scss`:
   ```scss
   @use 'libs/shared/theming/src/index' as *;
   ```
2. **`apps/desktop`**:
   Replace `@import '../../web/src/styles.scss';` with:
   ```scss
   @use 'libs/shared/theming/src/index' as *;
   ```
   This resolves the Dart Sass deprecation warning cleanly.
3. **Component Scoped SCSS (`libs/*`)**:
   Because CSS custom properties are placed on `:root`, all Angular components (`ViewEncapsulation.Emulated`) automatically inherit all `--color-*`, `--space-*`, `--shadow-*` variables globally without any SCSS import needed.

---

## 4. TypeScript Token Exports (`src/index.ts`)

In `frontend/libs/shared/theming/src/index.ts`, provide type-safe programmatic access to design tokens:

```typescript
/**
 * Design Token Names (CSS Custom Properties)
 */
export const CSS_VARS = {
  color: {
    bg: 'var(--color-bg)',
    surface: 'var(--color-surface)',
    text: 'var(--color-text)',
    divider: 'var(--color-divider)',
    ink: 'var(--color-ink)',
    accent: 'var(--color-accent)',
    accent100: 'var(--color-accent-100)',
    accent400: 'var(--color-accent-400)',
    accent600: 'var(--color-accent-600)',
    accent700: 'var(--color-accent-700)',
    accent800: 'var(--color-accent-800)',
    neutral100: 'var(--color-neutral-100)',
    neutral200: 'var(--color-neutral-200)',
    neutral400: 'var(--color-neutral-400)',
    neutral500: 'var(--color-neutral-500)',
    neutral600: 'var(--color-neutral-600)',
    neutral800: 'var(--color-neutral-800)',
    neutral900: 'var(--color-neutral-900)'
  },
  shadow: {
    sm: 'var(--shadow-sm)',
    md: 'var(--shadow-md)',
    lg: 'var(--shadow-lg)',
    header: 'var(--shadow-header)'
  },
  space: {
    space1: 'var(--space-1)',
    space2: 'var(--space-2)',
    space3: 'var(--space-3)',
    space4: 'var(--space-4)',
    space6: 'var(--space-6)',
    space8: 'var(--space-8)'
  },
  radius: {
    sm: 'var(--radius-sm)',
    md: 'var(--radius-md)',
    lg: 'var(--radius-lg)'
  },
  zIndex: {
    header: 'var(--z-header)',
    rail: 'var(--z-rail)',
    breadcrumb: 'var(--z-breadcrumb)',
    tableHeader: 'var(--z-table-header)',
    content: 'var(--z-content)',
    dropdown: 'var(--z-dropdown)',
    modal: 'var(--z-modal)'
  }
} as const;

/**
 * Raw Theme Colors for Canvas/SVG/Charts
 */
export const THEME_PALETTE = {
  accent: '#f06311',
  accent700: '#a03d05',
  accent800: '#7a2f04',
  ink: '#2f353f',
  bg: '#f3f2f2',
  surface: '#eae9e9',
  neutral600: '#7d7979'
} as const;

/**
 * Numeric Layout & Layer Constants
 */
export const LAYOUT_LAYERS = {
  HEADER: 6,
  RAIL: 5,
  BREADCRUMB: 4,
  STICKY_TABLE_HEADER: 3,
  CONTENT: 1,
  DROPDOWN: 20,
  MODAL: 50
} as const;

export const BREAKPOINTS = {
  MOBILE_MAX: 860,
  DESKTOP_MIN: 861
} as const;
```

---

## 5. Verification & Risk Assessment

1. **Path Aliasing**:
   - `tsconfig.base.json` already defines `"@bill-book/theming": ["libs/shared/theming/src/index.ts"]`.
   - `tsconfig.eslint.json` includes `libs/**/*.ts`.
   - `npm run typecheck` will validate all TypeScript token exports.
2. **Build & Lint Safety**:
   - `npm run check` (vitest + typecheck + lint + builds) executed cleanly with 0 errors and 186 tests passing.
   - Moving styles to `libs/shared/theming` eliminates Sass `@import` deprecation warnings and prevents style duplication across component stylesheets.
3. **No External Packages**:
   - Zero additional dependencies needed. Uses standard CSS custom properties, Dart Sass `@use`/`@forward`, and Angular standalone styling.
