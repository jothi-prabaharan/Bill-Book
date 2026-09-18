# Printing

The print template master, the renderer, and the plan to move both into a service of their own.

**Nothing in stage P is built.** What *is* built is recorded in [`Master.md`](./Master.md) stage 7,
because that is where the code lives today. This file is the design for taking it out, written
down so the argument does not have to be reconstructed from a diff.

---

## Where things stand

| | |
|---|---|
| **Built** | `con.PrintTemplates`, the ten-route API, the twelve-type catalogue, the renderer, `PrintTemplateId` on all fourteen document headers. See [`Master.md`](./Master.md) stage 7 |
| **Not built** | The per-document print route, the Angular editor, PDF/A |
| **Planned here** | Extracting all of it into an eighth service, `Printing`, on schema `prt` |
| **Decided** | Full move; callers push the payload under the user's token; build in slices on approval |
| **Waiting on the owner** | Whether anything has been deployed — P2 drops a table. See the warning there |

---

## Why a service at all, when this repository merged twelve into seven

`CLAUDE.md` records the three merges and the reason each time: two services were two halves of one
job. **An eighth has to answer that, not ignore it.** The answer is that printing is unlike all
three, and in the specific way each merge turned on:

- **Nothing in printing shares a transaction with anything else.** A template is read and a
  document is rendered; no ledger row, no stock movement, no numbering series is taken in the same
  breath. Accounting and Banking merged precisely *because* they could not share a transaction
  while separate. That pressure does not exist here.
- **It is the only part of the product that will grow a native dependency** — a PDF engine,
  possibly a headless browser, fonts. Keeping that out of the seven services that post ledger rows
  is worth a boundary by itself.
- **It is the only work that is CPU-bound and bursty.** A hundred-page statement run should not
  compete for the same process as a till taking a sale.

**This is an argument, not a fact.** If it turns out wrong, the reversal is cheaper than the three
merges were: printing owns one table and reads nobody else's.

## The decision the whole design turns on

`Master.md` 7.7 asks how an internal call carries its branch. Today Printing would need one:
Master holds the template, the document belongs to Sales, and rule 8 forbids Sales reading `con`.
`TenantMiddleware` fills the tenant context **only for an authenticated request**, so an
`[InternalOnly]` endpoint has no branch to scope to.

**Callers push the payload under the caller's own token.** Sales builds `{singles, lists}` from
its own data and POSTs it to Printing with the user's bearer token; Printing resolves the
template for that branch and renders.

Tenancy then travels in the JWT exactly as it does for every other user-facing request. No
internal endpoint has to invent a branch, and no service reads another's tables.

**7.7 is deleted rather than inherited, and that is the strongest argument for the split.** Pulling
the template the other way — Sales asking Printing for it — would have relocated the same question
onto a new boundary and settled nothing.

---

## What moves, and what deliberately does not

`Shared.Kernel.Printing` splits along the line between **contract** and **machinery**.

**Stays in `Shared.Kernel.Printing`** — what both sides must agree on: `PrintPayload`,
`PrintFormatContext`, `PlaceholderCatalog`, `DocumentTypeCatalog`, `PlaceholderType`,
`PlaceholderKind`, `MergeTags`.

Sales has to know an item row is keyed `Item.ItemName` to build a payload at all, and Printing has
to know the same to resolve it. That is a shared contract in the sense `Shared.Kernel.Tax` is, and
splitting it would leave two lists to keep in step.

**Moves to `Printing.Entity`** — the stored shape: `PrintTemplate`, `PrintSettings`,
`PrintContent`, `SegmentMargin(s)`, `PrintSegment`, `SegmentPosition`, `PrinterType`, `PaperSize`,
`PrintJson`, and the request/response models.

**Moves to `Printing.Api`** — the machinery nobody else calls: `PrintRenderer`,
`PrintSubstitution`, `PrintSettingsValidator`, `SegmentSanitizer`, `PrintGeometry`, `PrintMetrics`,
`DefaultLayoutGenerator`, `SamplePayload`, `MaskFormatter`.

**Does not move**: `PrintTemplateId` on the fourteen document headers. It is an unenforced `long`
and goes on pointing at a row in another schema exactly as it did — the schema's name changes and
nothing else does.

**`HtmlSanitizer` and `AngleSharp` move with the sanitiser**, off `Shared.Kernel`. That is a real
gain on its own: seven services currently carry an HTML parser none of them touches.

---

## Stage P — the extraction

