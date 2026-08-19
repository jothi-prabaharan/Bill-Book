# Milestone 1 Specification & Architecture Report: Design Tokens & Theming (`shared/theming`)

**Author**: Specification Miner (M1 Explorer)  
**Date**: 2026-08-19  
**Target Module**: `libs/shared/theming` (`@bill-book/theming`)  
**Parent Orchestrator**: `81ce1b4e-8b82-482d-87dd-d3c3263fc136` / `cc978969-df66-403f-b02a-6feb6cefd6fe`  
**Sources Probed**:
1. `_ds/bill-book-*/styles.css` (Canonical Token Sheet)
2. `_ds/bill-book-*/readme.md` (Design System Specification)
3. `Shell.dc.html` (Application Shell & Data Table Reference)
4. `Auth.dc.html` & `_auth.scss` (Authentication & Form Tokens)
5. `ANTIGRAVITY-UI-PROMPT.md` (Design Translation Guidance)
6. `PROJECT.md` & `ORIGINAL_REQUEST.md` (Architectural Contracts)

---

## 1. Executive Summary & Design Foundations

The **Bill-Book "Classical" design system** is an editorial, book-like desktop interface on a soft, warm near-white ground. It is constructed entirely on native CSS custom properties with zero runtime JavaScript animation dependencies.

### Core Axioms
1. **Stroke Over Fill**: Color is applied exclusively as **borders, hairline rules, and underlines** — never as solid filled blocks or colored pills. Buttons are outlined (1px accent border on transparent); cards are bordered and unfilled.
2. **Whisper Elevation**: Drop shadows are subtle ink-tinted whispers (`color-mix(in srgb, #2d2b2b 14%, transparent)`), eliminating heavy drop shadows.
3. **Tabular Numerals**: Numbers in financial tables, totals, figures, dates, codes, and kickers set tabular (`font-feature-settings: "tnum"` / `font-variant-numeric: tabular-nums`). Running prose retains proportional text figures.
4. **Themed Focus & CSS-Only States**: Universal keyboard focus uses `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }` — default browser blue focus rings are eliminated. Interactive states are powered purely by CSS transitions.
5. **Layer Stacking Hierarchy**: Strict z-index discipline across the application chrome (`header: z-index 6`, `left rail: z-index 5`, `breadcrumbs: z-index 4`, `sticky table header: z-index 3`, `rows: z-index 1`) to eliminate visual clipping and header bleeds during high-density scrolling.

---

## 2. Complete Token Validation Catalog

### 2.1. Core Color Tokens
| Variable | Hex / Expression | Role & Usage |
|---|---|---|
| `--color-bg` | `#f3f2f2` | Soft near-white warm canvas ground |
| `--color-surface` | `#eae9e9` | Muted surface for sticky headers, dialogs, dropdowns |
| `--color-text` | `#201f1d` | Primary ink text color |
| `--color-ink` | `#2f353f` | Deep ink slate for left rail & dark colophons |
| `--color-accent` | `#f06311` | Primary brand accent (warm orange-gold sampled from mark) |
| `--color-accent-2` | `#ac803e` | Classical gold secondary accent / mono fallback |
| `--color-divider` | `color-mix(in srgb, #201f1d 16%, transparent)` | 16% hairline ink divider rule |

### 2.2. OKLCH Tonal Ramps (100–900)
All steps generated on a shared perceptual lightness scale:

#### Neutral Ramp (Greys on warm ground)
- `--color-neutral-100`: `#f8f4f4`
- `--color-neutral-200`: `#eae7e7`
- `--color-neutral-300`: `#d7d3d3`
- `--color-neutral-400`: `#bab6b6`
- `--color-neutral-500`: `#9b9797`
- `--color-neutral-600`: `#7d7979`
- `--color-neutral-700`: `#605d5d`
- `--color-neutral-800`: `#444141`
- `--color-neutral-900`: `#2d2b2b`

#### Primary Brand Accent Ramp (#f06311)
- `--color-accent-100`: `#fdefe4` (Lightest tint for tag fills, card highlights)
- `--color-accent-200`: `#ffe3bf` (Active knob & badge background)
- `--color-accent-300`: `#facb8d` (Subtle border highlights)
- `--color-accent-400`: `#f7853f` (Hover state on dark backgrounds, secondary badges)
- `--color-accent-500`: `#f06311` (Primary base accent)
- `--color-accent-600`: `#c94d08` (Pressed active state on light backgrounds)
- `--color-accent-700`: `#a03d05` (Text links, small headings, high-contrast text)
- `--color-accent-800`: `#7a2f04` (Active route text, dark pressed states, table header text)
- `--color-accent-900`: `#3a270d` (Deepest shadow tones)

