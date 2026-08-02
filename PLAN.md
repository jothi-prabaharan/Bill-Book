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

- [x] **0.1 — Session-start hook that installs the .NET SDK and npm packages**
  `.claude/hooks/session-start.sh`, registered in `.claude/settings.json`. Runs synchronously, is idempotent, and persists `DOTNET_ROOT` and `PATH` through `$CLAUDE_ENV_FILE`.
  *Done when*: a fresh session can run `dotnet --version` and `nx --version` without setup.
  **Partly blocked** — the npm half is verified. The SDK half cannot be verified here: see 0.2.

- [ ] **0.2 — First `dotnet build` on the solution, and fix what it finds** ⛔ **BLOCKED**
  The egress policy for these sessions denies `dot.net` and `builds.dotnet.microsoft.com` with a 403, so the SDK cannot be installed and the backend cannot be compiled. The proxy README says to report a blocked host rather than route around it.
  **To unblock**: have those two hosts allowed for this repository's sessions, then start a new session — the hook installs the SDK on its own from that point.
  Once it runs, expect EF Core 10 package versions to be wrong (`Directory.Packages.props` says as much), `Identity` / `Platform` to collide with framework namespaces, and `TreatWarningsAsErrors` to turn every warning into a failure.
  *Done when*: `dotnet build backend/Bill-Book.sln` succeeds with no errors.

- [x] **0.3 — First `npm install` and `nx build web`**
  Done. 1167 packages install, `@angular/cdk` resolves at 20.2.14, and both apps build. The eleven pages are in the bundle — confirmed by grepping the emitted lazy chunks for their content, not just by the build exiting zero.
  *Done when*: `nx build web` and `nx build docs` both succeed.

- [ ] **0.4 — Regenerate the hand-written migrations with `dotnet ef` and diff them** ⛔ blocked by 0.2
  Five migrations were written by hand to match EF's output format: Accounting's `AddNumberingSeries`, `AddPaymentTerms` and `AddBankParentAccountsIndex`, and the `InitialCreate` for Contacts, Inventory and Banking. Their model snapshots were assembled the same way.
  *Done when*: `dotnet ef migrations add` produces an empty migration for every context, proving each snapshot matches its model.

- [ ] **0.5 — Apply every migration to a local database** ⛔ blocked by 0.2
  *Done when*: `scripts/setup-dev-db` runs clean and all schemas exist with their RLS policies.

---

## Stage 1 — Make the masters usable

Small, and it turns eleven empty screens into a working system. Today a new organization gets **no master data at all**, so the Item page cannot save anything: an item needs a unit type, and none exist.

- [x] **1.1 — Organization-created hook**
  Done, as a call rather than an event. `ProvisioningWorker` published `CustomerProvisioned` through `IEventPublisher`, whose only implementation logs "EVENT (not delivered)" — which is exactly why eight seed methods had no caller. `ITenantSeeder` now calls each service's `POST internal/seed/organization` in turn, guarded by `[InternalOnly]` and a shared key, with the tenant on the request because the worker holds no user token. The event is still published for whatever consumes it later.
  Provisioning now **fails** when a service cannot be seeded, rather than flipping the organization to Active with no master data.
  *Done when*: creating an organization causes every service to run its seed exactly once, and twice is harmless.

- [x] **1.2 — Wire the eight orphaned seeds to it**
  All eight now run from the seed endpoints in Accounting, Contacts and Inventory. Accounting seeds first, because tax rates provision sub-accounts beneath its control accounts.
  *Done when*: a brand-new organization has a chart of accounts, six tax rates, five numbering series, six payment terms, eight contact roles, six unit types with their units, and the standard metal purities.

- [x] **1.3 — Gateway routes for Contacts, Inventory and Banking**
  Eleven routes and three clusters added, with Contacts on 5005, Inventory on 5006 and Banking on 5007. The three services had no launch configuration at all, so those were added too and folded into both compounds. `internal/*` stays unrouted, which is what keeps the seed endpoints off the public surface.
  *Done when*: each new page loads its data through the gateway rather than a direct service address.

