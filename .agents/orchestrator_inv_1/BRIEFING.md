# BRIEFING — 2026-08-20T18:50:00Z

## Mission
Build Stage T3.1 — Invoices (INV) for the Bill-Book ERP SaaS application, integrating backend API with accounting/inventory and constructing frontend UI.

## 🔒 My Identity
- Archetype: Project Orchestrator
- Roles: orchestrator, user_liaison, human_reporter, successor
- Working directory: C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1
- Original parent: parent
- Original parent conversation ID: 11c736af-129f-4f67-88b6-2f8c531e6989

## 🔒 My Workflow
- **Pattern**: Project
- **Scope document**: C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1\PROJECT.md
1. **Decompose**: Survey codebase and architecture via 3 Explorers, create Project decomposition across Backend, Frontend, and Verification.
2. **Dispatch & Execute**:
   - Step 0: Survey codebase via Explorers (Completed).
   - Milestone M1: Backend Domain Models & Service Foundation [done]
   - Milestone M2: Backend GL/Inventory Integration & Controller [done]
   - Milestone M3: Frontend UI, Workflows & GL Preview [in-progress]
   - Milestone M4: Comprehensive Testing, Docs & Release [pending]
3. **On failure**: Retry -> Replace -> Skip -> Redistribute -> Redesign -> Escalate.
4. **Succession**: At 16 spawns, write handoff.md, spawn successor.
- **Work items**:
  1. Survey & Architecture Mapping [done]
  2. Milestone 1: Backend Domain Models & Service Foundation [done]
  3. Milestone 2: Backend GL/Inventory Integration & Controller [done]
  4. Milestone 3: Frontend UI, Workflows & GL Preview [in-progress]
  5. Milestone 4: Comprehensive Testing, Docs & Release [pending]
- **Current phase**: 2B (Milestone M3 Execution)
- **Current focus**: Milestone M3 Worker implementation

## 🔒 Key Constraints
- NEVER write, modify, or create source code files directly.
- NEVER run build/test commands yourself — require workers to do so.
- NEVER investigate or explore the problem at the code level — dispatch Explorers for technical investigation.
- LINQ only. Never write raw SQL.
- Every per-customer table carries OrgId, global query filter, and RLS policy.
- Org context must reach query via RlsConnectionInterceptor.
- Entities are plain property bags inheriting OrgScopedEntity / AuditableEntity with DataAnnotations and ErrorMessage on all.
- Do not add packages.
- Angular 20 standalone components, inject(), signal()/computed(), async/await.
- Never reuse a subagent after it has delivered its handoff — always spawn fresh.

## Current Parent
- Conversation ID: 11c736af-129f-4f67-88b6-2f8c531e6989
- Updated: 2026-08-20T18:15:33Z

## Key Decisions Made
- Initialized Project Orchestrator for Stage T3.1 - Invoices.
- Completed 3 parallel surveys.
- Milestone M1 completed and verified.
- Milestone M2 completed and verified.
- First M3 worker failed due to 503 capacity error; replaced with fresh Worker (8c9ea571-f765-47e2-91e5-0999df43bd4e).

## Team Roster
| Agent | Type | Work Item | Status | Conv ID |
|-------|------|-----------|--------|---------|
| explorer_backend_model | teamwork_preview_explorer | Survey Backend Entities & DbContext | completed | da26b6c5-9f53-4865-b9ec-a4195cbdec5a |
| explorer_backend_posting | teamwork_preview_explorer | Survey Posting, Accounting/Inventory & Controller | completed | 5887a3c3-e5ba-4246-b8b6-83ecda56c1ef |
| explorer_frontend_ui | teamwork_preview_explorer | Survey Frontend UI, Workflows & Docs | completed | 8b68548f-2e52-4413-bd99-e578e84c9932 |
| worker_m1_backend_model | teamwork_preview_worker | Implement M1: Backend Domain Models & Service | completed | 98460f1f-e873-46da-bb45-edb5927b497f |
| worker_m2_backend_controller | teamwork_preview_worker | Implement M2: Controller & Integration | completed | f29b50e7-ff9d-48ff-869b-4a151dc28e08 |
| worker_m3_frontend_ui | teamwork_preview_worker | Implement M3: Frontend UI, Workflows & GL Preview | failed (503) | b8a1a367-6cba-4c73-91bb-77b333e952c4 |
| worker_m3_frontend_ui_2 | teamwork_preview_worker | Implement M3: Frontend UI, Workflows & GL Preview | in-progress | 8c9ea571-f765-47e2-91e5-0999df43bd4e |

## Succession Status
- Succession required: no
- Spawn count: 7 / 16
- Pending subagents: 8c9ea571-f765-47e2-91e5-0999df43bd4e
- Predecessor: none
- Successor: not yet spawned

## Active Timers
- Heartbeat cron: 01f9a570-97e1-4f48-a358-a0c24fb12427/task-13
- Safety timer: none

## Artifact Index
- ORIGINAL_REQUEST.md — Authoritative User Request
- DISPATCH.md — Initial Orchestrator Dispatch Record
- BRIEFING.md — Persistent working memory
- progress.md — Live state tracker and heartbeat
- plan.md — High-level milestone execution plan
- PROJECT.md — Global architecture, feature inventory & milestones