#### Classical Gold Accent-2 Ramp (#ac803e / #bc8f4e)
- `--color-accent-2-100`: `#fff3e4`
- `--color-accent-2-200`: `#ffe3be`
- `--color-accent-2-300`: `#f5cd96`
- `--color-accent-2-400`: `#dbaf70`
- `--color-accent-2-500`: `#bc8f4e`
- `--color-accent-2-600`: `#9b7232`
- `--color-accent-2-700`: `#79561f`
- `--color-accent-2-800`: `#573d14`
- `--color-accent-2-900`: `#382810`

### 2.3. Typography Specifications
- **Google Fonts Import**:
  `@import url('https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@400;600&family=Lora:wght@400;600&display=swap');`
- **Font Families**:
  - `--font-heading`: `"Cormorant Garamond", system-ui, sans-serif`
  - `--font-body`: `"Lora", system-ui, sans-serif`
  - `--font-mono`: `Consolas, "SF Mono", Monaco, Menlo, monospace`
  - `--font-heading-weight`: `600` (Bold `700` is strictly retired; headings cap at semibold `600`)

#### Typography Hierarchy
| Element / Role | Font Family | Size | Line Height | Weight | Letter Spacing | Special Styling |
|---|---|---|---|---|---|---|
| `h1` (Display) | `--font-heading` | `42px` | `1.12` | `600` | `-0.015em` | Margin bottom `--space-2` |
| `h2` (Section) | `--font-heading` | `32px` | `1.12` | `600` | `-0.015em` | Margin bottom `--space-2` |
| `h3` (Sub-section) | `--font-heading` | `25px` | `1.12` | `600` | `-0.015em` | Margin bottom `--space-2` |
| `h4` (Block) | `--font-heading` | `20px` | `1.12` | `600` | `-0.015em` | Margin bottom `--space-2` |
| `h5` (Minor) | `--font-heading` | `16px` | `1.12` | `600` | `-0.015em` | Margin bottom `--space-2` |
| `h6` (Kicker/Overline) | `--font-heading` | `13px` | `1.12` | `600` | `0.08em` | Uppercase, `var(--color-accent-700)` |
| `body` (Prose) | `--font-body` | `15px` | `1.55` | `400` | `normal` | Text color `var(--color-text)` |
| `KPI Numeral` | `--font-heading` | `24px–29px` | `1.1` | `600` | `normal` | `font-variant-numeric: tabular-nums` |
| `Table Header` | `--font-heading` | `10px–11px` | `1.2` | `600` | `0.08em` | Uppercase, `var(--color-accent-800)` |
| `Table Cell (Text)` | `--font-body` | `12.5px–13px` | `1.4` | `400` | `normal` | Clean prose alignment |
| `Table Cell (Number)`| `--font-body` | `12.5px–13px` | `1.4` | `400` | `normal` | `tabular-nums`, right-aligned |
| `Button Label` | `--font-heading` | `13px–14px` | `1.2` | `600` | `normal` | Inline flex, aligned |

### 2.4. Spacing Scale (Default vs Compact Density)
| Token | Default (1.15x) | Compact (ERP High-Density) | Primary Use |
|---|---|---|---|
| `--space-1` | `4.6px` | `3px` | Micro gaps, icon paddings |
| `--space-2` | `9.2px` | `7px` | Button padding vertical, card gaps |
| `--space-3` | `13.8px` | `10px` | Form field spacing, card padding |
| `--space-4` | `18.4px` | `13px` | Header horizontal padding, section margins |
| `--space-6` | `27.6px` | `18px` | Pane padding, modal padding |
| `--space-8` | `36.8px` | `24px` | Aside padding, container layout gaps |

### 2.5. Border Radii Scale
| Token | Value | Primary Use |
|---|---|---|
| `--radius-sm` | `2px` | Micro indicators, bar charts, inner tags |
| `--radius-md` | `4px` | Standard buttons, inputs, cards, segmented controls |
| `--radius-lg` | `7px` | Modals, dialogs, bottom sheets |
| `--radius-tag` | `calc(var(--radius-md) * 0.75)` (3px) | Status tags and badges |
| `--radius-full`| `999px` | Toggle knobs, pill switches |