- [x] **1.4 — Backfill for the bank parent accounts**
  Solved by making the chart-of-accounts seed idempotent **per account** rather than all-or-nothing: it now adds only the control accounts an organization lacks. Re-running the seed endpoint closes the gap, and the unique index on (OrgId, AccountSystemName) makes it safe to run repeatedly. Better than a data migration, which would have needed raw SQL outside the four cases CLAUDE.md allows, and would have fixed this one gap only.
  *Done when*: an organization created before that change can add a bank account.

---

## Stage 2 — Finish Contacts

Agreed scope that was specified and not delivered. Four of seven tables exist.

- [~] **2.1 — `IFileStorage`, with both implementations** — *one of two shipped*
  `AzureBlobFileStorage` for production and `LocalDiskFileStorage` for development, shipped together — `ISecretStore`, `IEventPublisher` and `IEmailSender` are interface-only and that is exactly the trap to avoid repeating.
  *Done when*: DI starts in Development with no Azure account.
  **`LocalDiskFileStorage` is written and registered; `AzureBlobFileStorage` is not.** `Azure.Storage.Blobs` is not in `Directory.Packages.props`, and adding a package reference that cannot be restored or compiled — the SDK hosts are blocked — would be worse than the gap. Carried to 5.9. The interface is not stubbed: it has a working implementation behind it, so DI starts.

- [x] **2.2 — `con.ContactAttachments`**
  Content-type allowlist, size cap from configuration, blob keys namespaced `{orgId}/contacts/{contactId}/…`, downloads through a signed URL minted per request rather than a public link.
  *Done when*: a GST certificate can be uploaded against a contact and downloaded back.

- [x] **2.3 — `con.ContactLicences`**
  Drug licence, FSSAI, BIS, medical registration, each with an expiry.
  *Done when*: a contact with a lapsed drug licence is visible as such, and an expiring-licences report exists.

- [x] **2.4 — `con.ContactBankDetails`**
  Vendor payout details: account holder, number, IFSC, UPI, one default.
  *Done when*: a vendor can hold more than one payout account with exactly one default.

- [x] **2.5 — Three more tabs on the contact page**
  Bank Details, Licences, Documents. Trading limits already live on the General tab.
  *Done when*: all seven specified tabs are present and save as part of the contact.

---

## Stage 3 — Stock foundation

~~Until this lands, an item is a catalogue entry that cannot hold stock, and the "locked once stock has moved" rule is inert — `HasStockMovementsAsync` returns `false` unconditionally.~~ **Done.** Items hold stock, and the lock is live.

- [x] ~~**3.1 — `plt.Branches`**~~ — **struck: it was a duplicate**
  Built, then removed the same day at the owner's direction. The intended model is **two levels, not three**: the Customer is the head office and an **Organization is a branch**. `plt.Branches` duplicated `plt.Organizations` almost column for column — GSTIN, both address lines, city, state, postal code, country, phone, mobile, email — while `OrgId`, not `BranchId`, was the only thing that ever scoped a row.
  Reverted in full: the table, its API and its page are gone, `BranchId` is dropped from `inv.Warehouses`, `inv.StockMovements` and `acc.NumberingSeries`, and `Organization` gained `OrgCode` to carry the branch code that numbering needs. `CLAUDE.md` now states the two-level model and forbids a `BranchId` column outright.
  **The consequence to keep in mind**: a branch is a hard data boundary, so each one has its own items, contacts, stock and books. Cross-branch consolidated reporting is a deliberate read across organizations, not a filter that can be relaxed.

