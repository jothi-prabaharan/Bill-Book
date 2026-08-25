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

**Not built.** `Customer.Api/Program.cs` is a deliberate placeholder — its own comments say to replace it wholesale, copying `Inventory.Api/Program.cs`, rather than extend it. No entities, no controllers, no migrations, no pages. This is the one Phase-1 service with nothing behind it.

One service over what used to be two — CRM and the support helpdesk — merged because they share a subject (the person on the other side of the relationship) and a lifecycle (a lead becomes a customer, a customer raises a ticket). A ticket wanting the campaign that won the account, or a lead wanting its open tickets, is a join rather than a service call.

**No column-level spec exists anywhere in this repository** — `docs/Specification.md` has zero mentions of CRM, lead, ticket or campaign. This section is that spec's first draft, written as an Antigravity brief rather than settled first and briefed after, because there is nothing to draft it from except the one open question already recorded in `CLAUDE.md`'s Undecided list: *"CRM: campaign/marketing automation in v1?"*

## Open questions — confirm before C1 starts

These are proposals from Claude Code (senior), not repository-owner decisions. Antigravity should not start C1 until the repository owner has confirmed or overridden them — inventing an answer here is exactly the mistake `AGENTS.md`'s "stop and ask" section exists to prevent.

1. **Campaign/marketing automation in v1? Proposed: no.** v1 is a lead's lifecycle (new → contacted → qualified → converted/lost) and a ticket's lifecycle (open → in progress → resolved → closed), nothing that sends anything on a schedule. A `Source` field on `Leads` (enum, not a table) records where a lead came from without building a campaign engine.
2. **Does `cus` need its own top-level "customer" entity?** Proposed: no. `con.Contact` (Master service, already built) stays the one canonical record of a person or company Bill Book trades with. `cus.Leads` is pre-sale and points at nothing until conversion; `cus.Tickets` points at an existing `con.Contact`. This sidesteps the `mst.Customer` naming collision entirely by never introducing a competing entity — there is no `cus.Customer`.
3. **SLA policy: fixed per priority, or a configurable table?** Proposed: fixed for v1 — a `TicketPriority` enum (Low/Medium/High/Urgent) with a hardcoded due-by offset per priority in C#, no `cus.SlaPolicies` table yet. A configurable table is real scope; add it when a second branch's SLA actually needs to differ from the first, not speculatively.
4. **Lead → Contact conversion: does it create the `con.Contact`, or just link to an existing one?** Proposed: both — `ConvertToContactAsync` creates a new `con.Contact` when none is named, or links to an existing `ContactId` when the lead names one (a lead from an existing customer's referral, say). Either way `cus.Leads.ConvertedContactId` is a **real foreign key** to `con.Contacts` — `cus` and `con` are schemas in the same per-customer physical database, unlike the `mst` cross-database case, so there is no reason to leave it unenforced.

## Proposed schema (C1) — for review, not yet committed

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
| **C0** | Repository owner | Confirm or override the four open questions above. Nothing below starts until this lands. |
| **C1** | **Senior** (schema is where a tenancy mistake propagates silently — same reasoning as Reporting's R0) | `Customer.Entity/TableEntities/{Lead,Ticket,TicketMessage}.cs`, the three enums, `Customer.Repository/CustomerDbContext.cs` with `DbSet`s + Fluent config + query filters, the initial migration with RLS policies, and the schema-coverage test (`OrgId` + query filter + RLS over the whole model) — the same shape as `Sales.Api.Tests.SalesQueryFilterTests`, which exists because a schema nobody queries is a schema nobody has checked. |
| **C2** | **Junior — Antigravity** | Controllers: `LeadsController` (CRUD + `POST leads/{id}/convert`), `TicketsController` (CRUD + status transitions + `POST tickets/{id}/messages`). Request/response models with `ErrorMessage` on every annotation. `OrgId` cross-org check on every action, `Forbid()` not `NotFound()` on mismatch, per `docs/ai-agent-structure-rules.md`. |
| **C3** | **Junior — Antigravity** | `libs/customer/customer-core` (both currently empty scaffolds) and `libs/customer/customer-ui`: lead list/form, ticket list/form, wired into `apps/web`. Standalone components, `inject()`, signals, `.page.ts`/`.dialog.ts`/`.list.ts` suffixes, works at ~360px — same rules as every other module. |
| **C4** | **Junior — Antigravity** | Seed data: nothing reference-data-shaped is obviously needed (no fixed lead sources or ticket priorities beyond the enums), but confirm with C0's answers before skipping this stage outright. |

**Done when:** a lead can be created, converted to a contact (existing or new), a ticket can be raised against that contact, worked through its status lifecycle with a message thread, and none of it is visible to a second organization in the same database — verified by querying as that organization, never by reading the diff, per the gate discipline in `docs/Reporting.md` §9.1.