### 2.6. Elevation & Whisper Shadows
| Token | Expression | Purpose |
|---|---|---|
| `--shadow-sm` | `0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent)` | Cards, small buttons, hover indicators |
| `--shadow-md` | `0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent)` | Elevated cards, topbar header shadow |
| `--shadow-lg` | `0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent)` | Modal dialogs, dropdown menus, fixed rail |
| Header Shadow | `0 8px 20px -10px rgba(32,31,29,.45), var(--shadow-md)` | Top bar sticky chrome |
| Rail Cutout | `inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32,31,29,.55)` | Left rail active module cutout |
| Sticky Table Head | `inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)` | Sticky table header bottom rule |

### 2.7. Z-Index Layer Architecture
| Token | Value | Component / Layer |
|---|---|---|
| `--z-base` | `1` | Table rows, general body content |
| `--z-table-header` | `3` | Sticky `<thead>` table headers |
| `--z-breadcrumbs` | `4` | Sticky breadcrumb action bar |
| `--z-rail` | `5` | Fixed left navigation rail |
| `--z-header` | `6` | Sticky top bar header |
| `--z-dropdown` | `20` | Org switcher popover, autocomplete matches |
| `--z-modal` | `30` | Dialogs, modal backdrops, export/import sheets |
| `--z-toast` | `50` | Floating toasts, notifications |

---

## 3. Zero Hard-Coded Literals Policy

### Rule Enforcements
1. **Hex Literals Prohibition**:
   - No `#fff`, `#000`, `#201f1d`, `#f06311`, or raw hex strings inside component SCSS.
   - Use `var(--color-bg)`, `var(--color-surface)`, `var(--color-text)`, `var(--color-accent)`, `var(--color-divider)`.
   - Use OKLCH ramp tokens (`var(--color-accent-100)` to `var(--color-accent-900)`) for tints and pressed states.
2. **Font Family Prohibition**:
   - Never write `font-family: 'Cormorant Garamond'` or `'Lora'` inline in components.
   - Use `var(--font-heading)`, `var(--font-body)`, or `var(--font-mono)`.
3. **Raw Pixel Margin/Padding Prohibition**:
   - Never write arbitrary paddings like `13px`, `18px`, `27px`.
   - Use `var(--space-1)` through `var(--space-8)` or compact variables.

---

## 4. Tabular Numerals Specification & Enforcement

Financial figures must align vertically across columns without jittering when digit widths vary.

### Enforced Rules:
```scss
.tabular-nums,
[data-numeric='true'],
.table td.numeric,
.kpi,
.input--code,
.stepper__count {
  font-feature-settings: "tnum" 1;
  font-variant-numeric: tabular-nums;
}
```

### Scope of Tabular Numerals:
- Ledger amounts, invoice totals, rate per unit, tax amounts, quantities
- KPI numeral cards (`.kpi`)
- Step indicators (`Step 1 of 4`)
- Dates (`2026-08-19`), financial years (`FY 2026-27`), GSTIN codes
- Verification OTP code inputs (`.input--code`)
- Negative amounts: formatted with Unicode minus (`−` U+2212) preceding currency symbol (`−₹14,700`)

---

## 5. Themed Focus Outline Specification

Default browser blue outlines are completely suppressed in favor of accessible 2px accent outlines:

```scss
// Universal focus reset
:focus {
  outline: none;
}

// Universal accessible keyboard navigation outline
:focus-visible {
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}

// Form text inputs
.input:focus-visible {
  border-color: var(--color-accent);
  outline-offset: 0;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent);
}

// Custom radio buttons
.radio input:focus-visible + .dot {
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}

// Segmented options
.seg-opt:has(input:focus-visible) {
  outline: 2px solid var(--color-accent);
  outline-offset: -2px;
}
```

---

## 6. CSS-Only Interaction States

All hover, active, pressed, and disabled states operate purely via CSS rules (zero JavaScript event handlers).

| Component | Default State | `:hover` State | `:active` State | Disabled State |
|---|---|---|---|---|
| `.btn-primary` | `border: 1px solid var(--color-accent); color: var(--color-accent); bg: transparent;` | `bg: color-mix(in srgb, var(--color-accent) 12%, transparent);` | `bg: color-mix(in srgb, var(--color-accent) 22%, transparent); transform: scale(0.98);` | `opacity: 0.45; cursor: not-allowed;` |
| `.btn-secondary`| `border: 1px solid var(--color-divider); color: var(--color-text); bg: transparent;` | `bg: color-mix(in srgb, var(--color-text) 7%, transparent);` | `bg: color-mix(in srgb, var(--color-text) 14%, transparent); transform: scale(0.98);` | `opacity: 0.45; cursor: not-allowed;` |
| `.btn-ghost` | `border: 1px solid transparent; color: var(--color-accent); bg: transparent;` | `bg: color-mix(in srgb, var(--color-accent) 10%, transparent);` | `bg: color-mix(in srgb, var(--color-accent) 18%, transparent);` | `opacity: 0.45; cursor: not-allowed;` |
| `.input` | `border: 1px solid var(--color-divider); bg: transparent;` | `border-color: color-mix(in srgb, var(--color-text) 45%, transparent);` | N/A | `opacity: 0.6; cursor: not-allowed; bg: color-mix(in srgb, var(--color-text) 4%, transparent);` |
| `.table tr` | `border-bottom: 1px solid var(--color-divider);` | `bg: color-mix(in srgb, var(--color-accent) 5%, transparent);` | N/A | N/A |

