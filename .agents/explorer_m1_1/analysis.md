# Milestone 1 Exploration & Architecture Report: Design Tokens & Theming (`shared/theming`)

**Explorer**: `explorer_m1_1`  
**Milestone**: M1 (Design Tokens & Theming)  
**Target Library**: `frontend/libs/shared/theming/`  
**Date**: 2026-08-19  

---

## 1. Executive Summary

This investigation designs the architecture and provides production-ready blueprints for Milestone 1: Design Tokens & Theming (`frontend/libs/shared/theming`).

The Bill-Book design language ("Classical") enforces an editorial, book-like aesthetic with soft warm near-white ground, stroke-over-fill styling (no filled color pills or blocks), ink-tinted whisper shadows, strict tabular numeral alignment for financial data, accessible themed focus rings (`:focus-visible`), and layered z-index hierarchy for compact ERP data density.

### Key Discoveries & Architectural Decisions
1. **Directory Structure**: `libs/shared/theming/` currently has only `project.json` and an empty `src/.gitkeep`. It is mapped in `frontend/tsconfig.base.json` to `@bill-book/theming -> libs/shared/theming/src/index.ts`.
2. **SCSS Modularity**: All styles must be decomposed into granular, single-responsibility SCSS partials (`_tokens.scss`, `_typography.scss`, `_buttons.scss`, `_forms.scss`, `_cards.scss`, `_tags.scss`, `_table.scss`, `_dialog.scss`, `_utilities.scss`) orchestrated by `index.scss`.
3. **TypeScript Tokens**: `index.ts` will export a strongly typed, immutable `TOKENS` object containing all color palettes, ramps, fonts, compact spacing values, radii, and z-index constants, accompanied by unit tests in `tokens.spec.ts`.
4. **App Styles Integration**: `frontend/apps/web/src/styles.scss` and `frontend/apps/desktop/src/styles.scss` will import `libs/shared/theming/src/lib/index.scss` directly.
5. **No New Packages**: All custom properties and components use standard CSS/SCSS native capabilities (OKLCH color-mix, CSS grid/flex, pure CSS `:has()` and `:focus-visible`).

---

## 2. Token Architecture & System Design

### 2.1. Color Palette & OKLCH Ramps
- **Ground & Surfaces**:
  - `--color-bg: #f3f2f2` (Soft near-white warm canvas ground)
  - `--color-surface: #eae9e9` (Muted surface for sticky headers & dialogs)
  - `--color-text: #201f1d` (Primary ink text)
  - `--color-ink: #2f353f` (Deep ink slate for 56px left rail & colophons)
  - `--color-divider: color-mix(in srgb, #201f1d 16%, transparent)` (16% hairline ink divider)
  - `--color-border: color-mix(in srgb, #201f1d 16%, transparent)`
