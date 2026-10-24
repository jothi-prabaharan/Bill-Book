# Backend API audit

TK-260, 24 October 2026. Read-only: no code was changed. Every figure was measured by script over the C# in `backend/` (migrations, tests and build output left out) and is repeatable; the heuristic ones say so.

**The short answer.** The backend is not full of waste. It is broad: 23 services, 296 controllers, 1,181 actions, about 134,000 lines of code in the services and 147,000 with Shared.Kernel and the worker. The repeated code that can go **without losing a feature or changing a route** is about **10,200 to 11,800 lines, 7 to 8%**. Going further would mean removing features. Some of that saving is mechanical and provable by a diff (section 4, items 1 to 4); the rest touches posting logic and needs the owner's test runs.

## 1. The backend in numbers

| Service | Controllers | Actions | Code lines |
|---|---:|---:|---:|
| Reporting | 27 | 50 | 24,206 |
| Accounting | 48 | 205 | 20,003 |
| Master | 41 | 165 | 18,002 |
| Sales | 24 | 129 | 16,618 |
| Inventory | 18 | 105 | 9,975 |
| Purchase | 15 | 49 | 7,273 |
| Customer | 17 | 72 | 5,217 |
| Site | 11 | 43 | 4,382 |
| Printing | 9 | 23 | 3,822 |
| Payroll | 11 | 63 | 3,747 |
| Employee | 10 | 47 | 3,437 |
| TimeLeave | 7 | 42 | 3,278 |
| Performance | 9 | 28 | 2,550 |
| Student | 7 | 31 | 1,960 |
| Fee | 5 | 21 | 1,852 |
| Claims | 7 | 21 | 1,690 |
| Recruitment | 8 | 31 | 1,622 |
| Admission, WorkOrder, MaintenanceContract, Preventive, Facility, Attendance | 22 | 57 | 4,824 |
| **Services total** | **296** | **1,181** | **134,458** |

Where the lines are (all projects, 147,326 code lines): repositories 41%, controllers 14%, request and response models 8%, Shared.Kernel 6%, report catalog sources 5%, seed data 4%, entities 4%, DbContext configuration 4%, hosting 3%. **Controllers are thin; the weight is in repositories.**

Duplication: **9% of the code is copied exactly** between files (8,425 of 91,272 significant lines) and **30% repeats in structure** with only names changed. It is concentrated in the Sales and Purchase document repositories, `Program.cs`, DbContext configuration, the report catalog and the request and response models.

## 2. Rule check (CLAUDE.md hard rules)

| Rule | Result |
|---|---|
| Every endpoint authenticated | All 296 controllers carry `[Authorize]`; only `AuthController`'s sign-in and reset actions are anonymous, by design. |
| Every endpoint guarded by a permission | 281 controllers guard every action. 15 carry token-only actions (62 actions): self-service `me` pages, reference data and the menu. Two are writes worth a look (finding 1 and 2 below). |
| 2 Entities are property bags | 0 violations in 232 entity files. |
| 3 `ErrorMessage` on every annotation | **57 without one**: Inventory 26, Sales 23, Reporting 8 (finding 5). |
| 8 No service crosses another's DbContext | Clean. Only `Master.Api` references other services' repository projects, as the startup migration and seeding host. |
| 13 Transactions | Controllers use `BeginScopeAsync` (2, in `InternalStockController`). One raw `BeginTransactionAsync` outside the infrastructure that implements the scope (finding 7). |
| Tenancy: `IgnoreQueryFilters` | 69 uses: 37 in seeders, 27 with an explicit `OrgId` or `CustomerId` filter beside them, 5 in `BlankPhoneBackfill`, which says why it runs with no branch. No unfiltered read found. |
| 14 Failures go through `SqlErrorCatalog` | **58 `catch` blocks in controllers** write their own messages (finding 3). |
| 16 Business logic in repositories | **32 controllers read or write the database themselves** (finding 4). |
| 1 No raw SQL | 6 hits: 4 are the row-level-security and `CREATE DATABASE` code the rule allows. Two do not (finding 6). |
| `async void`, sync-over-async | None, apart from the four sync overrides in `DocumentQuotaInterceptor` that must block. |

## 3. Findings to act on

Ranked by what could hurt first.