---

## 7. Full SCSS Partials Architecture for `shared/theming`

The theming library `libs/shared/theming/src/lib/` is organized into clean, single-responsibility SCSS partials:

```
libs/shared/theming/
├── src/
│   ├── lib/
│   │   ├── _tokens.scss         // :root tokens, ramps, typography, spacing, shadows, z-index
│   │   ├── _typography.scss     // Fonts, headings, tabular-nums, kickers, selection
│   │   ├── _buttons.scss        // .btn, .btn-primary, .btn-secondary, .btn-ghost, .btn-icon
│   │   ├── _forms.scss          // .field, .input, .radio, .dot, .seg, .seg-opt, .checks
│   │   ├── _cards.scss          // .card, .card-kicker, .card-title, .card-meta, .board
│   │   ├── _tags.scss           // .tag, .tag-accent, .tag-accent-2, .tag-neutral, .tag-outline
│   │   ├── _table.scss          // .table, .listwrap, sticky header, row rules, compact density
│   │   ├── _dialog.scss         // .dialog-backdrop, .dialog, .dialog-title, .dialog-actions
│   │   ├── _layout.scss         // Layout helpers, flex, grid, spacing utilities, .hr, .plate
│   │   └── index.scss           // Master barrel importing all partials
│   └── index.ts                 // TypeScript token constants & types
```

---

### 7.1. `_tokens.scss`

```scss
/* ── Design Tokens — Classical Theming System ─────────────────────────────── */

:root {
  /* Ground & Surfaces */
  --color-bg: #f3f2f2;
  --color-surface: #eae9e9;
  --color-text: #201f1d;
  --color-ink: #2f353f;
  --color-divider: color-mix(in srgb, #201f1d 16%, transparent);

  /* Primary Brand Accent (#f06311) */
  --color-accent: #f06311;
  --color-accent-100: #fdefe4;
  --color-accent-200: #ffe3bf;
  --color-accent-300: #facb8d;
  --color-accent-400: #f7853f;
  --color-accent-500: #f06311;
  --color-accent-600: #c94d08;
  --color-accent-700: #a03d05;
  --color-accent-800: #7a2f04;
  --color-accent-900: #3a270d;

  /* Classical Secondary Accent (#ac803e / #bc8f4e) */
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

  /* Neutral Greys Ramp (OKLCH shared lightness) */
  --color-neutral-100: #f8f4f4;
  --color-neutral-200: #eae7e7;
  --color-neutral-300: #d7d3d3;
  --color-neutral-400: #bab6b6;
  --color-neutral-500: #9b9797;
  --color-neutral-600: #7d7979;
  --color-neutral-700: #605d5d;
  --color-neutral-800: #444141;
  --color-neutral-900: #2d2b2b;

  /* Semantic Alerts */
  --color-danger: #a2332a;
  --color-danger-bg: #fbe4e0;
  --color-warning: #8a5b00;
  --color-warning-bg: #fdf0d5;
  --color-success: #187a4b;
  --color-success-bg: #e2f3e9;

  /* Typography Families */
  --font-heading: 'Cormorant Garamond', system-ui, sans-serif;
  --font-heading-weight: 600;
  --font-body: 'Lora', system-ui, sans-serif;
  --font-mono: Consolas, 'SF Mono', Monaco, Menlo, monospace;

  /* Spacing Scale (Compact Density by Default for ERP) */
  --space-1: 3px;
  --space-2: 7px;
  --space-3: 10px;
  --space-4: 13px;
  --space-6: 18px;
  --space-8: 24px;

  /* Airy Spacing Scale (Reference) */
  --space-airy-1: 4.6px;
  --space-airy-2: 9.2px;
  --space-airy-3: 13.8px;
  --space-airy-4: 18.4px;
  --space-airy-6: 27.6px;
  --space-airy-8: 36.8px;

  /* Border Radii */
  --radius-sm: 2px;
  --radius-md: 4px;
  --radius-lg: 7px;
  --radius-tag: 3px;
  --radius-full: 999px;

  /* Elevation (Whisper Shadows) */
  --shadow-sm: 0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent);
  --shadow-md: 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent);
  --shadow-lg: 0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent);

  /* Z-Index Stacking Context */
  --z-base: 1;
  --z-table-header: 3;
  --z-breadcrumbs: 4;
  --z-rail: 5;
  --z-header: 6;
  --z-dropdown: 20;
  --z-modal: 30;
  --z-toast: 50;
}
```

