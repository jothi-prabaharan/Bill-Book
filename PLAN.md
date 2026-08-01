# PLAN.md — build order

The order to build things in, and how to tell when each one is actually done.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds the tables and pages. This file holds **what to do next**, one item at a time.

## How to use this file

1. Take the **first unticked box**. The order is deliberate — later stages assume earlier ones.
2. Do it, and check it against its **Done when** line. That line is the test; "it compiles" is not the same as "it works".
3. Tick the box **in the same commit as the work**, the way release notes and docs already work here.
4. If a task turns out to be wrong or unnecessary, strike it and say why rather than deleting it. The reason is worth more than the tidiness.

---

## Where things stand

Verified on 1 August 2026, by reading the repository rather than from memory.

**Built** — Master, Platform, Identity, Accounting, Contacts, Inventory, Banking. 16 master tables added most recently, 11 pages, 5 hand-written migrations.

**The one fact that colours everything below**: nothing in this repository has ever been compiled. There has never been a .NET SDK or a `node_modules` available to it. Every service and both Angular apps are unverified against a build, including code that predates the recent work.

**Known-stale documentation**: `CLAUDE.md` still lists `AuthController.ResolveCustomerIdAsync` as a blocking gap. It is not — `IPlatformDirectory.ResolveOrgAsync` replaced it. Its "Not yet built" list is also out of date.

---

## Stage 0 — Make the build real

Until this stage is finished, every claim about this repository is "written", not "works". Nothing below it can be trusted, so nothing below it should be started.

- [ ] **0.1 — Session-start hook that installs the .NET SDK and runs `npm ci`**
  *Done when*: a fresh session can run `dotnet --version` and `nx --version` without setup.

- [ ] **0.2 — First `dotnet build` on the solution, and fix what it finds**
  Expect EF Core 10 package versions to be wrong (`Directory.Packages.props` says as much), and `Identity` / `Platform` to collide with framework namespaces.
  *Done when*: `dotnet build backend/Bill-Book.sln` succeeds with no errors.

- [ ] **0.3 — First `npm install` and `nx build web`**
  `@angular/cdk` was added to `package.json` but never resolved, and 11 pages have never been compiled.
  *Done when*: `nx build web` and `nx build docs` both succeed.

- [ ] **0.4 — Regenerate the hand-written migrations with `dotnet ef` and diff them**
  Five migrations were written by hand to match EF's output format: Accounting's `AddNumberingSeries`, `AddPaymentTerms` and `AddBankParentAccountsIndex`, and the `InitialCreate` for Contacts, Inventory and Banking. Their model snapshots were assembled the same way.
  *Done when*: `dotnet ef migrations add` produces an empty migration for every context, proving each snapshot matches its model.

- [ ] **0.5 — Apply every migration to a local database**
  *Done when*: `scripts/setup-dev-db` runs clean and all schemas exist with their RLS policies.

---

## Stage 1 — Make the masters usable

Small, and it turns eleven empty screens into a working system. Today a new organization gets **no master data at all**, so the Item page cannot save anything: an item needs a unit type, and none exist.

- [ ] **1.1 — Organization-created hook**
  Platform publishes it; each service subscribes. One mechanism, not a call per service.
  *Done when*: creating an organization causes every service to run its seed exactly once, and twice is harmless.

- [ ] **1.2 — Wire the eight orphaned seeds to it**
  `SeedForOrganizationAsync` exists with **zero callers** in: chart of accounts, tax master, numbering series, payment terms, contact person roles, unit types + units, metal purities.
  *Done when*: a brand-new organization has a chart of accounts, six tax rates, five numbering series, six payment terms, eight contact roles, six unit types with their units, and the standard metal purities.

- [ ] **1.3 — Gateway routes for Contacts, Inventory and Banking**
  Every page calls `/api/…` with nothing routing those paths.
  *Done when*: each new page loads its data through the gateway rather than a direct service address.

- [ ] **1.4 — Backfill migration for the bank parent accounts**
  Organizations seeded before Banking landed have no 1400 / 1500 / 2300 groups, so creating a bank account fails with "the chart of accounts has no bank parent group".
  *Done when*: an organization created before that change can add a bank account.

---

## Stage 2 — Finish Contacts

Agreed scope that was specified and not delivered. Four of seven tables exist.

- [ ] **2.1 — `IFileStorage`, with both implementations**
  `AzureBlobFileStorage` for production and `LocalDiskFileStorage` for development, shipped together — `ISecretStore`, `IEventPublisher` and `IEmailSender` are interface-only and that is exactly the trap to avoid repeating.
  *Done when*: DI starts in Development with no Azure account.

- [ ] **2.2 — `con.ContactAttachments`**
  Content-type allowlist, size cap from configuration, blob keys namespaced `{orgId}/contacts/{contactId}/…`, downloads through a signed URL minted per request rather than a public link.
  *Done when*: a GST certificate can be uploaded against a contact and downloaded back.