1. **`TimeLeave/ApprovalsController.Act` (line 197, check at line 210).** The controller is guarded by the HRMS/Payroll add-on and a token, with no permission. The check that the caller is the step's approver is skipped when the caller has no employee record (`empId` is null), so such a user can act on a step assigned to someone else. A step with no approver is open to everyone on purpose (the comment says so); an assigned one should not be. Confirm the intent. The action also carries `"Approve"` and `"Reject"` as strings (lines 219, 326) where an enum belongs, and the controller makes 22 database calls itself.
2. **`Master/ComplianceController.MarkFiled` (line 195).** A write guarded only by a token and the business-type scope, with no permission: any signed-in user of the branch can mark a statutory filing as filed. (`ExtendDueDate` on the same controller does carry `platform.edit`.)
3. **Rule 14.** 58 `catch` blocks in controllers write their own message: Site 25, Master 14, Customer 11, Reporting 7, Accounting 1. Error bodies come in three shapes: 141 anonymous `{ message = … }`, 438 `MessageResponse` (the class is defined in six services) and 22 `Problem`/`ValidationProblem`.
4. **Rule 16.** 32 controllers (186 database calls) do the work a repository should: `ApprovalsController` 22, `TicketsController` 13, `LeadsController` 11, `MeProfileController` 11, `MasterController` 11, `ComplianceController` 11, then the `Internal*` lookup controllers in Master, Inventory, Reporting and Student. Moving it adds lines to repositories; it is compliance, not shortening.
5. **Rule 3.** 57 annotations with no `ErrorMessage`, for example `Sales.Entity/TableEntities/SalesRegister.cs` and `ReminderProfile.cs`.
6. **Rule 1.** `Master.Api/Services/DatabaseMigrationService.cs:513` builds `SELECT 1 FROM pg_database WHERE datname = '{targetDatabase}'` by string interpolation (the value is a shard name from configuration, and `CREATE DATABASE` itself is allowed, but the check should be parameterised). `Shared.Kernel/Tenancy/TenantDatabaseResolver.cs:124` is a raw, parameterised `SELECT` through `NpgsqlCommand`, outside the rule's four exceptions (CLAUDE.md already records it).
7. **Rule 13.** `worker/BillBook.Worker/Costing/CostingWorker.cs:306` calls `BeginTransactionAsync` directly instead of `BeginScopeAsync`.
8. **Orphan code. Removed by TK-261 (91 lines).** `backend/shared/Shared.Infrastructure` (two files, no project, not in the solution, a Syncfusion generator for a library D-11 dropped), `worker/BillBook.Worker/BackgroundWorkerService.cs` (the project template's stub, never registered), `AudienceRepository.ResolveForAudienceAsync` and `TableReservationRulesRepository.HoldsTable` (no caller; the query states the same test itself). **Two items first listed here were not dead and were kept:** `EInvoiceCredentials` is the documented credential-name scheme for the real e-invoice provider (D-24), used by the sandbox test and waiting for the provider; `LeaveRepository.AccrueLeaveAsync` is the only code in the product that adds to a leave balance (finding 12).
9. **Encoding.** 16 files in `Reporting.Repository/ReadModels` are Windows-1252, not UTF-8 (an em dash in comments). They compile; they show garbled in a UTF-8 editor or a diff.
10. **Possible N+1.** 45 loops await a database call inside the loop (Site 9, Customer 5, Accounting 5, Sales 4, Inventory 4, worker 4, Fee 3, TimeLeave 3). Heuristic: some are right (posting a ledger's legs one by one), so each needs a human look.

11. **Known from TK-219, not part of the scan.** The Reconciliation Report's own-currency `bookBalance` still counts revaluation postings, so it can differ from Bank Summary's closing balance for a foreign-currency bank account. The fix is to leave those postings out, reusing `BankSummaryReportRepository.RevaluationLegs`. It needs its own card.

12. **Found by TK-261: leave never accrues.** The TimeLeave card says leave accrual and rollover run as a hosted job. No such job exists, nothing calls `LeaveRepository.AccrueLeaveAsync` and nothing in the worker mentions accrual or rollover, so `Accrued` stays at zero and `CarryForwardKind` is stored and never applied. This is a missing feature and needs its own card.

## 4. What can be shortened

Same routes, same permissions, same answers. Estimates are conservative and from the measured repetition; none has been tried.

| # | Change | Lines saved | Risk | How to prove nothing changed |
|---|---|---:|---|---|
| 1 | **Remove the dead code** in finding 8. **Done, TK-261: 91 lines.** | 91 | None | Solution built clean, 0 warnings. |
| 2 | **Per-service copies become shared code.** `Program.cs` (2,668 lines, 63% repeated) takes one `AddBillBookService<TContext>` extension. `EditLogController` (23 copies, 440 lines), `InternalSeedController` (23, 945), `HttpCurrentUser` (23, 475), `DatabaseMigrationService` (22, 465 beyond Master's own) and `HttpCallerPermissions` (8, 105) become generic classes in Shared.Kernel. | ~3,000 | Low | Dump every route and DI registration before and after and diff; every host starts; the endpoint-guard tests. |
| 3 | **DbContext conventions.** 302 `HasKey`, 235 `HasColumnType`, 141 `HasConversion<string>().HasMaxLength(20)` and 156 `OnDelete` lines say the same thing per entity; model conventions and a helper say it once. 6,026 lines, 39% repeated. | ~1,500 | Low | The EF model snapshot of each of the 26 contexts must not change: `dotnet ef migrations has-pending-model-changes` and `MigrationModelTests`. A machine can prove this one. |
| 4 | **The report catalog states each column's key and type once.** A source's `Columns` and the 2,700-line `ReportCatalogSeeder` both name every column and its data type (checked on Bank Summary). Headers, order and defaults live only in the seeder, so only the overlap can go, not the whole list; `reports.json` feeds a separate table and is left alone. | ~800–1,000 | Moderate | Seed a fresh database before and after: `rpt.Reports` and `rpt.ReportDetails` identical; `POST api/reports/{key}/query` identical for every key. Check the overlap across more than one report before starting. |
| 5 | **One error contract.** One error body and one place that turns exceptions into it (the existing `ApiExceptionFilter` and `SqlErrorCatalog`), keeping the `message` and `errors` keys the pages read. Fixes finding 3. | ~500–700 | Moderate | Same status code and keys for each route's refusals; the frontend failure handling untouched. |
| 6 | **Twins merge.** `ReceiveMoney` and `SpendMoney` (1,083 lines, almost the same), `TransferMoney` beside them, and `RecurringBilling` in Sales and in Purchase (878 lines, about 90% the same). | ~800–1,000 | Moderate | Local round trips on PostgreSQL with the ledger diffed; the owner's suites. |
| 7 | **Request and response models.** The Quote, SalesOrder and PurchaseOrder families, and the `ListItem` and `Response` pairs that carry the same fields: 11,932 lines, 31% repeated. | ~1,500–2,000 | Moderate | Serialise every response type before and after and compare the JSON. |
| 8 | **Sales and Purchase document repositories.** Nine files, 7,806 lines, share the same header and line saving, numbering, status and conversion steps. A shared document base would hold them once. | ~2,000–2,500 | **High** | Posting, stock and ledger live here: the owner's suites plus a round trip for each document type with the ledger and stock diffed. Do this last. |
| | **Total** | **~10,200–11,800** | | |

Suggested order: 1, 2, 3, 4 (each provable by a diff), then 5, then 7, 6 and 8. Item 1 is done; the rest are taken one at a time, each as its own card.

## 5. Left alone, and why

- **Unused endpoints.** 188 of 952 routes have no caller in the frontend (144 plus 44 aliases). Almost all are features whose page is not built yet (the restaurant screens, savings schemes, payroll adjustments, loyalty), so removing them would lose features. The list can be regenerated on request.
- **Alias routes.** 14 controllers answer on two routes (`api/hrm/team` and `api/team`, and the `/api/users/*` aliases on `AuthController`). Dropping the old route saves almost no code and could break a caller.
- **Comments.** 30% of the raw lines (211,126) are comments and blanks. They carry the reasons behind the rules; they are not counted as code and were not touched.

## 6. How this was measured, and its limits

Ad hoc Python over the source, run from a scratch folder (not committed): controller and action counts by attribute, guards by reading the attributes above each class and action, rule scans by pattern, clones by hashing windows of 6 consecutive code lines (exact) and 8 lines with identifiers, numbers and strings replaced (structural). Heuristics: unused endpoints (a route matched against `/api/…` strings in the frontend; strings built at run time are missed), N+1 (a loop that awaits a database call), unused methods (a name seen once in the whole code base). Nothing was run against a database and no tests were run, as section 0.5 of `Task_Details.md` requires. Savings are estimates until a change is made.