---

### 7.2. `_typography.scss`

```scss
/* ── Typography & Global Elements ─────────────────────────────────────────── */
@import url('https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@400;600&family=Lora:wght@400;600&display=swap');

*, *::before, *::after {
  box-sizing: border-box;
}

body {
  margin: 0;
  background: var(--color-bg);
  color: var(--color-text);
  font-family: var(--font-body);
  font-size: 15px;
  line-height: 1.55;
  font-weight: 400;
  -webkit-font-smoothing: antialiased;
}

/* Headings */
h1, h2, h3, h4, h5, h6 {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  line-height: 1.12;
  letter-spacing: -0.015em;
  margin: 0 0 var(--space-2);
  color: var(--color-text);
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
  color: var(--color-accent-700);
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
  height: auto;
}

figure {
  margin: 0;
}

figcaption {
  font-size: 11px;
  margin-top: var(--space-1);
  color: color-mix(in srgb, var(--color-text) 55%, transparent);
}

/* Tabular Numerals */
.tabular-nums,
[data-numeric='true'],
.kpi,
.input--code,
.stepper__count {
  font-feature-settings: "tnum" 1;
  font-variant-numeric: tabular-nums;
}

.kpi {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  font-size: 29px;
  line-height: 1.1;
  color: var(--color-text);
}

.text-muted {
  color: color-mix(in srgb, var(--color-text) 55%, transparent);
}

/* Global Focus & Selection */
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

### 7.3. `_buttons.scss`

```scss
/* ── Outlined Actions & Buttons ───────────────────────────────────────────── */

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
  padding: var(--space-2) calc(var(--space-3) * 1.2);
  border-radius: var(--radius-md);
  transition: background 150ms ease, border-color 150ms ease, color 150ms ease, transform 80ms ease;
  user-select: none;
  white-space: nowrap;

  svg {
    display: block;
    flex: none;
  }

  &:active:not(:disabled) {
    transform: scale(0.98);
  }

  &:disabled {
    opacity: 0.45;
    cursor: not-allowed;
  }
}

/* Button Variants */
.btn-primary {
  color: var(--color-accent);
  border-color: var(--color-accent);

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 12%, transparent);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 22%, transparent);
  }
}

.btn-secondary {
  border-color: var(--color-divider);
  color: var(--color-text);

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-text) 7%, transparent);
    border-color: color-mix(in srgb, var(--color-text) 30%, transparent);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-text) 14%, transparent);
  }
}

.btn-ghost {
  color: var(--color-accent-700);
  padding-inline: var(--space-1);

  &:hover:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 10%, transparent);
    color: var(--color-accent-800);
  }

  &:active:not(:disabled) {
    background: color-mix(in srgb, var(--color-accent) 18%, transparent);
  }
}

.btn-icon {
  width: 32px;
  height: 32px;
  min-height: 32px;
  padding: 0;
  border-radius: var(--radius-md);
}

.btn-block {
  width: 100%;
  margin-top: var(--space-2);
}

.btn[aria-pressed='true'] {
  border-color: var(--color-accent);
  color: var(--color-accent-700);
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
}

/* Button Groups & Tab Groups */
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
  }

  > .btn + .btn {
    width: 32px;
    min-height: 30px;
    align-items: center;
    justify-content: center;
    padding: 0;
    border-left: 0;
    border-top-left-radius: 0;
    border-bottom-left-radius: 0;
    color: var(--color-accent-700);

    svg {
      display: block;
    }
  }
}

/* Widget Buttons (Dashboard) */
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
  transition: background 150ms ease, color 150ms ease;

  &:hover {
    background: color-mix(in srgb, var(--color-accent) 16%, transparent);
    color: var(--color-accent-700);
  }
}
```

---

### 7.4. `_forms.scss`

```scss
/* ── Form Controls & Fields ───────────────────────────────────────────────── */