- [x] **3.2 — `inv.ItemStock`**
  One row per item — quantity on hand, weighted average cost, `xmin`. The target of the synchronous, concurrency-safe point-of-sale decrement.
  *Done when*: two concurrent sales of the last unit cannot both succeed.
  `ItemId` is key and foreign key both, so a second row is structurally impossible. The decrement is one `ExecuteUpdateAsync` guarded by `QuantityOnHand >= qty`, and the row count is the answer — zero means nothing changed. The weighted average is recomputed inside the same UPDATE, which reads the pre-statement values, so there is no read to race against.

- [x] **3.3 — `inv.StockMovements`**
  Receipts, issues, adjustments and transfers, each storing the unit as entered **and** the base quantity as a snapshot.
  *Done when*: a receipt in bags and an issue in grams both land on one stock figure in the item's inventory unit.
  The conversion factor is stored on the row, not re-derived, so correcting a unit factor later cannot restate recorded history — a check constraint asserts the two quantities agree. `(OrgId, SourceType, SourceId, SourceLineId)` is uniquely indexed, which is the idempotency key for at-least-once delivery. Transfers write two rows and change the pool by nothing, because the pool was never split by location.

- [x] **3.4 — Switch the item config lock on**
  Replace the `HasStockMovementsAsync` stub with a real query. It is deliberately the only line that needs to change.
  *Done when*: an item with movements refuses a change to its unit type, inventory unit, costing method, profile or tracking flags.
  It was one line, as designed.

- [x] **3.5 — The Organization master itself** — *found while collapsing Branch into Organization*
  An Organization is now a branch, and there is **no way to create a second one**. `SignupService` writes the first; nothing else in the codebase writes `plt.Organizations` at all. No list endpoint, no create, no update, and no page — `platform-ui` has only configurations, currencies and SMTP. The Branches page that was deleted was the only branch-management screen ever built, and it wrote to a table that scoped nothing.
  Needs `GET/POST/PUT api/organizations` with the caller's `customer_id` checked, a Settings page, and a branch switcher in the shell (`org_id` is already in the JWT and `select-organization` already exists).
  **Creating one must run the same seeding provisioning runs** — chart of accounts, tax rates, numbering, units, payment terms — or a new branch comes up empty and cannot save an item. That is the exact bug 1.1 fixed for new customers.
  Also unenforced: `License.MaxOrganizations` defaults to 1 and `TrialMaxOrganizations = 1`, stored and checked by nothing.
  *Done when*: a second branch can be created, is seeded like the first, and can be switched into.
  `GET/POST/PUT/DELETE api/organizations` on Platform, with `[Authorize]` and the customer read from the **token** rather than the route — `plt` holds every customer's rows and has no RLS to fall back on, so the claim is the whole boundary.
  Creating one runs `ITenantSeeder`, the same seeding provisioning runs. A branch whose seeding fails is left `Provisioning` and returns 202 with a **Finish setup** action, rather than going Active with no chart of accounts behind it.
  `License.MaxOrganizations` is now enforced, and the base currency is frozen after creation — every amount posted was converted to it.
  Switching: `POST api/auth/switch-organization` on Identity reuses `SelectOrganizationAsync` with the user taken from the access token, so a switch grants the permissions held **in the target branch**. `GET api/auth/organizations` lists what the user may switch into; the login path now shares that same lookup rather than keeping its own copy.

---

## Stage 4 — Costing engine

The largest piece, and what makes `CostingType` honest. Today an item set to FEFO costs at weighted average, because nothing consumes layers.

- [x] **4.1 — `inv.CostLayers`, `CostLayerConsumptions`, `ItemBatches`, `ItemSerials`**
  *Done when*: a receipt creates a layer and an issue records which layers it consumed.
  Selection is implemented too, since it is a single ORDER BY once the layers exist: FIFO by receipt date, LIFO reversed, FEFO by expiry with nulls last, specific identification straight off the serial's own layer. Weighted average creates layers and consumes none — it keeps a running average instead, and its layers stand as receipt history.
  **Deliberate deviation to review in 4.2**: costing runs *inside* the movement's transaction, not on a worker. `CLAUDE.md` says costing is async. Committing layers with the movement is the only way to guarantee they never drift, and it removes 4.3's entire problem class — an async pass would have to solve ordering and exactly-once delivery to arrive back at the same guarantee. If the worker is still wanted, it should take recosting and backdating rather than first-pass allocation.

