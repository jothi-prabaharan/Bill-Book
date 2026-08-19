## 2026-08-18T16:57:01Z

You are an Explorer for the Component Test Strategy and Tier Decomposition.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\test_explorer_components_2

Read the following reference documents:
1. ORIGINAL_REQUEST.md: C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md
2. PROJECT.md: C:\Users\Praba\Source\repos\Bill-Book\PROJECT.md
3. TEST_INFRA.md: C:\Users\Praba\Source\repos\Bill-Book\TEST_INFRA.md
4. Repository rules: C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md

Your task:
1. Design the systematic 4-tier test case matrix for all 5 primitive components:
   - Tier 1: Feature / Contract Coverage (>=5 tests per component: writeValue, registerOnChange, registerOnTouched, setDisabledState, template rendering, default values).
   - Tier 2: Boundary & Corner Cases (>=5 tests per component: null/undefined/empty inputs, zero, negative amounts, max length, step bounds, min/max clipping, non-numeric filtering, rapid keystrokes).
   - Tier 3: Cross-Feature Interactions (pairwise / state interactions: dynamic disabled switching, dynamic min/max changes, switching between ngModel and direct inputs, clear and retype, currency symbol and paise formatting toggle).
   - Tier 4: Real-World Application Scenarios (Integration in complete Angular Form scenarios: template-driven forms with ngForm, reactive forms with formGroup and formControl, form validation states like invalid/touched/dirty, resetting forms, and multi-field data entry).
2. Ensure at least 5 Tier 1, 5 Tier 2, 1-2 Tier 3, and 1-2 Tier 4 tests per component, satisfying the full test thresholds (~11*5 = 55+ test cases).
3. Write your detailed test design and assertion matrix report to `C:\Users\Praba\Source\repos\Bill-Book\.agents\test_explorer_components_2\handoff.md`.
4. Send a message to parent when completed.