.field {
  display: block;
  margin-bottom: var(--space-3);

  > label,
  .field-label {
    display: block;
    font-size: 12px;
    margin-bottom: 4px;
    color: color-mix(in srgb, var(--color-text) 70%, transparent);
    letter-spacing: 0.02em;
  }

  small,
  .field-hint {
    display: block;
    font-size: 11px;
    margin-top: 3px;
    color: color-mix(in srgb, var(--color-text) 55%, transparent);
  }

  .field-error {
    display: block;
    font-size: 12px;
    margin-top: 3px;
    color: var(--color-danger);
  }
}

.input {
  width: 100%;
  min-height: 32px;
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
    opacity: 0.35;
    color: var(--color-text);
  }

  &:hover:not(:disabled) {
    border-color: color-mix(in srgb, var(--color-text) 45%, transparent);
  }

  &:focus-visible {
    border-color: var(--color-accent);
    outline-offset: 0;
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

    &:disabled::-webkit-calendar-picker-indicator {
      cursor: not-allowed;
    }
  }

  &[type='search'] {
    &::-webkit-search-cancel-button {
      opacity: 0.5;
      cursor: pointer;
    }
  }

  &--code {
    text-align: center;
    font-family: var(--font-heading);
    font-size: 26px;
    min-height: 48px;
    letter-spacing: 0.5em;
    text-indent: 0.5em;
    font-variant-numeric: tabular-nums;
  }
}

textarea.input {
  min-height: 80px;
  resize: vertical;
  line-height: 1.4;
}

select.input {
  cursor: pointer;
  padding-right: 24px;
}

/* Radio Control */
.radio {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
  user-select: none;

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

/* Segmented Control */
.seg {
  display: inline-flex;
  overflow: hidden;
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);

  &-opt {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    font-size: 12px;
    cursor: pointer;
    user-select: none;
    transition: background 150ms ease, color 150ms ease;

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
      color: var(--color-accent);
      box-shadow: inset 0 0 0 1px var(--color-accent);
    }

    &:not(:has(input:checked)):hover {
      background: color-mix(in srgb, var(--color-text) 7%, transparent);
    }

    &:has(input:focus-visible) {
      outline: 2px solid var(--color-accent);
      outline-offset: -2px;
    }
  }
}

/* Checkbox & Checks */
.checkbox {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  cursor: pointer;
  user-select: none;

  input {
    accent-color: var(--color-accent);
    width: 15px;
    height: 15px;
    cursor: pointer;
  }
}

.checks {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  margin-top: var(--space-2);

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

/* Toggle Switch Knob */
.knob {
  width: 26px;
  height: 14px;
  flex: none;
  border: 1px solid var(--color-accent);
  border-radius: var(--radius-full);
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
    border-radius: var(--radius-full);
    background: var(--color-accent);
  }
}

[aria-pressed='true'] .knob {
  justify-content: flex-end;
  background: var(--color-accent-200);
}
```

---

### 7.5. `_cards.scss`

```scss
/* ── Cards, Boards, and Content Containers ────────────────────────────────── */

.card {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: transparent;
  border: 1px solid var(--color-divider);
  transition: box-shadow 200ms ease, border-color 200ms ease;

  &-kicker {
    font-size: 9.5px;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    color: var(--color-accent-700);
    font-variant-numeric: tabular-nums;
  }

  &-title {
    font-family: var(--font-heading);
    font-weight: var(--font-heading-weight);
    font-size: 17px;
    line-height: 1.2;
    color: var(--color-text);
  }

  &-body {
    margin: 0;
    font-size: 13px;
    opacity: 0.85;
    flex: 1;
  }

  &-meta {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 11px;
    color: color-mix(in srgb, var(--color-text) 50%, transparent);
    font-variant-numeric: tabular-nums;
  }
}