- [x] **P1 — Scaffold the service.** Three projects under `backend/Api/Printing/` plus
      `tests/Printing.Api.Tests/`, added to `Bill-Book.sln` (51 projects). `PrintingDbContext :
      TenantDbContext` on schema `prt`, mapping `PrintTemplate` and the `ErrorLog` the base class
      supplies.
      *Done when*: the service builds, starts, and answers nothing — with Master's copy untouched.
      **Done.** It builds with zero warnings under `TreatWarningsAsErrors`, starts on port 4508,
      migrates `prt` into `IN000001`, and answers 401 to every route including `/openapi/v1.json`
      — the `FallbackPolicy` default-deny, with no controller yet to let anything through.

      `Program.cs` mirrors Customer's: JWT, `TenantContext`, `RlsConnectionInterceptor`,
      `AuditSaveChangesInterceptor`, `AddBillBookReliability<PrintingDbContext>()` (hard rule 13),
      the tenant database resolver, OpenAPI. `Master:BaseUrl` is required in `appsettings` even
      though Printing calls Master for nothing: `AddBillBookAuthentication` reads it to fetch the
      signing key, and startup throws without it.

      **Nothing is deleted in this slice.** `main` is never in a state where printing is
      half-moved. `Printing.Entity.TableEntities.PrintTemplate` is therefore a second class rather
      than a moved one, and `PrintSettings` / `PrintContent` stay in `Shared.Kernel.Printing`
      where Master is still reading them. Both copies exist on purpose until P3.

      One thing that had to be added rather than shared: `HttpCurrentUser` is copied verbatim into
      each of the seven services, so Printing has the eighth copy. Folding the eight into
      `Shared.Kernel` is a change to seven services that had nothing to do with this one.

- [ ] **P2 — Schema, and the cutover.** Create `prt.PrintTemplates`; drop `con.PrintTemplates`.
      *Done when*: `prt` carries both filtered unique indexes and an RLS policy that is enabled
      **and FORCEd**, and `con` no longer has the table.

      **The create half is done; the drop is not, and is what this box is still open for.**
      `20260918205343_InitialPrintingSchema` creates `prt.PrintTemplates` and `prt.ErrorLogs`
      with both filtered unique indexes and a hand-written RLS block — `ENABLE`, `FORCE`, and a
      `FOR ALL USING` policy per table. Applied to a database dropped and rebuilt from zero,
      `pg_class` reports `relrowsecurity` and `relforcerowsecurity` true with one policy each, and
      `information_schema` reports no `%Id1` shadow column. `prt` joins the schema list in
      Master's `DatabaseMigrationService` (7 → 8), and `Printing.Api.Tests` links
      `tests/Shared/RlsAudit.cs` — five tests, no skips, from a dropped database.

      The policy was checked for being vacuous, not just for being green: dropping
      `printtemplates_tenant_isolation` by hand turns the assertion red with
      `PrintTemplates (no policy)`. That check is worth repeating on any suite that claims RLS,
      for the reason in the note directly below this stage.

      > **⚠ This slice drops a table. Confirm before running it.**
      > It is safe only on the greenfield assumption `CLAUDE.md` relies on — no released
      > deployment, no customer data — which makes a `prt` create plus a `con` drop a rename
      > rather than a migration. **If anything has been deployed since 6 September 2026, this
      > slice needs a copy step first.**

      RLS is written by hand, as EF generates none. `prt` joins the schema list in
      `DatabaseMigrationService`, and `tests/Shared/RlsAudit.cs` is linked into the new suite —
      the caveat in `CLAUDE.md` about RLS being "enforced everywhere and asserted nowhere" is
      exactly what that link exists for.

- [ ] **P3 — Move the code.** Entity, models, service, controller and the renderer machinery, as
      split above.
      *Done when*: the 23 service tests pass in `Printing.Api.Tests` against `PRINTING_TEST_DB`,
      and `Master.Api.Tests` no longer knows what a print template is.

      `PrintTemplateService` keeps its `BeginScopeAsync` transaction and its outcome enum; the
      controller keeps the five refusal codes.

      **Not a new permission module.** Both the module list and the action list are closed sets
      seeded in `AdminDbContext`, and a controller naming an unseeded module is a locked door for
      every role including Owner — which shipped once already, in Customer. A `printing` module
      would need an Admin migration reseeding `Permissions` and `RolePermissions`; designing print
      stationery is a settings act and `settings.view` / `settings.edit` already say so. Worth
      revisiting only if printing ever needs a permission settings does not.

      `Printing.Api.Tests` carries `EndpointGuardAudit` like the other seven.

- [ ] **P4 — The render endpoint, which is the point of the exercise.**
      *Done when*: a real invoice renders through Sales end to end, and `Master.md` 7.6 can tick.

      ```
      POST /api/print/{documentTypeCode}     { templateId?, payload }   → paginated HTML
      POST /api/print/preview/{templateId}   { }                        → HTML from sample data
      ```

      Both under the caller's token. Printing resolves the template — explicit id → branch default
      → platform seed — renders, and answers. It reads no other service's tables and holds no
      document.

      `IPrintClient` in `Shared.Kernel` posts the payload and forwards the caller's bearer token.
      Sales gets the first payload builder, for the invoice, behind `IPrintPayloadBuilder`. **The
      other thirteen are mechanical against that seam and are not in this stage.**

- [ ] **P5 — Provisioning, gateway, seed.**
      *Done when*: a new branch comes up with its twelve templates without Master owning the seed.

      `TenantSeeder.Services` gains `Printing` with a `Seeding:Printing` base URL in every
      `appsettings.*.json`; `PrintTemplateSeeder` moves behind Printing's own
      `InternalSeedController`, matching the six services that already have one. The gateway gains
      an eighth cluster and two routes — `/api/print-templates/{**catch-all}` and
      `/api/print/{**catch-all}` — in all four config files. CI gains `PRINTING_TEST_DB`.

