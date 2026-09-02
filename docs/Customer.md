# Customer / Contacts Module

**A naming collision worth stating up front, found 24 August 2026.** This file's title and the section below it document `con` — Contacts, mapped by the **Master** service's `ContactsDbContext`, and built. That is a different thing from the **Customer** service (`Customer.Api`, schema `cus`, CRM + Support), which is unbuilt and is documented in its own section further down this file. `mst.Customer` is a third, unrelated thing again — the head office / tenant. Three names, three concepts. Read `con` below as Contacts; read `cus` further down as the Customer service.

**Schema:** `con`

## Overview
Manages customer and vendor profiles, contact roles, prepayments, and outstanding balances. Coordinates with the Accounting module's AR/AP subledgers.

## Task Checklist
- [x] **0.1 — Schema design:** `con.Contacts` and role indexes.
- [x] **1.1 — Initial seeding:** Seed master contacts upon tenant creation.
- [x] **2.1 — Contact Portal:** Basic self-serve views for statements.
- [ ] **2.2 — Prepayment advance routing:** Link payments to customer prepayment advances rather than specific documents when overpaid.
- [ ] **TBD — Credit limits and holds:** Enforce business rules during sales order generation.

---

# Customer service (`cus`) — CRM + Support

**C0 and C1 are complete and audited, 2 September 2026.** Antigravity scaffolded the backend (entities, `CustomerDbContext`, migrations, `LeadsController`/`TicketsController`) ahead of the process below — before the four open questions had a recorded answer and before Senior had reviewed the schema. This section records that audit: the four questions are answered, the schema/tenancy/RLS gates are verified against a real PostgreSQL, and the issues the audit found are fixed or logged as follow-ups for C2/C3. See "C1 audit findings — 2 September 2026" below for the detail.

One service over what used to be two — CRM and the support helpdesk — merged because they share a subject (the person on the other side of the relationship) and a lifecycle (a lead becomes a customer, a customer raises a ticket). A ticket wanting the campaign that won the account, or a lead wanting its open tickets, is a join rather than a service call.

**No column-level spec existed anywhere in this repository before this section** — `docs/Specification.md` has zero mentions of CRM, lead, ticket or campaign. What follows is that spec, confirmed.

## Open questions — confirmed 2 September 2026

These were proposals from Claude Code (senior); they are now decided, via the session that ran this audit, and are the spec going forward. Overriding one of them later is a documented decision, not a silent edit.