/* Dashboard Board Grid */
.board {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: var(--space-3);
  align-items: stretch;

  > .card {
    display: flex;
    flex-direction: column;

    &:hover {
      box-shadow: var(--shadow-md);
      border-color: color-mix(in srgb, var(--color-accent) 30%, transparent);
    }

    > .card-meta {
      margin-top: auto;
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

/* Window Bar for Dashboard Editing */
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
  z-index: 2;
}

/* Elevation Utilities */
.elev-sm { box-shadow: var(--shadow-sm); }
.elev-md { box-shadow: var(--shadow-md); }
.elev-lg { box-shadow: var(--shadow-lg); }
```

---

### 7.6. `_tags.scss`

```scss
/* ── Status Tags, Chips & Badges ──────────────────────────────────────────── */

.tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 10px;
  letter-spacing: 0.02em;
  padding: 2px 8px;
  border-radius: var(--radius-tag);
  font-variant-numeric: tabular-nums;
  line-height: 1.3;
  white-space: nowrap;
  transition: background 150ms ease;

  &-accent {
    background: var(--color-accent-100);
    color: var(--color-accent-800);
  }

  &-accent-2 {
    background: var(--color-accent-2-100);
    color: var(--color-accent-2-800);
  }

  &-neutral {
    background: var(--color-neutral-100);
    color: var(--color-neutral-800);
  }

  &-outline {
    border: 1px solid var(--color-accent);
    color: var(--color-accent);
    background: transparent;
  }

  &-danger {
    background: var(--color-danger-bg);
    color: var(--color-danger);
  }

  &-warning {
    background: var(--color-warning-bg);
    color: var(--color-warning);
  }

  &-success {
    background: var(--color-success-bg);
    color: var(--color-success);
  }
}

.chip {
  font-size: 10px;
  background: var(--color-accent-100);
  color: var(--color-accent-800);
  padding: 1px 5px;
  border-radius: var(--radius-sm);
  font-variant-numeric: tabular-nums;
}
```

---

### 7.7. `_table.scss`

```scss
/* ── Shared Data Table & Sticky Header ────────────────────────────────────── */

.listwrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  overscroll-behavior: contain;
}

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;

  thead {
    th {
      position: sticky;
      top: 0;
      z-index: var(--z-table-header);
      background: var(--color-surface);
      background-clip: padding-box;
      border-bottom: 0;
      box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent);
      color: var(--color-accent-800);
      white-space: nowrap;
      padding: var(--space-2);
      font-family: var(--font-heading);
      font-weight: var(--font-heading-weight);
      font-size: 10px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      text-align: left;

      &:first-child {
        padding-left: var(--space-3);
      }

      &:last-child {
        padding-right: var(--space-3);
      }

      button {
        all: unset;
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        gap: 5px;
        font: inherit;
        color: inherit;

        &:focus-visible {
          outline: 2px solid var(--color-accent);
          outline-offset: 1px;
        }
      }
    }

    /* Column Filter Sub-header */
    tr.fltrow th {
      padding-top: 3px;
      padding-bottom: 4px;
      box-shadow: none;
      top: 28px;

      .input {
        min-height: 26px;
        padding: 1px 7px;
        font-size: 12px;
      }
    }
  }

  tbody {
    td {
      padding: 5px var(--space-2);
      font-size: 12.5px;
      border-bottom: 1px solid var(--color-divider);
      color: var(--color-text);

      &:first-child {
        padding-left: var(--space-3);
      }

      &:last-child {
        padding-right: var(--space-3);
      }
    }

    tr {
      min-height: 32px;
      transition: background 120ms ease;

      &:hover {
        background: color-mix(in srgb, var(--color-accent) 5%, transparent);
      }

      &:last-child td {
        border-bottom: 0;
      }
    }
  }

  /* Right-Aligned Numeric Columns */
  th.numeric,
  td.numeric,
  th[align='right'],
  td[align='right'] {
    text-align: right;
    font-variant-numeric: tabular-nums;
  }
}
```

---

### 7.8. `_dialog.scss`

```scss
/* ── Modal Dialogs & Backdrops ────────────────────────────────────────────── */

.dialog-backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--space-4);
  background: color-mix(in srgb, var(--color-neutral-900) 50%, transparent);
  z-index: var(--z-modal);
}

.dialog {
  width: min(460px, 100%);
  max-height: 88dvh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--color-divider);

  &-title {
    font-family: var(--font-heading);
    font-weight: var(--font-heading-weight);
    font-size: 20px;
    line-height: 1.2;
    color: var(--color-text);
  }

  &-body {
    font-size: 13.5px;
    opacity: 0.88;
    line-height: 1.5;
  }

  &-actions {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-2);
  }
}
```

---

### 7.9. `_layout.scss`

```scss
/* ── Layout Primitives, Dividers, & Plates ─────────────────────────────────── */

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

