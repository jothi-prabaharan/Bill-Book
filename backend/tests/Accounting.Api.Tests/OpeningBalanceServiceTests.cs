using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The opening balance, against a real database — because half of what is being
/// proved lives in the schema: the one-per-branch unique index, the check
/// constraints that say what each kind of line may name, and the deferred balance
/// trigger the posting has to satisfy.
///
/// The through-line of every test here is the same: an opening balance that is
/// <i>wrong</i> balances just as well as one that is right. Nothing downstream
/// catches it, because everything downstream is measured from it.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class OpeningBalanceServiceTests
{
    private const int Asset = 1;
    private const int Liability = 2;
    private const int Equity = 3;

    private readonly PostgresFixture _postgres;

    public OpeningBalanceServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// A migration that ties: cash and a customer's unpaid invoice on one side,
    /// capital on the other. Opening Balance Equity nets to zero, and that is the
    /// whole validation.
    /// </summary>
    [SkippableFact]
    public async Task A_balanced_migration_finalizes_and_posts_every_line_against_equity()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Contact(7);

        Assert.Equal(
            OpeningBalanceOutcome.Ok,
            (await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
            {
                AsOfDate = new DateOnly(2026, 4, 1),
                Lines =
                [
                    h.Gl(h.CashId, debit: 60_000m),
                    Receivable(7, 40_000m, "INV-2025-0912", new DateOnly(2026, 1, 15)),
                    h.Gl(h.CapitalId, credit: 100_000m),
                ],
            }, ct)).Outcome);

        OpeningBalanceReadinessView readiness = (await h.Openings.ReadinessAsync(ct))!;

        Assert.Equal(0m, readiness.OpeningBalanceEquity);
        Assert.Empty(readiness.Blockers);
        Assert.True(readiness.CanFinalize);

        Assert.Equal(
            OpeningBalanceOutcome.Ok, (await h.Openings.FinalizeAsync(ct)).Outcome);

        OpeningBalanceView after = (await h.Openings.GetAsync(ct))!;
        Assert.Equal("Finalized", after.Status);
        Assert.NotNull(after.TransactionNo);

        List<JournalLedger> rows = await h.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "OPB")
            .ToListAsync(ct);

        // Six rows, not four: every line is its own balanced pair against equity,
        // so a row on the equity side traces back to the line that produced it.
        Assert.Equal(6, rows.Count);
        Assert.Equal(rows.Sum(r => r.DebitAmountBase), rows.Sum(r => r.CreditAmountBase));

        // Opening Balance Equity nets to nothing, which is what "it ties" means.
        List<JournalLedger> equity = [.. rows.Where(r => r.AccountId == h.EquityId)];
        Assert.Equal(
            equity.Sum(r => r.DebitAmountBase), equity.Sum(r => r.CreditAmountBase));

        // The receivable landed on the contact's own balance, not on the bare
        // control account — which is the difference between a statement that is
        // right and one that says the customer owes nothing.
        JournalLedger receivable = Assert.Single(
            rows, r => r.AccountId == h.ReceivableId && r.DebitAmount > 0);

        Assert.Equal(h.ReceivableSubAccountId, receivable.SubAccountId);
        Assert.Equal(40_000m, receivable.DebitAmount);
        Assert.Contains("INV-2025-0912", receivable.TransactionDesc);
    }

    /// <summary>
    /// The validation that matters. A migration missing its capital line balances
    /// perfectly in the ledger — every line is its own pair — and leaves the
    /// entire position sitting in Opening Balance Equity, which is an account that
    /// means "not accounted for yet". Finalizing it would invent equity out of
    /// something somebody forgot to key.
    /// </summary>
    [SkippableFact]
    public async Task A_migration_that_leaves_equity_holding_something_cannot_finalize()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines = [h.Gl(h.CashId, debit: 60_000m)],
        }, ct);

        OpeningBalanceReadinessView readiness = (await h.Openings.ReadinessAsync(ct))!;

        Assert.Equal(60_000m, readiness.OpeningBalanceEquity);
        Assert.False(readiness.CanFinalize);
        Assert.Contains(readiness.Blockers, b => b.Contains("Opening Balance Equity"));

        OpeningBalanceResult refused = await h.Openings.FinalizeAsync(ct);

        Assert.Equal(OpeningBalanceOutcome.NotReady, refused.Outcome);
        Assert.Equal("Draft", (await h.Openings.GetAsync(ct))!.Status);
        Assert.Empty(await h.Db.JournalLedger.Where(l => l.TransactionTypeCode == "OPB").ToListAsync(ct));
    }

    /// <summary>
    /// Stock counts toward the equity plug even though Accounting never posts it.
    /// Inventory posts <c>Dr Inventory / Cr Opening Balance Equity</c> itself, so
    /// an item line is a credit OBE has not seen yet — and a migration whose only
    /// asset is stock ties against a capital line just like any other.
    /// </summary>
    [SkippableFact]
    public async Task Stock_ties_against_capital_without_accounting_posting_its_value()
    {
        var inventory = new RecordingInventory();
        await using Harness h = await Harness.CreateAsync(_postgres, inventory);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines =
            [
                Item(itemId: 11, quantity: 100m, unitCost: 250m),
                h.Gl(h.CapitalId, credit: 25_000m),
            ],
        }, ct);

        OpeningBalanceReadinessView readiness = (await h.Openings.ReadinessAsync(ct))!;

        Assert.Equal(25_000m, readiness.InventoryValue);
        Assert.Equal(0m, readiness.OpeningBalanceEquity);
        Assert.True(readiness.CanFinalize);

        Assert.Equal(OpeningBalanceOutcome.Ok, (await h.Openings.FinalizeAsync(ct)).Outcome);

        // Inventory was handed the quantity and the cost, keyed on the document
        // and line so a retry lands on the same row rather than doubling stock.
        OpeningStockRequestLine sent = Assert.Single(inventory.Lines);
        Assert.Equal(11, sent.ItemId);
        Assert.Equal(100m, sent.Quantity);
        Assert.Equal(250m, sent.UnitCost);
        Assert.Equal(1, sent.LineNumber);

        // And Accounting posted only the capital line's pair — nothing against
        // the Inventory account, which is Inventory's to write.
        List<JournalLedger> rows = await h.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "OPB")
            .ToListAsync(ct);

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.AccountId == h.InventoryId);
    }

    /// <summary>
    /// A receivable for a contact with no sub-account is refused before anything
    /// posts. It would otherwise land on the bare control account, balance
    /// perfectly, and leave the customer owing nothing — so the statement, the
    /// aging report and the first receipt against it are all wrong while the trial
    /// balance says everything is fine.
    /// </summary>
    [SkippableFact]
    public async Task A_receivable_for_an_unprovisioned_contact_blocks_the_finalize()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines =
            [
                Receivable(99, 40_000m, "INV-1", new DateOnly(2026, 1, 15)),
                h.Gl(h.CapitalId, credit: 40_000m),
            ],
        }, ct);

        OpeningBalanceReadinessView readiness = (await h.Openings.ReadinessAsync(ct))!;

        Assert.Equal(0m, readiness.OpeningBalanceEquity);
        Assert.False(readiness.CanFinalize);
        Assert.Contains(readiness.Blockers, b => b.Contains("99"));

        Assert.Equal(
            OpeningBalanceOutcome.NotReady, (await h.Openings.FinalizeAsync(ct)).Outcome);
    }

    /// <summary>
    /// Inventory refusing a line stops the whole thing, and nothing posts. A
    /// migration that opened its books with two thirds of its stock is worse than
    /// one that did not open at all, because only the second is obvious.
    /// </summary>
    [SkippableFact]
    public async Task Inventory_refusing_the_stock_leaves_the_books_closed()
    {
        var inventory = new RecordingInventory { Refuse = "line 1 (item 11): ItemNotFound" };
        await using Harness h = await Harness.CreateAsync(_postgres, inventory);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines =
            [
                Item(itemId: 11, quantity: 100m, unitCost: 250m),
                h.Gl(h.CapitalId, credit: 25_000m),
            ],
        }, ct);

        OpeningBalanceResult refused = await h.Openings.FinalizeAsync(ct);

        Assert.Equal(OpeningBalanceOutcome.InventoryUnavailable, refused.Outcome);
        Assert.Contains("ItemNotFound", refused.Detail);
        Assert.Equal("Draft", (await h.Openings.GetAsync(ct))!.Status);
        Assert.Empty(await h.Db.JournalLedger.Where(l => l.TransactionTypeCode == "OPB").ToListAsync(ct));
    }

    /// <summary>
    /// Inventory reporting a value that disagrees with the document stops the
    /// finalize even though every line came back recorded. This is the tie check
    /// T8.2 asks for on the one subledger Accounting cannot compute for itself.
    /// </summary>
    [SkippableFact]
    public async Task Stock_recorded_at_a_different_value_than_the_document_says_stops_go_live()
    {
        var inventory = new RecordingInventory { ValueOverride = 24_000m };
        await using Harness h = await Harness.CreateAsync(_postgres, inventory);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines =
            [
                Item(itemId: 11, quantity: 100m, unitCost: 250m),
                h.Gl(h.CapitalId, credit: 25_000m),
            ],
        }, ct);

        OpeningBalanceResult refused = await h.Openings.FinalizeAsync(ct);

        Assert.Equal(OpeningBalanceOutcome.NotReady, refused.Outcome);
        Assert.Contains("24000.00", refused.Detail);
        Assert.Equal("Draft", (await h.Openings.GetAsync(ct))!.Status);
    }

    /// <summary>
    /// Read-only after go-live, and permanently. There is no reopen and no
    /// reverse: an opening balance is the ground every balance since stands on,
    /// so an error found afterwards is corrected with a journal entry that leaves
    /// a trail.
    /// </summary>
    [SkippableFact]
    public async Task A_finalized_opening_balance_can_never_be_edited_or_deleted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines = [h.Gl(h.CashId, debit: 1_000m), h.Gl(h.CapitalId, credit: 1_000m)],
        }, ct);

        await h.Openings.FinalizeAsync(ct);

        Assert.Equal(
            OpeningBalanceOutcome.AlreadyFinalized,
            (await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
            {
                AsOfDate = new DateOnly(2026, 4, 1),
                Lines = [h.Gl(h.CashId, debit: 9_000m), h.Gl(h.CapitalId, credit: 9_000m)],
            }, ct)).Outcome);

        Assert.Equal(
            OpeningBalanceOutcome.AlreadyFinalized, (await h.Openings.DeleteAsync(ct)).Outcome);

        Assert.Equal(
            OpeningBalanceOutcome.AlreadyFinalized, (await h.Openings.FinalizeAsync(ct)).Outcome);

        // And the figures are untouched by any of it.
        OpeningBalanceView after = (await h.Openings.GetAsync(ct))!;
        Assert.Equal(1_000m, after.Lines[0].DebitAmount);
    }

    /// <summary>
    /// Saving twice replaces the draft rather than making a second document. A
    /// branch has one moment it started, and the unique index says so — but a save
    /// path that relied on the index to catch it would surface as a constraint
    /// violation rather than as a replaced draft.
    /// </summary>
    [SkippableFact]
    public async Task Saving_again_replaces_the_draft_rather_than_opening_a_second_one()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        OpeningBalanceResult first = await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines = [h.Gl(h.CashId, debit: 1_000m), h.Gl(h.CapitalId, credit: 1_000m)],
        }, ct);

        OpeningBalanceResult second = await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
        {
            AsOfDate = new DateOnly(2026, 4, 1),
            Lines = [h.Gl(h.CashId, debit: 5_000m), h.Gl(h.CapitalId, credit: 5_000m)],
        }, ct);

        Assert.Equal(first.OpeningBalanceId, second.OpeningBalanceId);
        Assert.Equal(1, await h.Db.OpeningBalances.CountAsync(ct));

        OpeningBalanceView after = (await h.Openings.GetAsync(ct))!;
        Assert.Equal(2, after.Lines.Count);
        Assert.Equal(5_000m, after.Lines[0].DebitAmount);
    }

    /// <summary>
    /// A line has to name the thing its kind is about, and nothing else. A
    /// receivable free to choose its own account is a receivable that can be filed
    /// under Sales Revenue.
    /// </summary>
    [SkippableFact]
    public async Task A_line_naming_the_wrong_thing_for_its_kind_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        Assert.Equal(
            OpeningBalanceOutcome.LineShapeInvalid,
            (await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
            {
                AsOfDate = new DateOnly(2026, 4, 1),
                Lines =
                [
                    new SaveOpeningBalanceLineRequest
                    {
                        LineType = OpeningBalanceLineType.ContactReceivable,
                        ContactId = 7,
                        AccountId = h.CashId,
                        DebitAmount = 100m,
                    },
                ],
            }, ct)).Outcome);

        // An item line carrying a hand-keyed amount as well as a cost: two figures
        // for one value, and only one of them is what the stock is worth.
        Assert.Equal(
            OpeningBalanceOutcome.LineShapeInvalid,
            (await h.Openings.SaveAsync(new SaveOpeningBalanceRequest
            {
                AsOfDate = new DateOnly(2026, 4, 1),
                Lines =
                [
                    new SaveOpeningBalanceLineRequest
                    {
                        LineType = OpeningBalanceLineType.Item,
                        ItemId = 11,
                        Quantity = 10m,
                        UnitCost = 5m,
                        DebitAmount = 50m,
                    },
                ],
            }, ct)).Outcome);
    }

    private static SaveOpeningBalanceLineRequest Receivable(
        long contactId, decimal amount, string reference, DateOnly documentDate) => new()
        {
            LineType = OpeningBalanceLineType.ContactReceivable,
            ContactId = contactId,
            DebitAmount = amount,
            DocumentReference = reference,
            DocumentDate = documentDate,
        };

    private static SaveOpeningBalanceLineRequest Item(
        long itemId, decimal quantity, decimal unitCost) => new()
        {
            LineType = OpeningBalanceLineType.Item,
            ItemId = itemId,
            Quantity = quantity,
            UnitCost = unitCost,
        };

    /// <summary>
    /// Stands in for Inventory and keeps what it was asked to record. Recorded
    /// rather than mocked away, because what these tests are about is what
    /// Accounting <i>hands over</i> — Inventory's own suite covers what it does
    /// with it.
    /// </summary>
    private sealed class RecordingInventory : IInventoryOpeningStock
    {
        public List<OpeningStockRequestLine> Lines { get; } = [];

        /// <summary>Set to make Inventory refuse, with this as the reason.</summary>
        public string? Refuse { get; set; }

        /// <summary>Set to answer a value other than the one the lines come to.</summary>
        public decimal? ValueOverride { get; set; }

        public Task<OpeningStockOutcome> RecordAsync(
            long openingBalanceId,
            DateOnly asOfDate,
            IReadOnlyList<OpeningStockRequestLine> lines,
            CancellationToken ct)
        {
            if (Refuse is not null)
            {
                return Task.FromResult(new OpeningStockOutcome(false, 0m, Refuse));
            }

            Lines.AddRange(lines);

            return Task.FromResult(new OpeningStockOutcome(
                true, ValueOverride ?? lines.Sum(l => l.Quantity * l.UnitCost)));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required OpeningBalanceService Openings { get; init; }

        public required Guid OrgId { get; init; }

        public required long CashId { get; init; }

        public required long CapitalId { get; init; }

        public required long EquityId { get; init; }

        public required long ReceivableId { get; init; }

        public required long PayableId { get; init; }

        public required long InventoryId { get; init; }

        public long ReceivableSubAccountId { get; private set; }

        public SaveOpeningBalanceLineRequest Gl(
            long accountId, decimal debit = 0m, decimal credit = 0m) => new()
            {
                LineType = OpeningBalanceLineType.GlAccount,
                AccountId = accountId,
                DebitAmount = debit,
                CreditAmount = credit,
            };

        /// <summary>Provisions a contact's trade balances, the way Contacts would.</summary>
        public async Task Contact(long contactId)
        {
            foreach (long accountId in new[] { ReceivableId, PayableId })
            {
                var sub = new SubAccount
                {
                    OrgId = OrgId,
                    AccountId = accountId,
                    AccountTypeId = accountId == ReceivableId ? Asset : Liability,
                    ReferenceType = SubAccountReferenceType.Contact,
                    ReferenceId = contactId,
                    Purpose = SubAccountPurpose.Primary,
                    TaxComponent = TaxComponent.None,
                    SubAccountName = $"Contact {contactId}",
                    IsActive = true,
                };

                Db.SubAccounts.Add(sub);
                await Db.SaveChangesAsync();

                if (accountId == ReceivableId)
                {
                    ReceivableSubAccountId = sub.SubAccountId;
                }
            }
        }

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, IInventoryOpeningStock? inventory = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(
                tenant.CustomerId!.Value, tenant.OrgId!.Value);

            async Task<long> Account(string code, SystemAccount? system, string name, int typeId)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    AccountSystemName = system is { } s ? SystemAccountNames.Of(s) : null,
                    IsSystemDefault = system is not null,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            db.NumberingSeries.AddRange(
                Repository.SeedData.NumberingSeriesSeed.Build(orgId)
                    .Where(s => s.SeriesCode == "OPB"));
            await db.SaveChangesAsync();

            return new Harness
            {
                Db = db,
                OrgId = orgId,
                CashId = await Account("1010", null, "Cash", Asset),
                CapitalId = await Account("3010", null, "Capital", Equity),
                EquityId = await Account(
                    "3900", SystemAccount.OpeningBalanceEquity, "Opening Balance Equity", Equity),
                ReceivableId = await Account(
                    "1100", SystemAccount.AccountsReceivable, "Accounts Receivable", Asset),
                PayableId = await Account(
                    "2100", SystemAccount.AccountsPayable, "Accounts Payable", Liability),
                InventoryId = await Account("1200", SystemAccount.Inventory, "Inventory", Asset),
                Openings = new OpeningBalanceService(
                    db,
                    new LedgerPostingService(db, tenant, new StubBaseCurrency()),
                    inventory ?? new RecordingInventory(),
                    new NumberGenerator(
                        db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                    new StubBaseCurrency(),
                    tenant,
                    new StubCurrentUser(),
                    TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