1. **Campaign/marketing automation in v1? Confirmed: no.** v1 is a lead's lifecycle (new → contacted → qualified → converted/lost) and a ticket's lifecycle (open → in progress → resolved → closed), nothing that sends anything on a schedule. A `Source` field on `Leads` (enum, not a table) records where a lead came from without building a campaign engine.
2. **Does `cus` need its own top-level "customer" entity? Confirmed: no.** `con.Contact` (Master service, already built) stays the one canonical record of a person or company Bill Book trades with. `cus.Leads` is pre-sale and points at nothing until conversion; `cus.Tickets` points at an existing `con.Contact`. This sidesteps the `mst.Customer` naming collision entirely by never introducing a competing entity — there is no `cus.Customer`.
3. **SLA policy: fixed per priority, or a configurable table? Confirmed: fixed for v1.** A `TicketPriority` enum (Low/Medium/High/Urgent) with a hardcoded due-by offset per priority in C#, no `cus.SlaPolicies` table yet. A configurable table is real scope; add it when a second branch's SLA actually needs to differ from the first, not speculatively.
4. **Lead → Contact conversion: does it create the `con.Contact`, or just link to an existing one? Confirmed: both.** `ConvertToContactAsync` creates a new `con.Contact` when none is named, or links to an existing `ContactId` when the lead names one (a lead from an existing customer's referral, say). Either way `cus.Leads.ConvertedContactId` is a **real foreign key** to `con.Contacts` — `cus` and `con` are schemas in the same per-customer physical database, unlike the `mst` cross-database case, so there is no reason to leave it unenforced. **As built today, only the "link to an existing ContactId" half works** — see finding C1-4 below; the "create a new Contact" half is C2/C3 scope, not yet implemented.

## Schema (C1) — built and audited

`OrgId`-scoped, `Shared.Kernel.Tenancy.OrgScopedEntity`, RLS policy per table, matching every other per-customer schema in this product.

```
cus.Leads
  LeadId            long, PK
  OrgId             Guid
  Name              string, required
  CompanyName       string?
  Phone             string?
  Email             string?
  Source            enum LeadSource   (Website, Referral, WalkIn, Other)
  Status            enum LeadStatus   (New, Contacted, Qualified, Converted, Lost)
  ConvertedContactId long?, FK -> con.Contacts.ContactId (real FK, same database)
  ConvertedAt       DateTime?

cus.Tickets
  TicketId          long, PK
  OrgId             Guid
  ContactId         long, FK -> con.Contacts.ContactId (real FK, same database)
  Subject           string, required
  Description       string?
  Status            enum TicketStatus   (Open, InProgress, Resolved, Closed)
  Priority          enum TicketPriority (Low, Medium, High, Urgent)
  SlaDueAt          DateTime?           (computed at creation from Priority, stored — never recomputed live, or a priority change would silently reprice history the way an FX rate must not)
  AssignedToUserId  Guid?               (unenforced — mst.Users is a different database, resolved in C#, batched, same pattern as CreatedBy/ModifiedBy)
  ResolvedAt        DateTime?
  ClosedAt          DateTime?

cus.TicketMessages
  TicketMessageId   long, PK
  OrgId             Guid
  TicketId          long, FK -> cus.Tickets.TicketId
  AuthorType         enum TicketAuthorType (Contact, User)
  AuthorUserId      Guid?               (unenforced mst.Users reference, set when AuthorType = User)
  Body              string, required
```

Every entity is a plain property bag inheriting `AuditableEntity` via `OrgScopedEntity` — no methods, no computed properties, matching hard rule 2. Every Data Annotation carries `ErrorMessage`, matching hard rule 3.

## Stage breakdown

| Stage | Owner | Task |
|---|---|---|
| **C0** | Repository owner | ~~Confirm or override the four open questions above.~~ **Done — see "confirmed 2 September 2026" above.** |
| **C1** | **Senior** (schema is where a tenancy mistake propagates silently — same reasoning as Reporting's R0) | ~~`Customer.Entity/TableEntities/{Lead,Ticket,TicketMessage}.cs`, the three enums, `Customer.Repository/CustomerDbContext.cs` with `DbSet`s + Fluent config + query filters, the initial migration with RLS policies, and the schema-coverage test~~ **[x] Done — audited 2 September 2026, see findings below. Antigravity had already written the entities, context and migrations; this stage became a review of that work rather than a from-scratch build, which is exactly the C1-1 finding below.** |
| **C2** | **Junior — Antigravity** | Controllers: `LeadsController` (CRUD + `POST leads/{id}/convert`), `TicketsController` (CRUD + status transitions + `POST tickets/{id}/messages`). Request/response models with `ErrorMessage` on every annotation. `OrgId` cross-org check on every action, `Forbid()` not `NotFound()` on mismatch, per `docs/ai-agent-structure-rules.md`. **Written, but two audit findings (C1-3, C1-4 below) are open against it — pick those up before C3.** |
| **C3** | **Junior — Antigravity** | `libs/customer/customer-core` (both currently empty scaffolds) and `libs/customer/customer-ui`: lead list/form, ticket list/form, wired into `apps/web`. Standalone components, `inject()`, signals, `.page.ts`/`.dialog.ts`/`.list.ts` suffixes, works at ~360px — same rules as every other module. **Cleared to start** — the schema/tenancy/RLS gates below are green. |
| **C4** | **Junior — Antigravity** | Seed data: nothing reference-data-shaped is obviously needed (no fixed lead sources or ticket priorities beyond the enums), but confirm with C0's answers before skipping this stage outright. |

**Done when:** a lead can be created, converted to a contact (existing or new), a ticket can be raised against that contact, worked through its status lifecycle with a message thread, and none of it is visible to a second organization in the same database — verified by querying as that organization, never by reading the diff, per the gate discipline in `docs/Reporting.md` §9.1.

## C1 audit findings — 2 September 2026

Verified against a real PostgreSQL (`CUSTOMER_TEST_DB`), migrating from scratch and running `Customer.Api.Tests.CustomerQueryFilterTests` — never by reading the diff, per the gate discipline this section is required to follow.

**Gates passed, all six tests green:**
- Every `OrgScopedEntity` in `cus` (`Lead`, `Ticket`, `TicketMessage`) carries the global `CustomerId`+`OrgId` query filter, inherited from `Shared.Kernel.Tenancy.TenantDbContext` — no per-entity code to get wrong.
- `xmin` is mapped as the concurrency token on all three.
- A second organization gets zero rows back for both `Leads` and `Tickets` — verified by querying, not asserted.
- A second customer (sharing the same physical database) gets zero rows back for `Leads`.
- Row-level security (`FORCE ROW LEVEL SECURITY` + a `CustomerId`+`OrgId` policy, matching `RlsConnectionInterceptor`'s `app.current_customer_id`/`app.current_org_id`) covers all three tables in `cus`.

**C1-1 (critical, fixed).** The migration-squash commit (`befdac6`, "squash EF Core migrations into single initial schemas per module", same day) silently dropped every RLS policy in the product. RLS lives in hand-written raw SQL inside each migration's `Up()`; a squash that regenerates a migration from the current EF model has no way to know that SQL exists, so the new `InitialXSchema` migrations came out with tables, indexes and FKs but zero `ENABLE ROW LEVEL SECURITY` and zero `CREATE POLICY` anywhere in the backend — confirmed by grep across every service after the squash. Accounting also lost its five deferred constraint triggers (the third of the three balance checks on `Journals`/`JournalLedger`/`SpendMoney`/`ReceiveMoney`), and Customer's own migration lost its real FK to `con.Contacts`. This was caught only because `Row_level_security_covers_every_table_in_the_schema` was run for real against a freshly-migrated database rather than trusted from the last time it passed — the same lesson this repository already learned once from the `sal.SalesRegister` gap. **Fixed**: restored the RLS policies, the Accounting triggers, and the Customer→Contacts FK into the new squashed migrations for Customer, Accounting, Sales (including two tables — `ReminderProfiles`/`ReminderLogs` — added after the squash's source migrations and missed on the first pass), Purchase, Inventory and Master/Contacts. Verified with a full `dotnet test` against fresh per-service databases: 642 of 643 backend tests pass (the one failure, `Accounting.Api.Tests.ReconciliationMatchingTests.GetSuggestedMatches_FindsExactAmountWithinThreeDays`, is a pre-existing test-data bug — a hardcoded `BankAccountId` that the test never creates — unrelated to this fix and to `cus`).
- **This means every schema's migration history should be treated as suspect after any future squash.** A squash is not a refactor a diff review catches; only a from-scratch migrate against a real database, with the RLS/trigger-coverage tests each service already has, catches it.

**C1-2 (accepted, not a defect).** `cus.Leads.ConvertedContactId` and `cus.Tickets.ContactId` are enforced as real foreign keys to `con.Contacts` via raw SQL in the migration (`ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY`), rather than an EF Fluent-API relationship. This is deliberate, not an oversight: `Contact` is `Master.Entity`'s type, and modelling the relationship in EF would mean `Customer.Repository` referencing another service's Entity project — a boundary this codebase does not cross anywhere else. Raw SQL here is the same kind of necessary exception RLS already is (hard rule 1 lists `CREATE DATABASE`, RLS, triggers and `set_config`; a same-database cross-schema FK to another service's entity belongs in that list for the same reason — the LINQ alternative would cost more than it buys). The EF model legitimately does not know about this FK, the same way it does not know about RLS — that is expected, not a model/database mismatch to chase.

**C1-3 (open, for C2).** Neither `LeadsController.Convert` nor `TicketsController.Create`/`AddMessage` validates that the `ContactId`/`ConvertedContactId` supplied by the caller belongs to the caller's own org before accepting it. The database FK only proves the id exists somewhere in `con.Contacts` — not that it belongs to this branch. Because `ContactId` is a plain sequential `long`, a caller could in principle link a lead or ticket to a contact from another org in the same customer's database. RLS stops that contact's *own* rows from being read back through `con.Contacts` directly, but does not stop `cus.Tickets.ContactId` from pointing at it. Fix in C2: either validate the contact's `OrgId` via Master's contacts API before accepting the reference (per hard rule 8 — never reach into another service's `DbContext`), or extend the FK to a composite `(CustomerId, OrgId, ContactId)` referencing a matching unique key on `con.Contacts` (a larger, cross-service migration change, flagged here rather than made unilaterally during this audit).

**C1-4 (open, for C2/C3).** `LeadsController.Convert` only implements "link to an existing `ContactId`" — `ConvertLeadRequest.ContactId` is required, so the "create a new `con.Contact` when none is named" half of the confirmed C0 answer #4 does not exist yet. Not a tenancy issue, so left for C2 rather than fixed during this audit.
