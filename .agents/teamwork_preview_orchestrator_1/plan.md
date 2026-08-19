# Orchestration Plan: Bill-Book Desktop App Shell & Module Screens

## Overview
Implement the Bill-Book desktop application shell and module list/create screens in the existing Angular Nx workspace by translating the provided HTML/CSS design faithfully and abiding by all architectural, tenancy, and design constraints.

## Step 0: Survey
Spawn 3 Explorers / Spec Miners in parallel:
1. `teamwork_preview_spec_miner_design`: Examine `Shell.dc.html`, `styles.css` token file, layout rules, component hierarchy, CSS interactions, typography, borders, shadows, compact table specs.
2. `teamwork_preview_explorer_workspace`: Examine Angular Nx workspace (`frontend/`), library boundaries, `shared/theming`, `shared/ui-components`, `libs/app-shell`, module `-ui` and `-core` packages, dependencies, lint/test/build configs.
3. `teamwork_preview_spec_miner_api`: Examine backend API (`backend/Api`), `postman/Bill-Book.postman_collection.json`, DTOs for list/create/edit across Sales and other modules, verify DTO contract mapping and the constraint that accounting is labeled "Accounts".

## Step 1: Synthesis & PROJECT.md
Merge explorer outputs to produce:
- Architecture and module boundary definitions
- Feature Inventory mapping all requirements (R1-R5)
- Milestones with dependencies and write-ownership
- Interface Contracts (Shell components, Table inputs/outputs, DTO forms)
- Code Layout

## Step 2: Dual Track Dispatch
- Track A: E2E Testing Track (Spawn E2E Testing Orchestrator / Test Writer for Tiers 1-4)
- Track B: Implementation Track (Milestone Sub-orchestrators or sequential iteration loops)
  - M1: Design Tokens (`shared/theming`)
  - M2: Shared Data Table (`shared/ui-components`)
  - M3: App Shell (`libs/app-shell`)
  - M4: Sales Module Screens (`sales-ui`) & End-to-End Verification
  - M5: Remaining Module Screens (Purchases, Inventory, Accounts, etc.)

## Step 3: Gate Verification (Per Milestone)
- Explorer -> Worker -> Reviewers (2) -> Challengers (2) -> Forensic Auditor (`teamwork_preview_auditor`)
- Check gate criteria: build clean, lint clean, tests pass, no "Accounting" string, compact density verified, token usage verified, auditor CLEAN.

## Step 4: Final E2E Pass & Adversarial Hardening (Tier 5)
- Verify 100% E2E test pass across all tiers.
- Challenger adversarial coverage audit and hardening.

## Step 5: Final Report to User