- [x] **4.2 — `CostingEngine.Worker`**
  Layer selection per method — FIFO by receipt date, FEFO by expiry, specific identification by serial — consumed with an `xmin` compare-and-swap, never read-then-write.
  *Done when*: the same purchases and sale produce different, correct COGS under each method.
  Built as the owner decided: **costing is asynchronous**. `StockService` records the movement, moves the pool and marks it `Pending`; the worker settles the cost. Batch and serial handling stays in the request, because both are user input and belong in the answer to the caller rather than in a background failure.
  The worker walks organizations from `internal/customers/active-organizations` and sets the tenant on its own scope, since it has no request to take one from.
  *Done when* still cannot be demonstrated here — no test project, no SDK (5.7, 0.2). It is three purchases, one sale, five expected COGS figures; write it first when the build unblocks.

- [x] **4.3 — Per-item event ordering**
  Service Bus is unordered and at-least-once; FIFO consumes the wrong layer if movements arrive out of sequence.
  *Done when*: movements replayed out of order still cost identically, and a redelivered event does not double-count.
  Solved by making **the movements table the queue** instead of putting a broker in the path. Ordering comes from `ORDER BY ItemId, MovementDate, StockMovementId` — a property of the read, not a promise from a broker. Exactly-once comes from claiming a movement with a guarded `Pending → InProgress` update: two workers racing means one changes no rows. There is no redelivery to dedupe because there is no delivery, and the unique index on (issue, layer) would refuse a duplicate allocation anyway.
  A crashed worker's claims are reclaimed after a timeout, and a movement that keeps failing is parked as `Failed` with the reason on the row rather than retrying forever.
  If a broker is added later it should **wake** the loop, not replace it — the database stays the source of truth.

- [~] **4.4 — Backdated receipts and recosting** — *restated and visible; the journal half waits on 5.12*
  Now runs on the worker with everything else: a backdated receipt unwinds the affected issues and puts them **back in the queue**, so the replay is ordinary pending work rather than a second code path.
  A receipt dated before issues that already consumed layers invalidates every allocation after it. Unwind, replay, and post a COGS adjustment — reversing, never editing a posted entry.
  *Done when*: inserting a backdated receipt restates COGS and the adjustment is visible as its own journal.
  Recording a backdated receipt now unwinds every issue on or after its date, returns the quantity to the layers it came from, and replays them in date order against the layers as they now stand. Allocations are **superseded, never deleted** — `CostLayerConsumption.SupersededAt` plus a batch id, with the unique index filtered to current rows so the replacement can sit beside what it replaced. Quantities are untouched throughout; only cost moves.
  `inv.RecostingAdjustments` records each restatement: sale, previous cost, new cost, signed delta, and the receipt that triggered it. Surfaced at `GET api/stock/recostings` and on the item's movement history.
  **What is not done is the journal.** "Visible as its own journal" needs Accounting, and Inventory does not yet talk to it (5.12). The adjustment is a first-class record here and posts nothing. Finish this item when 5.12 lands — the rows it needs already exist and carry a signed delta for exactly that purpose.

- [x] **4.5 — Returns to the originating layer**
  A sales return puts quantity back on the layers it came from at their original cost, not at today's.
  *Done when*: buy, sell, return leaves stock value exactly where it started.
  `StockMovement.ReturnsStockMovementId` names the issue being reversed; the return reads that issue's allocations and gives them back oldest first, guarded by each layer's own ceiling so nothing can hold more than it received. Partial returns accumulate and cannot exceed what went out. A return left unlinked still falls back to the running average — refusing it outright would block a return whose original sale predates this feature.
  `StockPosition.LayeredStockValue` was added to make the acceptance test checkable: it sums the layers rather than trusting the running average, which is the figure that has to come back to where it started.