- [ ] **P6 — Documentation, in the same commit as the code that needs it.** Hard rule 10, and this
      time not as a debt paid afterwards.

      `CLAUDE.md`: services 7 → 8, the schema list gains `prt`, the merge table gains a **split**
      row saying why this one went the other way, and the printing convention is rewritten.
      `Master.md`: stage 7 moves here and leaves a pointer; 7.6 ticks and **7.7 is struck through
      with its answer**. `Sales.md`: T3.4 gains the route that now exists.
      `frontend/apps/docs/content/`: the print-template section gets its own page, the manifest
      gains it, and `releases.md` gets a bullet.

---

## RLS is missing from every other schema, and `prt` is the only one that has it

**Found on 18 September 2026 while writing P2's RLS block, by trying to copy the one the other
seven schemas use. There isn't one.**

`ALTER TABLE ... ENABLE ROW LEVEL SECURITY`, `FORCE`, and `CREATE POLICY` appear nowhere in the
repository. `migrationBuilder.Sql` is called in no migration at all, and no startup path issues
them either — `DatabaseMigrationService` runs `CREATE DATABASE` and nothing else. The thirteen
migrations are all dated 14 September 2026, which is the squash: the hand-written RLS blocks were
in the chains that were squashed away, and nothing carried them forward.

**It went unnoticed because the developer databases predate the squash.** They still carry
policies an older chain created, named `{table}_tenant_isolation`, a string that exists nowhere in
the source any more. Every RLS assertion passes against them and fails against a database built
from the current migrations. `dotnet test` from dropped databases, 18 September:

| | Tests | Pass | Fail | Skip |
|---|---|---|---|---|
| Backend total | 1,042 | 1,022 | **20** | 0 |

Of the 20: **16 are RLS**, two in each of `acc`, `con`, `cus`, `inv`, `pur`, `rpt`, `sal` — every
schema in the product except this one. The remaining 4 are
`Reporting.Api.Tests.ReportLayerCertificationTests`, which were already red and are unrelated.
`Printing.Api.Tests` is 5 of 5.

**What this means, plainly.** RLS is the second of the two guards the tenancy model is built on,
and `CLAUDE.md` is explicit that neither is trusted alone. On any database provisioned from the
current chain — which is every database that will ever be provisioned, since nothing has shipped —
the EF query filter is the only thing keeping one branch's rows from another. Nothing leaks today,
because the filter works. But `IgnoreQueryFilters`, a raw command, or a context that forgets
`base.OnModelCreating` (which is exactly what `SalesDbContext` did) would walk straight past the
only guard left.

**Not fixed here.** Restoring it is a migration per service across roughly ninety tables, and it is
not the printing task. It is also not hard — this file's own `prt` block is the shape, and the
policy text is recoverable verbatim from any un-dropped developer database:

```sql
SELECT tablename, policyname, qual FROM pg_policies WHERE schemaname = 'cus';
```

Two things to carry into that work. The first is that **a passing RLS test proves nothing until it
has been made to fail** — drop one policy by hand and confirm the suite goes red, as P2 did here.
The second is that **CI does not catch this today and should**: it runs on a fresh `postgres:16`
service container with no databases, so it is already testing from zero, and it is therefore
already red on this. That it was believed green is the same stale-database story one level up.

---

## What it costs, stated plainly

- **An eighth deployable**, in a product whose own history says the last three service splits were
  mistakes. The argument above is that this one is unlike those; it is still an argument.
- **A network hop on every print**, where today there would be none. Acceptable because printing
  is not on the posting path — nothing waits on it to save a document.
- **`Shared.Kernel` gains a contract it did not have** (`IPrintClient`, `PrintPayload`), which is
  the price of not letting Printing read `sal`.
- **Four migrations across two contexts**, one of which drops a table.

## Verification

```
cd backend && dotnet build && dotnet test      # 1,036 today; must not fall
cd frontend && npm run check
```

Drop and recreate every test database rather than reusing — a half-migrated one is a wholesale
skip, and a skip reads as a pass.

After P2, prove the schema is clean and the policy is real:

```sql
SELECT column_name FROM information_schema.columns
WHERE table_schema = 'prt' AND column_name LIKE '%Id1';

SELECT relrowsecurity, relforcerowsecurity FROM pg_class
WHERE oid = 'prt."PrintTemplates"'::regclass;
```

After P4, render a real invoice and **look at it**. The last two renderer bugs — column headings
printed twice, serial numbers printed as money — passed all 204 renderer tests and were found only
by looking at the page.

**The four `Reporting.Api.Tests.ReportLayerCertificationTests` failures are red on `main` already**
and must not be counted as a regression from this work.

## Out of scope

- The remaining thirteen payload builders
- PDF/A and the blob archive — still the Syncfusion licence decision, unchanged by any of this.
  Note for whoever returns to it that **PDFsharp 6.1.1 is already pinned** and is not
  licence-blocked
- The Angular editor and master screen — still unbuilt, still `Master.md` 7.5
- Any change to `PrintTemplateId` on the document headers
