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

- [ ] **P1 — Scaffold the service.** Three projects under `backend/Api/Printing/` plus
      `tests/Printing.Api.Tests/`, added to `Bill-Book.sln`. `PrintingDbContext : TenantDbContext`
      on schema `prt`, mapping `PrintTemplate` and the shared `ErrorLog`.
      *Done when*: the service builds, starts, and answers nothing — with Master's copy untouched.

      `Program.cs` mirrors any existing service: JWT, `TenantContext`,
      `RlsConnectionInterceptor`, `AuditSaveChangesInterceptor`,
      `AddBillBookReliability<PrintingDbContext>()` (hard rule 13), the tenant database resolver,
      OpenAPI.

      **Nothing is deleted in this slice.** `main` is never in a state where printing is half-moved.

- [ ] **P2 — Schema, and the cutover.** Create `prt.PrintTemplates`; drop `con.PrintTemplates`.
      *Done when*: `prt` carries both filtered unique indexes and an RLS policy that is enabled
      **and FORCEd**, and `con` no longer has the table.

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