- [ ] **2.3 — `con.ContactLicences`**
  Drug licence, FSSAI, BIS, medical registration, each with an expiry.
  *Done when*: a contact with a lapsed drug licence is visible as such, and an expiring-licences report exists.

- [ ] **2.4 — `con.ContactBankDetails`**
  Vendor payout details: account holder, number, IFSC, UPI, one default.
  *Done when*: a vendor can hold more than one payout account with exactly one default.

- [ ] **2.5 — Three more tabs on the contact page**
  Bank Details, Licences, Documents. Trading limits already live on the General tab.
  *Done when*: all seven specified tabs are present and save as part of the contact.

---

## Stage 3 — Stock foundation

Until this lands, an item is a catalogue entry that cannot hold stock, and the "locked once stock has moved" rule is inert — `HasStockMovementsAsync` returns `false` unconditionally.

- [ ] **3.1 — `plt.Branches`**
  `BranchId` is already referenced by `acc.JournalDetails`, `acc.JournalLedger`, `inv.Warehouses` and `acc.NumberingSeries`, with no table behind any of them.
  *Done when*: a branch can be created and picked wherever `BranchId` is stored.

- [ ] **3.2 — `inv.ItemStock`**
  One row per item — quantity on hand, weighted average cost, `xmin`. The target of the synchronous, concurrency-safe point-of-sale decrement.
  *Done when*: two concurrent sales of the last unit cannot both succeed.

- [ ] **3.3 — `inv.StockMovements`**
  Receipts, issues, adjustments and transfers, each storing the unit as entered **and** the base quantity as a snapshot.
  *Done when*: a receipt in bags and an issue in grams both land on one stock figure in the item's inventory unit.

- [ ] **3.4 — Switch the item config lock on**
  Replace the `HasStockMovementsAsync` stub with a real query. It is deliberately the only line that needs to change.
  *Done when*: an item with movements refuses a change to its unit type, inventory unit, costing method, profile or tracking flags.

---

## Stage 4 — Costing engine

The largest piece, and what makes `CostingType` honest. Today an item set to FEFO costs at weighted average, because nothing consumes layers.

- [ ] **4.1 — `inv.CostLayers`, `CostLayerConsumptions`, `ItemBatches`, `ItemSerials`**
  *Done when*: a receipt creates a layer and an issue records which layers it consumed.

- [ ] **4.2 — `CostingEngine.Worker`**
  Layer selection per method — FIFO by receipt date, FEFO by expiry, specific identification by serial — consumed with an `xmin` compare-and-swap, never read-then-write.
  *Done when*: the same purchases and sale produce different, correct COGS under each method.

- [ ] **4.3 — Per-item event ordering**
  Service Bus is unordered and at-least-once; FIFO consumes the wrong layer if movements arrive out of sequence.
  *Done when*: movements replayed out of order still cost identically, and a redelivered event does not double-count.

- [ ] **4.4 — Backdated receipts and recosting**
  A receipt dated before issues that already consumed layers invalidates every allocation after it. Unwind, replay, and post a COGS adjustment — reversing, never editing a posted entry.
  *Done when*: inserting a backdated receipt restates COGS and the adjustment is visible as its own journal.

- [ ] **4.5 — Returns to the originating layer**
  A sales return puts quantity back on the layers it came from at their original cost, not at today's.
  *Done when*: buy, sell, return leaves stock value exactly where it started.

---

## Stage 5 — Debt worth clearing

Independent of the stages above; take any of them whenever.

- [ ] **5.1 — RLS policies on `acc.Accounts`, `acc.SubAccounts`, `acc.TaxMasters`**
  The only per-customer tables without one. They rely on the EF query filter alone, which `CLAUDE.md` treats as the first line of defence, not the last.

- [ ] **5.2 — Surface sub-account provisioning failures in Contacts**
  `ContactService` discards the result, so a contact can save while its receivable and payable sub-accounts silently fail. Banking already does this properly — copy that pattern, including the retry action.

- [ ] **5.3 — Read the financial-year start month per organization**
  Hardcoded to 4 via configuration in the numbering generator and the numbering page. An organization on a different year numbers wrongly.

- [ ] **5.4 — Fix the `MetalPuritiesSeed` comment**
  It claims to seed only for jewellery organizations; the code seeds unconditionally. One or the other should change.

- [ ] **5.5 — Refresh `CLAUDE.md`**
  Its "Current state", "Blocking gaps" and "Not yet built" sections all predate this work, and the login gap it calls blocking is closed.

- [ ] **5.6 — Decide the numbering-series ownership exception**
  `NumberingSeries` lives in `Shared.Kernel` and is mapped by four services with `ExcludeFromMigrations`, so a code can be allocated inside the caller's transaction. It is a deliberate, documented exception to the no-shared-tables rule — either confirm it in `CLAUDE.md` or replace it with a table per service.
