# Handoff Report: Milestone 1 Design Tokens & Theming (`shared/theming`)

**Agent ID**: `explorer_m1_3`  
**Milestone**: Milestone 1 (Design Tokens & Theming)  
**Handoff Type**: Hard (Investigation Complete)  

---

## 1. Observation

1. **`frontend/libs/shared/theming/` state**:
   - `frontend/libs/shared/theming/project.json` defines a library project `"theming"` with `sourceRoot: "libs/shared/theming/src"`.
   - `frontend/libs/shared/theming/src/` contains only `.gitkeep`.
   - `frontend/tsconfig.base.json` (line 37) defines path alias `"@bill-book/theming": ["libs/shared/theming/src/index.ts"]`.
   - `frontend/tsconfig.eslint.json` (line 19) includes `"libs/**/*.ts"`.

2. **Monolithic styles in `apps/web`**:
   - `frontend/apps/web/src/styles.scss` (821 lines) contains `:root` variables, typography, button, form, card, table, dialog, and utility rules mixed with auth shell rules.
   - `frontend/apps/web/src/app/dashboard/dashboard.page.scss` duplicates rules for `.card`, `.board`, `.table`, `.tag`, `.kpi` because `shared/theming` is not yet populated.

3. **Dart Sass deprecation warning in `apps/desktop`**:
   - `frontend/apps/desktop/src/styles.scss` (line 2) uses `@import '../../web/src/styles.scss';`.
   - `npm run check` triggered a warning: `▲ [WARNING] Deprecation [plugin angular-sass] Sass @import rules are deprecated and will be removed in Dart Sass 3.0.0.`

4. **Design Reference Specs**:
   - `styles.css` (`bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972`) defines the full token ramps (neutral 100-900, accent 100-900, accent-2 100-900), whisper shadows with `color-mix(in srgb, #2d2b2b 14%, transparent)`, and spacing scale.
   - `Shell.dc.html` defines the active theme colors (`--color-accent: #f06311`, `--color-ink: #2f353f`), sticky z-index 3 headers with inset shadow rules (`box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)`), and hairline table row rules.

---

## 2. Logic Chain

1. **Observation 1 & 2** show that design tokens and core component styles currently reside monolithically in `apps/web/src/styles.scss` rather than in `libs/shared/theming`. This causes code duplication across pages and prevents clean sharing with `desktop`, `docs`, and UI component libraries.
2. **Observation 3** shows that `@import` in Sass is deprecated in modern Angular 20 / Dart Sass and produces build warnings.
3. Therefore, decomposing `apps/web/src/styles.scss` into structured partials inside `libs/shared/theming/src/lib/` (`_tokens.scss`, `_typography.scss`, `_buttons.scss`, `_forms.scss`, `_cards.scss`, `_tags.scss`, `_table.scss`, `_dialog.scss`, `_utilities.scss`) and aggregating them via `libs/shared/theming/src/index.scss` with modern `@forward` / `@use` eliminates duplication and solves the Sass deprecation warning.
4. Exporting strongly-typed constants in `libs/shared/theming/src/index.ts` allows Angular components, SVG charts, and canvas elements to access token names and theme colors type-safely via `@bill-book/theming`.

---

## 3. Caveats

1. The design reference contains two color variants: the original amber accent `#b68235` in `styles.css` and the orange/ink theme `#f06311` / `#2f353f` in `Shell.dc.html`. As implemented in `apps/web/src/styles.scss` (line 515), the active app uses `--color-accent: #f06311` with `--color-ink: #2f353f`. Both ramps are preserved in the token specifications.
2. No external packages are required or permitted (per `AGENTS.md` and `PROJECT.md`).

---

## 4. Conclusion

`libs/shared/theming` is ready to be populated with:
1. **SCSS Partials Architecture**: 9 dedicated partials under `frontend/libs/shared/theming/src/lib/` aggregated by `src/index.scss`.
2. **TypeScript Token Exports**: `src/index.ts` exporting `CSS_VARS`, `THEME_PALETTE`, `LAYOUT_LAYERS`, and `BREAKPOINTS`.
3. **App Integration**:
   - `apps/web/src/styles.scss` imports `@use 'libs/shared/theming/src/index' as *;` and retains only web-specific auth-shell rules.
   - `apps/desktop/src/styles.scss` imports `@use 'libs/shared/theming/src/index' as *;` removing the deprecated `@import`.
   - Component stylesheets can freely use `:root` CSS variables globally without individual imports.

---

## 5. Verification Method

1. **Verify TypeScript Compilation & Linting**:
   ```powershell
   cd frontend
   npm run typecheck
   npx nx lint theming
   ```
2. **Verify All Workspace Builds & Tests**:
   ```powershell
   cd frontend
   npm run check
   ```
   Confirm 0 errors, 0 deprecation warnings, and all builds (`web`, `desktop`, `docs`) passing cleanly.