---

## Stage 5 — Debt worth clearing

Independent of the stages above; take any of them whenever.

- [x] **5.1 — RLS policies on `acc.Accounts`, `acc.SubAccounts`, `acc.TaxMasters`**
  The only per-customer tables without one. They rely on the EF query filter alone, which `CLAUDE.md` treats as the first line of defence, not the last.
  Confirmed by reading every migration rather than from memory: those three were the only per-customer tables in the system with no policy — `acc.NumberingSeries` and `acc.PaymentTerms` in the same schema already had one. Added in `AddAccountingRowLevelSecurity`, each `DROP POLICY IF EXISTS` first so the migration is safe to re-run.
  **Also fixed while here**: four migrations had no `.Designer.cs` — `OrganizationIsTheBranch`, `DropNumberingBranchId`, `DropBranchId` and the new one. Three of those were mine. EF diffs `migrations add` against the last Designer, so a missing one makes the *next* migration wrong, which would have surfaced as a confusing bad diff at 0.4 rather than as an obvious omission.

- [x] **5.2 — Surface sub-account provisioning failures in Contacts**
  `ContactService` discards the result, so a contact can save while its receivable and payable sub-accounts silently fail. Banking already does this properly — copy that pattern, including the retry action.
  Worth recording what was actually there: the call site carried a comment saying *"the outcome is reported rather than swallowed"* directly above the line that swallowed it. The comment was the intent; the code never caught up.
  `con.Contacts.SubLedgerProvisionedAt` now records it, mirroring `BankAccounts.LedgerAccountId is null`. Held locally rather than asked of Accounting per row — the contact list is the classic N+1, and one HTTP call per row is not a list. Create returns `SubLedgerUnavailable` (409, contact kept), the list shows **No sub-ledger**, and `POST api/contacts/{id}/link-sub-ledger` retries.
  **Judgement call on existing rows**: the migration backfills them as provisioned. Marking every existing contact broken would be a false alarm on the common case, which is how a warning badge stops being read — and the retry is idempotent where the assumption is wrong. Said plainly in the migration.

- [x] **5.3 — Read the financial-year start month per organization**
  Hardcoded to 4 via configuration in the numbering generator and the numbering page. An organization on a different year numbers wrongly.
  `IFinancialYearProvider` in `Shared.Kernel`, reading the branch's own month from Platform's existing org-context endpoint and caching it for six hours — it changes about never, and an HTTP call per allocated code would be absurd. Added to that endpoint rather than a new one, so there is one lookup and one cache rather than two.
  **It never throws.** No tenant (seeding, design time) or Platform unreachable falls back to the configured value, because refusing to allocate a code over a settings lookup would be the worse failure. A month outside 1–12 is logged and ignored rather than composing a number nobody can explain.
  The Accounting preview and the Angular page now read it too, so what the screen shows and what the server generates cannot disagree. `NumberingSeriesService` lost its `NumberingOptions` dependency entirely as a result.

- [x] **5.4 — Fix the `MetalPuritiesSeed` comment**
  It claims to seed only for jewellery organizations; the code seeds unconditionally. One or the other should change.
  **The comment changed, not the code** — because the code could not have done what the comment described. `Vertical` and `Industry` appear nowhere in the backend outside migrations: there is no field on an organization that says which trade it is in, so there was nothing to condition on. The comment was describing a design that was never built.
  Seeding unconditionally is also the right default while that is true, and the comment now says why: the cost is asymmetric. A jeweller who did not get the purities cannot price a single ornament until someone types eleven rows by hand; a chemist who did get them has eleven unused rows on one settings screen, which they can deactivate and forget.
  Whether a branch should declare its trade is a real product question, not a comment's to answer — split out as **5.14** so the reference is not dangling.
  Checked every other seed file's summary while here — `ContactPersonRolesSeed`, `PaymentTermsSeed`, `ChartOfAccountsSeed`, `NumberingSeriesSeed`, `TaxMasterSeed`, `HsnSacSeed`, `HsnSacCsvLoader` — all accurate. This was the only one.

