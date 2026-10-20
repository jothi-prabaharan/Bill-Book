using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sales.Entity.Models;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Audit;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The edit log against a real <c>sal</c> schema (TK-118): an edited invoice
/// records each changed field's old and new value under the invoice, a save
/// that rolls back leaves no log behind, and the table refuses to be changed —
/// by the application, and by a superuser, because the refusal is a trigger
/// rather than a grant.
///
/// Written for TK-118 and not run by the AI that wrote it (docs/Task_Details.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EditLogTests
{
    private const long ItemId = 101;
    private const long ContactId = 42;

    private readonly PostgresFixture _pg;

    public EditLogTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task An_edited_invoice_logs_the_old_and_new_value_under_the_invoice()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(Request(new DateOnly(2026, 7, 1)), CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        InvoiceResult edited = await h.Invoices.UpdateAsync(created.InvoiceId, Request(new DateOnly(2026, 7, 15)), CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, edited.Outcome);

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);
        List<EditLogEntryView> history = await EditLogReader.HistoryAsync(
            reader, "Invoices", created.InvoiceId.ToString(), CancellationToken.None);

        // The header's insert, and the due date's change with both sides.
        Assert.Contains(history, e => e.EntityName == "Invoices" && e.Operation == nameof(EditOperation.Insert));

        EditLogEntryView change = Assert.Single(history, e =>
            e.EntityName == "Invoices"
            && e.Operation == nameof(EditOperation.Update)
            && e.Changes.TryGetProperty("DueDate", out _));
        JsonElement dueDate = change.Changes.GetProperty("DueDate");
        Assert.Equal("2026-07-01", dueDate.GetProperty("old").GetString());
        Assert.Equal("2026-07-15", dueDate.GetProperty("new").GetString());

        // Its lines are filed under the invoice too, so the History tab shows them.
        Assert.Contains(history, e => e.EntityName == "InvoiceDetails" && e.RootEntityKey == created.InvoiceId.ToString());
    }

    [SkippableFact]
    public async Task A_save_that_rolls_back_leaves_no_log_row()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long invoiceId;
        await using (ITransactionScope tx = await h.Db.Database.BeginScopeAsync(CancellationToken.None))
        {
            InvoiceResult created = await h.Invoices.CreateAsync(Request(new DateOnly(2026, 7, 1)), CancellationToken.None);
            Assert.Equal(InvoiceOutcome.Ok, created.Outcome);
            invoiceId = created.InvoiceId;

            await tx.RollbackAsync(CancellationToken.None);
        }

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);
        Assert.Null(await reader.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId));
        Assert.False(await reader.EditLogs.AnyAsync(e => e.RootEntityName == "Invoices" && e.RootEntityKey == invoiceId.ToString()));
    }

    [SkippableFact]
    public async Task The_log_refuses_update_delete_and_truncate_even_from_a_superuser()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceResult created = await h.Invoices.CreateAsync(Request(new DateOnly(2026, 7, 1)), CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        string key = created.InvoiceId.ToString();

        // Through EF, the way the application would try it.
        await AssertRefusedAsync(() => h.Db.EditLogs
            .Where(e => e.RootEntityKey == key)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserName, "rewritten")));
        await AssertRefusedAsync(() => h.Db.EditLogs
            .Where(e => e.RootEntityKey == key)
            .ExecuteDeleteAsync());

        // And as the suite's superuser directly, which RLS would not stop.
        await using var connection = new NpgsqlConnection(h.Db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using NpgsqlCommand truncate = connection.CreateCommand();
        truncate.CommandText = "TRUNCATE sal.\"EditLog\"";
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);
        Assert.True(await reader.EditLogs.AnyAsync(e => e.RootEntityKey == key));
        Assert.False(await reader.EditLogs.AnyAsync(e => e.UserName == "rewritten"));
    }

    [SkippableFact]
    public async Task A_logged_bulk_update_records_each_row_it_changed()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceResult created = await h.Invoices.CreateAsync(Request(new DateOnly(2026, 7, 1)), CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        int changed = await h.Db.Invoices
            .Where(i => i.InvoiceId == created.InvoiceId)
            .ExecuteUpdateLoggedAsync(h.Db, s => s.SetProperty(i => i.Notes, "Bulk note"), CancellationToken.None);
        Assert.Equal(1, changed);

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);
        EditLogEntryView row = Assert.Single(
            await EditLogReader.HistoryAsync(reader, "Invoices", created.InvoiceId.ToString(), CancellationToken.None),
            e => e.Source == "ExecuteUpdate");
        Assert.Equal("Bulk note", row.Changes.GetProperty("Notes").GetProperty("new").GetString());
    }

    private static async Task AssertRefusedAsync(Func<Task<int>> write)
    {
        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(write);
        PostgresException? postgres = thrown as PostgresException ?? thrown.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgres!.SqlState);
    }

    private static SaveInvoiceRequest Request(DateOnly dueDate) => new()
    {
        DocumentDate = new DateOnly(2026, 6, 1),
        DueDate = dueDate,
        ContactId = ContactId,
        PlaceOfSupplyStateCode = "33",
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Lines =
        [
            new SaveInvoiceLineRequest
            {
                ItemId = ItemId,
                Quantity = 2m,
                ConversionFactor = 1m,
                UnitPrice = 100m,
                TaxGroupId = 1,
                LineType = DocumentLineType.Stock,
                Description = "First",
            },
        ],
    };

    private sealed record Harness(SalesDbContext Db, InvoiceRepository Invoices, TenantContext Tenant, StubCurrentUser User)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            StubCurrentUser user = new();

            InvoiceRepository invoices = new(
                db,
                tenant,
                new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                user,
                TimeProvider.System,
                new RecordingInventory(),
                new RecordingLedger(),
                new StubCreditCheck(),
                new StubDocumentStorage(),
                new StubInvoicePdf(),
                new StubOrgIdentity(),
                new StubUqcLookup(), new StubEInvoicing());

            return new Harness(db, invoices, tenant, user);
        }
    }
}