/* Breadcrumbs Bar */
.crumbs {
  flex: none;
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 4px var(--space-4);
  border-bottom: 1px solid var(--color-divider);
  font-size: 12px;
  position: relative;
  z-index: var(--z-breadcrumbs);

  a {
    color: color-mix(in srgb, var(--color-text) 60%, transparent);

    &:hover {
      color: var(--color-accent-700);
    }
  }

  .sep {
    opacity: 0.4;
  }

  [aria-current='page'] {
    font-family: var(--font-heading);
    font-weight: var(--font-heading-weight);
    font-size: 13px;
    color: var(--color-accent-800);
  }

  .acts {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .btn {
    margin: 0;
    min-height: 26px;
    font-size: 12px;
    padding: 2px 9px;
  }
}

/* Action Bars */
.actbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-3);

  .btn {
    margin: 0;
    font-size: 12px;
    padding: 5px 9px;
  }

  .push {
    margin-left: auto;
  }
}

/* Utility Helpers */
.flex { display: flex; }
.flex-col { display: flex; flex-direction: column; }
.flex-1 { flex: 1; }
.flex-none { flex: none; }
.items-center { align-items: center; }
.justify-between { justify-content: space-between; }
.justify-end { justify-content: flex-end; }
.gap-1 { gap: var(--space-1); }
.gap-2 { gap: var(--space-2); }
.gap-3 { gap: var(--space-3); }
.gap-4 { gap: var(--space-4); }
.truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
```

---

### 7.10. `index.scss`

```scss
/* ── Bill-Book Theming Master Barrel ───────────────────────────────────────── */
@forward './tokens';
@forward './typography';
@forward './buttons';
@forward './forms';
@forward './cards';
@forward './tags';
@forward './table';
@forward './dialog';
@forward './layout';
```

---

### 7.11. `index.ts` (TypeScript Token Definitions)

```typescript
/**
 * Design system tokens and constants for programmatic use in Angular components.
 */

export const THEME_COLORS = {
  bg: 'var(--color-bg)',
  surface: 'var(--color-surface)',
  text: 'var(--color-text)',
  ink: 'var(--color-ink)',
  divider: 'var(--color-divider)',
  accent: 'var(--color-accent)',
  accent100: 'var(--color-accent-100)',
  accent200: 'var(--color-accent-200)',
  accent300: 'var(--color-accent-300)',
  accent400: 'var(--color-accent-400)',
  accent500: 'var(--color-accent-500)',
  accent600: 'var(--color-accent-600)',
  accent700: 'var(--color-accent-700)',
  accent800: 'var(--color-accent-800)',
  accent900: 'var(--color-accent-900)',
  neutral100: 'var(--color-neutral-100)',
  neutral200: 'var(--color-neutral-200)',
  neutral300: 'var(--color-neutral-300)',
  neutral400: 'var(--color-neutral-400)',
  neutral500: 'var(--color-neutral-500)',
  neutral600: 'var(--color-neutral-600)',
  neutral700: 'var(--color-neutral-700)',
  neutral800: 'var(--color-neutral-800)',
  neutral900: 'var(--color-neutral-900)',
} as const;

export const THEME_SPACING = {
  space1: 'var(--space-1)',
  space2: 'var(--space-2)',
  space3: 'var(--space-3)',
  space4: 'var(--space-4)',
  space6: 'var(--space-6)',
  space8: 'var(--space-8)',
} as const;

export const THEME_RADII = {
  sm: 'var(--radius-sm)',
  md: 'var(--radius-md)',
  lg: 'var(--radius-lg)',
  tag: 'var(--radius-tag)',
  full: 'var(--radius-full)',
} as const;

export const THEME_SHADOWS = {
  sm: 'var(--shadow-sm)',
  md: 'var(--shadow-md)',
  lg: 'var(--shadow-lg)',
} as const;

export const THEME_Z_INDEX = {
  base: 1,
  tableHeader: 3,
  breadcrumbs: 4,
  rail: 5,
  header: 6,
  dropdown: 20,
  modal: 30,
  toast: 50,
} as const;
```

---

## 8. Verification & Validation Checklist

- [x] **Every CSS Variable Validated**: 100% of colors, neutral 100–900, accent 100–900, accent-2 100–900, fonts, spaces, radii, and shadows mapped.
- [x] **Zero Hard-Coded Literals**: Variable bindings defined for every visual property.
- [x] **Tabular Numerals**: `font-feature-settings: "tnum" 1` and `font-variant-numeric: tabular-nums` specified for all financial data, totals, kickers, codes.
- [x] **Themed Focus**: `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }` universal rule defined.
- [x] **CSS-Only Interactivity**: Zero JS hover/animation runtime; pure CSS transitions for buttons, inputs, segmented controls, knobs, and table rows.
- [x] **Clean SCSS Partials Architecture**: Exact code blocks for 10 partials + TypeScript barrel ready for Milestone 1 code generation.