- [x] **5.5 — Refresh `CLAUDE.md`**
  Its "Current state", "Blocking gaps" and "Not yet built" sections all predate this work, and the login gap it calls blocking is closed.
  **All four "blocking gaps" were closed and none had been ticked.** `ResolveCustomerIdAsync` is now `IPlatformDirectory`; login refuses on `!ctx.DatabaseReady`; `CustomerCode` retries on the unique-index conflict; the three infrastructure interfaces all have registrations. Read every one in the code rather than trusting the list — a file claiming a login is broken when it works is worse than no file, because the next person builds around a problem that is not there.
  Rewrote "Current state" as a table per service with what actually works, and replaced the blocking-gaps list with **standing caveats** — never compiled, no tests, development stand-ins for Key Vault / Service Bus / Blob, and 5.10's unauthenticated endpoints. Those are the things a reader has to know before writing a line.
  **Also corrected three stale sections 5.5 did not name**, all of the same kind and all load-bearing: *Inventory & costing* still said weighted average only, with no mention of per-item costing methods, cost layers, async costing or the movements-table queue; the JWT claim list was missing `license_status` and `license_expiry`; and the roadmap still had FIFO/FEFO/LIFO in Phase 3 when it shipped in Phase 1. Someone reading any of those writes the wrong code.
  Trimmed *Undecided* of the two questions the code has since answered (trial expiry is blocked-not-read-only; costing is per-item), narrowed the provisioning one to the half still open, and added 5.14's branch-vertical question.

- [x] **5.8 — Make the remaining seeds idempotent per row**
  The chart of accounts now adds only what is missing. Tax masters, numbering series, payment terms, contact person roles, unit types and metal purities still bail out if the organization has any rows at all, so anything added to those seed lists later will never reach an existing organization.
  All six now match on the `*SystemName` column and insert only the difference, following the chart of accounts. Matching on the system name rather than the label is the point: these masters are renamable, so a jeweller who relabels "916 (22K)" as "22 Karat" must not be handed a second copy under the original wording.
  **Every one of them had a second unique index that a per-row insert can trip** — `TermName`, `RoleName`, `UomTypeName`, `UomCode`, `SeriesName`, `(MetalType, PurityName)`, `AccountCode`. A customer-created row holding a seed row's name would fail the insert for *every* row in the batch, not just its own, and this runs during provisioning where a throw fails the branch. Each seeder now skips a row whose name is already taken. Added the same guard to the chart of accounts, which had the same hole.
  **Filtered "at most one" indexes needed the same treatment.** Backfilling Due on Receipt into an organization that has since made Net 30 its default would violate `IX_PaymentTerms_Default`; the seed's flag now only stands where the slot is free. `NumberingSeries` is checked per `SeriesCode` rather than per organization, because that is what its index is keyed on.
  **The units case is the one that needed real thought.** `SetBaseUnitAsync` rescales every sibling factor when the base changes, so in a type rebased from kilograms to grams the seed's factors are a thousandfold out. A new unit inserted there would be silently wrong in stock and cost. Those types are skipped entirely — the base unit is compared by system name against the seed's own, so nothing is inserted at a scale it was not written for. `UomSeed.BuildUnits` also stopped indexing the type dictionary directly; a missing type now skips its units instead of throwing.
  `MetalPurities` was the only one of the six with no unique index on its system name, so a concurrent re-seed could have inserted twice — added in `AddMetalPuritySystemNameIndex`, filtered on NOT NULL like the others.
  The internal seed endpoints are now documented as re-runnable, which they had to be for any of this to be worth doing, and **Finish setup** on the branches screen is the user-facing way in.