- **Primary Brand Accent (#f06311)**:
  - `--color-accent: #f06311` (Base accent)
  - `--color-accent-100: #fdefe4` (Tinted fills & tag backgrounds)
  - `--color-accent-200: #ffe3bf` (Active knob & badge background)
  - `--color-accent-300: #facb8d` (Subtle border highlights)
  - `--color-accent-400: #f7853f` (Hover state on dark backgrounds)
  - `--color-accent-500: #f06311` (Primary base)
  - `--color-accent-600: #c94d08` (Pressed state)
  - `--color-accent-700: #a03d05` (Text links & small headings)
  - `--color-accent-800: #7a2f04` (Active route text & dark pressed states)
  - `--color-accent-900: #3a270d` (Deepest shadow tones)
- **Accent-2 Classical Gold Ramp (#ac803e / #bc8f4e)**:
  - `--color-accent-2: #ac803e`
  - `--color-accent-2-100: #fff3e4`
  - `--color-accent-2-200: #ffe3be`
  - `--color-accent-2-300: #f5cd96`
  - `--color-accent-2-400: #dbaf70`
  - `--color-accent-2-500: #bc8f4e`
  - `--color-accent-2-600: #9b7232`
  - `--color-accent-2-700: #79561f`
  - `--color-accent-2-800: #573d14`
  - `--color-accent-2-900: #382810`
- **Neutral Ramp (Greys on warm ground)**:
  - `--color-neutral-100: #f8f4f4`
  - `--color-neutral-200: #eae7e7`
  - `--color-neutral-300: #d7d3d3`
  - `--color-neutral-400: #bab6b6`
  - `--color-neutral-500: #9b9797`
  - `--color-neutral-600: #7d7979`
  - `--color-neutral-700: #605d5d`
  - `--color-neutral-800: #444141`
  - `--color-neutral-900: #2d2b2b`

### 2.2. Compact Spacing Scale (ERP Financial Density)
- `--space-1: 3px`
- `--space-2: 7px`
- `--space-3: 10px`
- `--space-4: 13px`
- `--space-6: 18px`
- `--space-8: 24px`

### 2.3. Radii & Whisper Elevation
- **Radii**:
  - `--radius-sm: 2px`
  - `--radius-md: 4px`
  - `--radius-lg: 7px`
  - `--radius-pill: 999px`
- **Whisper Shadows**:
  - `--shadow-sm: 0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent)`
  - `--shadow-md: 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent)`
  - `--shadow-lg: 0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent)`
  - `--shadow-header: 0 8px 20px -10px rgba(32, 31, 29, 0.45), var(--shadow-md)`
  - `--shadow-rail-active: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32, 31, 29, 0.55)`
  - `--shadow-table-head: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)`

### 2.4. Typography & Tabular Figures
- **Fonts**:
  - `--font-heading: "Cormorant Garamond", system-ui, sans-serif`
  - `--font-heading-weight: 600` (Bold is retired; headings cap at semibold 600)
  - `--font-body: "Lora", system-ui, sans-serif`
  - `--font-mono: Consolas, "Courier New", monospace`
- **Heading Scale**:
  - `h1`: `42px / 1.12 / -0.015em`
  - `h2`: `32px / 1.12 / -0.015em`
  - `h3`: `25px / 1.12 / -0.015em`
  - `h4`: `20px / 1.12 / -0.015em`
  - `h5`: `16px / 1.12 / -0.015em`
  - `h6`: `13px / 1.12 / 0.08em / uppercase`
- **Tabular Figures**:
  - `.tabular-nums`: `font-variant-numeric: tabular-nums; font-feature-settings: "tnum";`
  - Applied to KPI figures, table cells, currency amounts, transaction series numbers, and dates.

### 2.5. Stacking Z-Index Hierarchy
- Topbar Header: `z-index: 6` (`--z-topbar: 6`)
- Left Rail Nav: `z-index: 5` (`--z-rail: 5`)
- Breadcrumb Strip: `z-index: 4` (`--z-breadcrumbs: 4`)
- Sticky Table Header: `z-index: 3` (`--z-table-head: 3`)
- Content Outlet / Rows: `z-index: 1` (`--z-content: 1`)

---

## 3. Complete Code Blueprints for the Worker

Below are the exact file contents to be implemented in `frontend/libs/shared/theming/`.

### 3.1. `frontend/libs/shared/theming/src/lib/_tokens.scss`

```scss
/* Classical — Design System Tokens */
@import url('https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@400;600&family=Lora:wght@400;600&display=swap');

:root {
  /* Core Color Palette */
  --color-bg: #f3f2f2;
  --color-surface: #eae9e9;
  --color-text: #201f1d;
  --color-ink: #2f353f;
  --color-accent: #f06311;
  --color-accent-2: #ac803e;
  --color-divider: color-mix(in srgb, #201f1d 16%, transparent);
  --color-border: color-mix(in srgb, #201f1d 16%, transparent);

  /* OKLCH Neutral Ramp (Greys on warm ground) */
  --color-neutral-100: #f8f4f4;
  --color-neutral-200: #eae7e7;
  --color-neutral-300: #d7d3d3;
  --color-neutral-400: #bab6b6;
  --color-neutral-500: #9b9797;
  --color-neutral-600: #7d7979;
  --color-neutral-700: #605d5d;
  --color-neutral-800: #444141;
  --color-neutral-900: #2d2b2b;

  /* Primary Brand Accent Ramp (#f06311) */
  --color-accent-100: #fdefe4;
  --color-accent-200: #ffe3bf;
  --color-accent-300: #facb8d;
  --color-accent-400: #f7853f;
  --color-accent-500: #f06311;
  --color-accent-600: #c94d08;
  --color-accent-700: #a03d05;
  --color-accent-800: #7a2f04;
  --color-accent-900: #3a270d;

  /* Secondary Classical Gold Ramp (#ac803e / #bc8f4e) */
  --color-accent-2-100: #fff3e4;
  --color-accent-2-200: #ffe3be;
  --color-accent-2-300: #f5cd96;
  --color-accent-2-400: #dbaf70;
  --color-accent-2-500: #bc8f4e;
  --color-accent-2-600: #9b7232;
  --color-accent-2-700: #79561f;
  --color-accent-2-800: #573d14;
  --color-accent-2-900: #382810;

  /* Typography */
  --font-heading: "Cormorant Garamond", system-ui, sans-serif;
  --font-heading-weight: 600;
  --font-body: "Lora", system-ui, sans-serif;
  --font-mono: Consolas, "Courier New", monospace;

  /* Compact Spacing Scale (ERP Density) */
  --space-1: 3px;
  --space-2: 7px;
  --space-3: 10px;
  --space-4: 13px;
  --space-6: 18px;
  --space-8: 24px;

  /* Border Radii */
  --radius-sm: 2px;
  --radius-md: 4px;
  --radius-lg: 7px;
  --radius-pill: 999px;

  /* Whisper Shadows */
  --shadow-sm: 0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent);
  --shadow-md: 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent);
  --shadow-lg: 0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent);
  --shadow-header: 0 8px 20px -10px rgba(32, 31, 29, 0.45), var(--shadow-md);
  --shadow-rail-active: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32, 31, 29, 0.55);
  --shadow-table-head: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent);

  /* Layout Stacking Z-Index Hierarchy */
  --z-topbar: 6;
  --z-rail: 5;
  --z-breadcrumbs: 4;
  --z-table-head: 3;
  --z-content: 1;
}

/* Global Reset & Base */
*, *::before, *::after {
  box-sizing: border-box;
}

html, body {
  height: 100%;
  margin: 0;
}

body {
  background: var(--color-bg);
  color: var(--color-text);
  font-family: var(--font-body);
  font-size: 15px;
  line-height: 1.55;
  font-weight: 400;
}

/* Modern Scrollbar */
* {
  scrollbar-width: thin;
  scrollbar-color: var(--color-neutral-400) transparent;
}
*::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
*::-webkit-scrollbar-track {
  background: transparent;
}
*::-webkit-scrollbar-thumb {
  background: var(--color-neutral-400);
  border-radius: 3px;
}
*::-webkit-scrollbar-thumb:hover {
  background: var(--color-neutral-500);
}

/* Themed Focus & Selection */
:focus {
  outline: none;
}
:focus-visible {
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}
::selection {
  background: color-mix(in srgb, var(--color-accent) 30%, transparent);
}
```

---

### 3.2. `frontend/libs/shared/theming/src/lib/_typography.scss`

```scss
/* Classical — Typography */

h1, h2, h3, h4, h5, h6 {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  line-height: 1.12;
  letter-spacing: -0.015em;
  margin: 0 0 var(--space-2);
}

h1 { font-size: 42px; }
h2 { font-size: 32px; }
h3 { font-size: 25px; }
h4 { font-size: 20px; }
h5 { font-size: 16px; }
h6 {
  font-size: 13px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

p {
  margin: 0 0 var(--space-3);
}

a {
  color: var(--color-accent-700);
  text-decoration: none;
  text-underline-offset: 3px;
  transition: color 150ms ease;

  &:hover {
    color: var(--color-accent-800);
    text-decoration: underline;
  }
}

img {
  display: block;
  max-width: 100%;
}

figure {
  margin: 0;
}

figcaption {
  font-size: 11px;
  margin-top: var(--space-1);
  color: color-mix(in srgb, var(--color-text) 55%, transparent);
}

.text-muted {
  color: color-mix(in srgb, var(--color-text) 55%, transparent);
}

.tabular-nums {
  font-variant-numeric: tabular-nums;
  font-feature-settings: "tnum";
}

.kpi {
  font-family: var(--font-heading);
  font-size: 29px;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
  font-feature-settings: "tnum";
}

.kicker {
  font-size: 10px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-accent-700);
  font-variant-numeric: tabular-nums;
}

.hr {
  height: 1px;
  border: 0;
  margin: var(--space-4) 0;
  background: var(--color-divider);
}

.plate {
  filter: sepia(0.22) saturate(0.82) contrast(1.05);
  box-sizing: border-box;
  border: 6px solid var(--color-surface);
  outline: 1px solid var(--color-divider);
}
```

---

### 3.3. `frontend/libs/shared/theming/src/lib/_buttons.scss`

```scss
/* Classical — Buttons (Stroke-over-Fill) */

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  cursor: pointer;
  text-decoration: none;
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  font-size: 13px;
  line-height: 1.2;
  color: var(--color-text);
  background: transparent;
  border: 1px solid transparent;
  padding: 5px 11px;
  border-radius: var(--radius-md);
  transition: background 150ms ease, border-color 150ms ease, color 150ms ease, box-shadow 150ms ease, transform 80ms ease;

  svg {
    display: block;
    flex: none;
  }

  &:active:not(:disabled) {
    transform: scale(0.97);
  }

  &:disabled {
    opacity: 0.45;
    cursor: not-allowed;
  }
}

.btn-primary {
  color: var(--color-accent-700);
  border-color: var(--color-accent);

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 12%, transparent);
    color: var(--color-accent-800);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 22%, transparent);
  }
}

.btn-secondary {
  border-color: var(--color-divider);

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-text) 7%, transparent);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-text) 14%, transparent);
  }
}

.btn-ghost {
  color: var(--color-accent-700);
  padding-inline: var(--space-1);
  border-color: transparent;

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 10%, transparent);
    color: var(--color-accent-800);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 18%, transparent);
  }
}

.btn-icon {
  width: 34px;
  height: 34px;
  padding: 0;
  min-height: 0;
  flex: none;
}

.btn-block {
  width: 100%;
}

.btn[aria-pressed='true'] {
  border-color: var(--color-accent);
  color: var(--color-accent-700);
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
}

/* Segmented Button Group */
.tabgrp {
  display: inline-flex;

  > .btn {
    margin: 0;
    font-size: 12px;
    padding: 5px 9px;

    &:not(:last-child) {
      border-top-right-radius: 0;
      border-bottom-right-radius: 0;
    }

    + .btn {
      width: 32px;
      min-height: 30px;
      padding: 0;
      border-left: 0;
      border-top-left-radius: 0;
      border-bottom-left-radius: 0;
      color: var(--color-accent-700);
    }
  }
}

/* Window & Board Control Buttons */
.wbar {
  position: absolute;
  top: 6px;
  right: 6px;
  display: flex;
  gap: 2px;
  background: var(--color-bg);
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);
  padding: 2px;
}

.wbtn {
  width: 24px;
  height: 24px;
  display: grid;
  place-items: center;
  border: 0;
  background: transparent;
  cursor: pointer;
  font: 12px/1 var(--font-mono);
  color: var(--color-text);
  border-radius: 3px;

  &:hover {
    background: color-mix(in srgb, var(--color-accent) 16%, transparent);
    color: var(--color-accent-700);
  }
}
```

---

### 3.4. `frontend/libs/shared/theming/src/lib/_forms.scss`

```scss
/* Classical — Forms & Inputs */

.field {
  margin: 0;

  > label {
    display: block;
    font-size: 12px;
    margin-bottom: 5px;
    color: color-mix(in srgb, var(--color-text) 70%, transparent);
  }
}

.input {
  width: 100%;
  min-height: 30px;
  padding: 4px 9px;
  font: inherit;
  font-size: 13px;
  color: var(--color-text);
  caret-color: var(--color-accent);
  background: transparent;
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);
  transition: border-color 150ms ease, box-shadow 150ms ease;

  &::placeholder {
    opacity: 0.26;
  }

  &:hover:not(:disabled) {
    border-color: color-mix(in srgb, var(--color-text) 45%, transparent);
  }

  &:focus-visible {
    border-color: var(--color-accent);
    outline: none;
    box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent);
  }

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    background: color-mix(in srgb, var(--color-text) 4%, transparent);
  }

  &[type='date'] {
    font-family: var(--font-body);

    &::-webkit-calendar-picker-indicator {
      opacity: 0.45;
      cursor: pointer;
    }
  }

  &:disabled::-webkit-calendar-picker-indicator {
    cursor: not-allowed;
  }

  &[inputmode='numeric'],
  &[inputmode='decimal'] {
    text-align: right;
    font-variant-numeric: tabular-nums;
    font-feature-settings: "tnum";
  }
}

textarea.input {
  min-height: 80px;
  resize: vertical;
}

.input--code {
  text-align: center;
  font-family: var(--font-heading);
  font-size: 26px;
  min-height: 52px;
  letter-spacing: 0.5em;
  text-indent: 0.5em;
  font-variant-numeric: tabular-nums;
  font-feature-settings: "tnum";
}

/* Radio Controls */
.radio {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;

  input {
    position: absolute;
    opacity: 0;
    width: 0;
    height: 0;
    pointer-events: none;
  }

  .dot {
    width: 16px;
    height: 16px;
    flex: none;
    border-radius: 50%;
    border: 1.5px solid var(--color-divider);
    transition: border-color 150ms ease, background 150ms ease;
  }

  &:hover .dot {
    border-color: var(--color-accent);
  }

  input:checked + .dot {
    border-color: var(--color-accent);
    background: var(--color-accent);
    box-shadow: inset 0 0 0 4px var(--color-bg);
  }

  input:focus-visible + .dot {
    outline: 2px solid var(--color-accent);
    outline-offset: 2px;
  }
}

/* Checkboxes */
.checks {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
  margin-top: var(--space-3);

  label {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 13px;
    cursor: pointer;
  }

  input {
    accent-color: var(--color-accent);
  }
}

.checkbox {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  cursor: pointer;

  input {
    accent-color: var(--color-accent);
  }
}

/* Segmented Toggle Control */
.seg {
  display: inline-flex;
  overflow: hidden;
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);
}

.seg-opt {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  font-size: 13px;
  cursor: pointer;

  input {
    position: absolute;
    opacity: 0;
    width: 0;
    height: 0;
    pointer-events: none;
  }

  + .seg-opt {
    border-left: 1px solid var(--color-divider);
  }

  &:has(input:checked) {
    color: var(--color-accent-700);
    box-shadow: inset 0 0 0 1px var(--color-accent);
    background: color-mix(in srgb, var(--color-accent) 8%, transparent);
  }

  &:not(:has(input:checked)):hover {
    background: color-mix(in srgb, var(--color-text) 7%, transparent);
  }

  &:has(input:focus-visible) {
    outline: 2px solid var(--color-accent);
    outline-offset: -2px;
  }
}

/* Pure CSS Toggle Switch Knob */
.knob {
  width: 26px;
  height: 14px;
  flex: none;
  border: 1px solid var(--color-accent);
  border-radius: var(--radius-pill);
  display: inline-flex;
  align-items: center;
  padding: 1px;
  justify-content: flex-start;
  background: transparent;
  transition: background 150ms ease, justify-content 150ms ease;

  &::after {
    content: '';
    width: 10px;
    height: 10px;
    border-radius: var(--radius-pill);
    background: var(--color-accent);
    transition: transform 150ms ease;
  }
}

[aria-pressed='true'] .knob,
.knob.active {
  justify-content: flex-end;
  background: var(--color-accent-200);
}

/* Form Layout Grids */
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: var(--space-2) var(--space-4);
  margin-top: var(--space-2);
}

.grid-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-2) var(--space-4);

  .field { margin-bottom: 0; }
  .field--wide { grid-column: 1 / -1; }
}

.form-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
  font-size: 13px;
}

.form-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-top: var(--space-4);

  .btn-primary { flex: 1; }
}
```

---

### 3.5. `frontend/libs/shared/theming/src/lib/_cards.scss`

```scss
/* Classical — Cards & Elevation */

.card {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-md);
  background: transparent;
  border: 1px solid var(--color-divider);
  transition: box-shadow 200ms ease, border-color 200ms ease;
}

.card-kicker {
  font-size: 9.5px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-accent-700);
  font-variant-numeric: tabular-nums;
  font-feature-settings: "tnum";
}

.card-title {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  font-size: 17px;
  line-height: 1.2;
}

.card-body {
  margin: 0;
  font-size: 13px;
  opacity: 0.8;
  flex: 1;
}

.card-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: color-mix(in srgb, var(--color-text) 50%, transparent);
}

/* Whisper Elevation Utilities */
.elev-sm { box-shadow: var(--shadow-sm); }
.elev-md { box-shadow: var(--shadow-md); }
.elev-lg { box-shadow: var(--shadow-lg); }

/* Dashboard & Layout Boards */
.board {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: var(--space-3);
  align-items: stretch;

  > .card {
    display: flex;
    flex-direction: column;

    > .card-meta {
      margin-top: auto;
    }

    &:hover {
      box-shadow: var(--shadow-md);
      border-color: color-mix(in srgb, var(--color-accent) 30%, transparent);
    }
  }

  &.narrow {
    grid-template-columns: 1fr;

    > * {
      grid-column: auto !important;
    }
  }

  &.editing .card {
    outline: 1px dashed color-mix(in srgb, var(--color-accent) 55%, transparent);
    outline-offset: 2px;
    padding-top: 38px;
  }
}

/* Sheet & Form-Editor Container Styles */
.sheet {
  margin-top: 1.25rem;
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-lg);
  padding: 1rem;
  max-width: 46rem;

  &.narrow { max-width: 26rem; }
  h2 { margin: 0 0 0.75rem; font-size: 1rem; }
}

.child-card {
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);
  padding: 0.85rem;
  margin-bottom: 0.75rem;
}
```

---

### 3.6. `frontend/libs/shared/theming/src/lib/_tags.scss`

```scss
/* Classical — Tags & Badges */

.tag {
  display: inline-flex;
  align-items: center;
  font-size: 10px;
  letter-spacing: 0.02em;
  padding: 2px 8px;
  border-radius: calc(var(--radius-md) * 0.75);
  font-variant-numeric: tabular-nums;
  font-feature-settings: "tnum";
  transition: background 150ms ease;
}

.tag-accent {
  background: var(--color-accent-100);
  color: var(--color-accent-800);
}

.tag-accent-2 {
  background: var(--color-accent-2-100);
  color: var(--color-accent-2-800);
}

.tag-neutral {
  background: var(--color-neutral-100);
  color: var(--color-neutral-800);
}

.tag-outline {
  border: 1px solid var(--color-accent);
  color: var(--color-accent-700);
  background: transparent;
}

/* Chips & Status Badges */
.chip {
  font-size: 10px;
  background: var(--color-accent-100);
  color: var(--color-accent-800);
  padding: 1px 6px;
  border-radius: var(--radius-sm);
  margin-right: 4px;
  font-variant-numeric: tabular-nums;
}

.badge {
  font-size: 11px;
  padding: 2px 7px;
  border-radius: var(--radius-sm);
  font-variant-numeric: tabular-nums;

  &.expired { background: #fbe4e0; color: #a52c17; }
  &.soon { background: #fdf0d5; color: #8a5b00; }
  &.valid { background: #e2f3e9; color: #187a4b; }
  &.unlinked { font-size: 10px; background: #fdf0d5; color: #8a5b00; padding: 1px 5px; border-radius: var(--radius-sm); margin-left: 5px; }
}
```

---

### 3.7. `frontend/libs/shared/theming/src/lib/_table.scss`

```scss
/* Classical — Data Tables (Sticky Header, Hairline Rules, Compact Density) */

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12.5px;

  th {
    text-align: left;
    font-size: 10px;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: color-mix(in srgb, var(--color-text) 60%, transparent);
    padding: 5px var(--space-2);
    border-bottom: 1px solid var(--color-divider);

    button {
      all: unset;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font: inherit;
      color: inherit;
    }
  }

  td {
    padding: 5px var(--space-2);
    border-bottom: 1px solid var(--color-divider);
    min-height: 32px;
  }

  thead th:first-child,
  tbody td:first-child {
    padding-left: var(--space-3);
  }

  thead th:last-child,
  tbody td:last-child {
    padding-right: var(--space-3);
  }

  tbody tr {
    transition: background 120ms ease;

    &:hover {
      background: color-mix(in srgb, var(--color-accent) 5%, transparent);
    }

    &:last-child td {
      border-bottom: 0;
    }
  }

  th.numeric,
  td.numeric,
  th[numeric],
  td[numeric] {
    text-align: right;
    font-variant-numeric: tabular-nums;
    font-feature-settings: "tnum";
  }

  tbody .input {
    min-height: 28px;
    font-size: 12.5px;
  }

  tbody .input[inputmode] {
    text-align: right;
    font-variant-numeric: tabular-nums;
  }
}

/* Sticky Header in Scrollable Table Wrapper */
.listwrap {
  .table thead th {
    position: sticky;
    top: 0;
    z-index: var(--z-table-head, 3);
    background: var(--color-surface);
    background-clip: padding-box;
    border-bottom: 0;
    box-shadow: var(--shadow-table-head);
    color: var(--color-accent-800);
    white-space: nowrap;
    padding-top: var(--space-2);
    padding-bottom: var(--space-2);
  }

  .table thead tr.fltrow th {
    padding-top: 3px;
    padding-bottom: 4px;
    box-shadow: none;
    top: 28px;
  }

  .table thead .fltrow .input {
    min-height: 24px;
    padding: 1px 7px;
    font-size: 11.5px;
  }
}
```

---

### 3.8. `frontend/libs/shared/theming/src/lib/_dialog.scss`

```scss
/* Classical — Dialogs & Modals */

.dialog-backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--space-4);
  background: color-mix(in srgb, var(--color-neutral-900) 50%, transparent);
  z-index: 100;
}

.dialog {
  width: min(440px, 100%);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-3);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--color-divider);
}

.dialog-title {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  font-size: 20px;
  margin: 0;
}

.dialog-body {
  font-size: 14px;
  opacity: 0.85;
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
  margin-top: var(--space-2);
}
```

---

### 3.9. `frontend/libs/shared/theming/src/lib/_utilities.scss`

```scss
/* Classical — Common Utility Classes */

.flex { display: flex; }
.flex-col { flex-direction: column; }
.flex-wrap { flex-wrap: wrap; }
.flex-1 { flex: 1; }
.flex-none { flex: none; }
.block { display: block; }
.grid { display: grid; }

.items-center { align-items: center; }
.items-baseline { align-items: baseline; }
.items-start { align-items: flex-start; }
.items-end { align-items: flex-end; }
.justify-between { justify-content: space-between; }
.justify-end { justify-content: flex-end; }
.justify-center { justify-content: center; }
.justify-start { justify-content: flex-start; }

.gap-1 { gap: var(--space-1); }
.gap-2 { gap: var(--space-2); }
.gap-3 { gap: var(--space-3); }
.gap-4 { gap: var(--space-4); }

.m-0 { margin: 0; }
.mt-1 { margin-top: var(--space-1); }
.mt-2 { margin-top: var(--space-2); }
.mt-3 { margin-top: var(--space-3); }
.mt-4 { margin-top: var(--space-4); }
.mb-1 { margin-bottom: var(--space-1); }
.mb-2 { margin-bottom: var(--space-2); }
.mb-3 { margin-bottom: var(--space-3); }
.mb-4 { margin-bottom: var(--space-4); }
.ml-auto { margin-left: auto; }
.mr-auto { margin-right: auto; }

.p-0 { padding: 0; }
.p-3 { padding: var(--space-3); }
.p-4 { padding: var(--space-4); }

.text-center { text-align: center; }
.text-right { text-align: right; }
.text-left { text-align: left; }

.w-full { width: 100%; }
.h-full { height: 100%; }
.w-auto { width: auto; }
.h-auto { height: auto; }
.min-w-0 { min-width: 0; }
.min-h-0 { min-height: 0; }

.overflow-hidden { overflow: hidden; }
.overflow-auto { overflow: auto; }
.overflow-y-auto { overflow-y: auto; }
.whitespace-nowrap { white-space: nowrap; }
.truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.font-heading { font-family: var(--font-heading); }
.font-semibold { font-weight: 600; }
.uppercase { text-transform: uppercase; }

.text-xs { font-size: 11px; }
.text-sm { font-size: 12px; }
.text-lg { font-size: 18px; }
.text-xl { font-size: 22px; }
.text-2xl { font-size: 29px; }

.text-accent-700 { color: var(--color-accent-700); }
.text-accent-800 { color: var(--color-accent-800); }
.bg-surface { background: var(--color-surface); }
.bg-neutral-200 { background: var(--color-neutral-200); }
.bg-accent { background: var(--color-accent); }
.border-divider { border: 1px solid var(--color-divider); }
```

---

### 3.10. `frontend/libs/shared/theming/src/lib/index.scss`

```scss
/* Master Theming Entry Point */
@import './tokens';
@import './typography';
@import './buttons';
@import './forms';
@import './cards';
@import './tags';
@import './table';
@import './dialog';
@import './utilities';
```

---

### 3.11. `frontend/libs/shared/theming/src/index.ts`

```ts
/**
 * TypeScript Design Token Constants and Definitions for Bill-Book
 */

export const TOKENS = {
  colors: {
    bg: '#f3f2f2',
    surface: '#eae9e9',
    text: '#201f1d',
    ink: '#2f353f',
    accent: '#f06311',
    accent2: '#ac803e',
    divider: 'color-mix(in srgb, #201f1d 16%, transparent)',
    neutral: {
      100: '#f8f4f4',
      200: '#eae7e7',
      300: '#d7d3d3',
      400: '#bab6b6',
      500: '#9b9797',
      600: '#7d7979',
      700: '#605d5d',
      800: '#444141',
      900: '#2d2b2b',
    },
    accentRamp: {
      100: '#fdefe4',
      200: '#ffe3bf',
      300: '#facb8d',
      400: '#f7853f',
      500: '#f06311',
      600: '#c94d08',
      700: '#a03d05',
      800: '#7a2f04',
      900: '#3a270d',
    },
    accent2Ramp: {
      100: '#fff3e4',
      200: '#ffe3be',
      300: '#f5cd96',
      400: '#dbaf70',
      500: '#bc8f4e',
      600: '#9b7232',
      700: '#79561f',
      800: '#573d14',
      900: '#382810',
    },
  },
  typography: {
    fontHeading: '"Cormorant Garamond", system-ui, sans-serif',
    fontHeadingWeight: 600,
    fontBody: '"Lora", system-ui, sans-serif',
    fontMono: 'Consolas, "Courier New", monospace',
  },
  spacing: {
    space1: '3px',
    space2: '7px',
    space3: '10px',
    space4: '13px',
    space6: '18px',
    space8: '24px',
  },
  radii: {
    sm: '2px',
    md: '4px',
    lg: '7px',
    pill: '999px',
  },
  zIndex: {
    topbar: 6,
    rail: 5,
    breadcrumbs: 4,
    tableHead: 3,
    content: 1,
  },
} as const;

export type DesignTokens = typeof TOKENS;
```

---

### 3.12. `frontend/libs/shared/theming/src/lib/tokens.spec.ts`

```ts
import { describe, it, expect } from 'vitest';
import { TOKENS } from '../index';

describe('Design Tokens Contract', () => {
  it('should define core palette with stroke-over-fill accent', () => {
    expect(TOKENS.colors.bg).toBe('#f3f2f2');
    expect(TOKENS.colors.surface).toBe('#eae9e9');
    expect(TOKENS.colors.accent).toBe('#f06311');
    expect(TOKENS.colors.ink).toBe('#2f353f');
  });

  it('should define 100-900 tonal ramps for neutral and accent', () => {
    expect(TOKENS.colors.neutral[100]).toBe('#f8f4f4');
    expect(TOKENS.colors.neutral[900]).toBe('#2d2b2b');
    expect(TOKENS.colors.accentRamp[100]).toBe('#fdefe4');
    expect(TOKENS.colors.accentRamp[500]).toBe('#f06311');
    expect(TOKENS.colors.accentRamp[700]).toBe('#a03d05');
  });

  it('should define compact spacing scale for ERP density', () => {
    expect(TOKENS.spacing.space1).toBe('3px');
    expect(TOKENS.spacing.space2).toBe('7px');
    expect(TOKENS.spacing.space3).toBe('10px');
    expect(TOKENS.spacing.space4).toBe('13px');
    expect(TOKENS.spacing.space6).toBe('18px');
    expect(TOKENS.spacing.space8).toBe('24px');
  });

  it('should maintain strict stacking context z-index hierarchy', () => {
    expect(TOKENS.zIndex.topbar).toBe(6);
    expect(TOKENS.zIndex.rail).toBe(5);
    expect(TOKENS.zIndex.breadcrumbs).toBe(4);
    expect(TOKENS.zIndex.tableHead).toBe(3);
    expect(TOKENS.zIndex.content).toBe(1);
    expect(TOKENS.zIndex.topbar).toBeGreaterThan(TOKENS.zIndex.tableHead);
  });
});
```

---

## 4. App Styles Integration Strategy

In `frontend/apps/web/src/styles.scss`:
Replace duplicate token definitions and base classes with:
```scss
@import '../../../libs/shared/theming/src/lib/index.scss';

/* App Shell Navigation & Action Bars */
.crumbs {
  flex: none;
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 4px var(--space-4);
  border-bottom: 1px solid var(--color-divider);
  font-size: 12px;

  a { color: color-mix(in srgb, var(--color-text) 60%, transparent); }
  a:hover { color: var(--color-accent-700); }
  .sep { opacity: 0.4; }
  [aria-current='page'] { font-family: var(--font-heading); font-weight: 600; font-size: 13px; color: var(--color-accent-800); }
  .acts { margin-left: auto; display: flex; align-items: center; gap: var(--space-2); }
  .btn { margin: 0; min-height: 26px; font-size: 12px; padding: 2px 9px; display: inline-flex; align-items: center; gap: 8px; }
}

.actbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-3);

  .btn { margin: 0; font-size: 12px; padding: 5px 9px; display: inline-flex; align-items: center; gap: 8px; }
  .push { margin-left: auto; }
}

/* Rail Navigation Item Overrides */
nav[aria-label='Modules']::-webkit-scrollbar { width: 0; height: 0; }
.rail-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px 0;
  border-radius: var(--radius-md);
  font-family: var(--font-heading);
  font-weight: 600;
  font-size: 10px;
  letter-spacing: 0.03em;
  color: rgba(243, 242, 242, 0.72);
  text-align: center;

  &:hover { color: var(--color-accent-400); }

  &[aria-current='page'],
  &.active {
    position: relative;
    z-index: 1;
    color: var(--color-accent-700);
    background: var(--color-bg);
    margin: 0 -4px 0 -4px;
    padding-right: 4px;
    padding-left: 4px;
    border-radius: 0;
    box-shadow: var(--shadow-rail-active);

    &:hover { background: var(--color-bg); color: var(--color-accent-800); }
    svg { stroke-width: 2.4; color: var(--color-accent); }
  }
}
```

In `frontend/apps/desktop/src/styles.scss`:
```scss
/* Desktop styles */
@import '../../web/src/styles.scss';
```

---

## 5. Verification Protocol for Worker

1. **Lint Verification**:
   ```powershell
   cd frontend
   npx nx lint theming
   ```
2. **Type Check**:
   ```powershell
   npm run typecheck
   ```
3. **Unit Tests**:
   ```powershell
   npm run test
   ```
   Must pass `tokens.spec.ts` along with existing 186 unit tests.
4. **App Builds**:
   ```powershell
   npx nx build web
   npx nx build desktop
   ```
   Both builds must complete with zero errors.
5. **Full Pipeline Check**:
   ```powershell
   npm run check
   ```
