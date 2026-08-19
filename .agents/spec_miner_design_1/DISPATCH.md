# Dispatch Assignment

## 2026-08-19T14:45:04Z

Role: Design Spec Miner for Bill-Book application
Working Directory: `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_design_1`
Parent Orchestrator ID: `cc978969-df66-403f-b02a-6feb6cefd6fe`

Mandatory Inputs:
1. `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`
2. Design Reference: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html`
3. Design Tokens: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css`
4. Rules: `docs/coding-standards.md`, `docs/ai-agent-structure-rules.md`, `AGENTS.md`

Tasks:
1. Examine `styles.css` and catalog all design tokens: colors (primary, neutrals, borders, rules, underlines), typography (font-family, sizes, line-heights, tabular numbers for tables/figures), shadows (whispers), border radii, focus outlines, compact density dimensions, spacing.
2. Examine `Shell.dc.html` and extract the exact HTML/CSS structure for:
   - CSS grid layout of Shell
   - Fixed left rail (module navigation, active state, user menu at bottom)
   - Top bar (actions, searchable org name dropdown, financial year tag)
   - Breadcrumb strip (replacing page title, holding module-level controls/actions)
   - Scrolling content outlet
3. Extract specs for Shared Data Table:
   - Sticky header structure and inset bottom shadow
   - Hairline row rules
   - Compact density row heights and padding
   - Sorting indicators and hover/focus states
4. Verify all interaction states are CSS-only (no JS-driven hover/animation).
5. Document exact CSS custom properties and how they should be ported into SCSS `:root` in `shared/theming`.

Write full, detailed analysis in `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_design_1\analysis.md`.