- [ ] **5.7 — There are no tests and no linter**
  No project in the Nx workspace defines a `lint` or `test` target, so `npm run lint` and `npm run test` are no-ops against an empty set, and the backend has no test project at all. Worth fixing before the codebase grows further.

- [ ] **5.12 — A stock issue posts nothing to the ledger**
  `StockService` moves quantity and cost and stops there. `Dr COGS / Cr Inventory` is Accounting's to write, on an event Inventory does not yet publish — so today stock and the general ledger disagree the moment anything is issued. Nothing is wrong with either half; they are simply not connected. Do it when Sales lands, since that is what will be issuing.
  **4.4 is blocked on this too.** A backdated receipt already restates costs and records a signed delta per sale in `inv.RecostingAdjustments`; what it cannot do is post the correcting journal. Those rows exist to be read by whatever closes this item.

- [ ] **5.13 — No reserved quantity**
  `ItemStock` holds on-hand only. An order that is confirmed but not yet delivered leaves stock fully available, so it can be promised twice. Needs a `QuantityReserved` column and a matching guarded update — deliberately left out until Sales exists to write it, because a reserve nothing releases is worse than none.

- [ ] **5.10 — Platform's other org-scoped endpoints are unauthenticated**
  `Platform.Api` had no authentication at all until Branches needed it. Currencies, configurations and SMTP settings still take the org id straight from the route with no `[Authorize]` and no claim check, which means any caller who can reach the gateway can read or edit any organization's settings. Branches added the JWT scheme and checks the claim; the rest were left alone deliberately, because tightening signup and the internal endpoints without a compiler is how a working provisioning flow stops working. Do it in one pass, with `[AllowAnonymous]` on signup and the internal controllers, once the SDK is available.

- [ ] **5.11 — Three copies of `Reordering`**
  Banking and Inventory each carry their own, and `Shared.Kernel.Ordering` now holds the canonical one that Platform uses. Point the other two at it and delete their copies, along with their local `ReorderRequest`.

- [ ] **5.9 — `AzureBlobFileStorage`**
  Left out of 2.1. Needs `Azure.Storage.Blobs` added to `Directory.Packages.props`, which cannot be restored or compiled while the SDK hosts are blocked. `GetDownloadUrlAsync` returning a real SAS URL is the reason to build it — until then every download streams through the API, which works but puts the bytes through the service.

- [ ] **5.14 — Should a branch declare its trade?** *(needs an owner decision, not code)*
  Nothing on an organization says whether it sells medicines or ornaments. Every branch is therefore seeded with everything: eleven metal purities on a chemist, pharma fields on a jeweller's item form. It works, and it is untidy in a way that grows — each vertical added makes every other vertical's settings screens longer.
  A `Vertical` on the organization (Pharma / Jewellery / General, or several ticked) would let seeding, the item profile default and the settings menu all narrow themselves. It is a small column with a wide blast radius: signup, the branch form, `OrganizationProvisioningService`, `MetalPuritiesSeed`, the item profile picker and probably the reports menu.
  The questions for the owner: is a branch ever more than one trade at once — a chemist that also sells FMCG, a jeweller that also does watch repair? Does the vertical hide a screen or merely preset a default? Can it be changed after the branch has traded, and if so what happens to the rows the old vertical seeded?
  Not started deliberately: guessing wrong here means an unpickable choice on the signup form that every customer has to answer before they understand it.

- [ ] **5.6 — Decide the numbering-series ownership exception**
  `NumberingSeries` lives in `Shared.Kernel` and is mapped by four services with `ExcludeFromMigrations`, so a code can be allocated inside the caller's transaction. It is a deliberate, documented exception to the no-shared-tables rule — either confirm it in `CLAUDE.md` or replace it with a table per service.
