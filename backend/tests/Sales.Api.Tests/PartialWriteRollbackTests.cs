using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// What happens to the header when the lines fail.
///
/// <c>InvoiceService.CreateAsync</c> writes an invoice across three tables and
/// calls <c>SaveChangesAsync</c> between each — the header, then every line, then
/// every line's taxes. A five-line invoice is <b>eleven</b> separate saves.
///
/// EF wraps each of those in its own implicit transaction, so before the
/// request-wide filter existed each one committed the moment it returned. A
/// failure on the second table left the header on disk: an invoice with some of
/// its lines, totals that agree with lines that are not there, and nothing to
/// say it is wrong. Nobody would find it until a GST return did not tie.
///
/// The first test reproduces exactly that, so what the filter prevents is on the
/// record rather than described. The second shows the same sequence inside the
/// transaction the filter opens.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PartialWriteRollbackTests
{
    private readonly PostgresFixture _postgres;

    public PartialWriteRollbackTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Without_a_transaction_the_header_survives_a_failure_on_the_lines()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using SalesDbContext db = _postgres.CreateContext(customerId, orgId);

        Invoice invoice = NewInvoice("INV-NOTX");

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        // Committed already, on its own, exactly as CreateAsync's first save was.
        Assert.True(invoice.InvoiceId > 0);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.InvoiceDetails.Add(NewDetail(invoice.InvoiceId + 9_000_000, customerId, orgId));
            await db.SaveChangesAsync();
        });

        db.ChangeTracker.Clear();

        await using SalesDbContext reader = _postgres.CreateContext(customerId, orgId);

        // The orphan. This is the bug, not the fix.
        Assert.NotNull(await reader.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId));

        Assert.Empty(await reader.InvoiceDetails
            .Where(d => d.InvoiceId == invoice.InvoiceId)
            .ToListAsync());
    }

    [SkippableFact]
    public async Task Inside_the_requests_transaction_the_header_goes_back_too()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using SalesDbContext db = _postgres.CreateContext(customerId, orgId);

        long invoiceId;

        // What TransactionFilter opens before the controller action runs. Every
        // SaveChanges below joins it without being told to — EF uses
        // Database.CurrentTransaction whenever one is open, which is why the
        // service code did not have to change for this to work.
        await using (ITransactionScope tx = await db.Database.BeginScopeAsync(CancellationToken.None))
        {
            Invoice invoice = NewInvoice("INV-TX");

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            invoiceId = invoice.InvoiceId;
            Assert.True(invoiceId > 0);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                db.InvoiceDetails.Add(NewDetail(invoiceId + 9_000_000, customerId, orgId));
                await db.SaveChangesAsync();
            });

            // The filter's rollback, on the exception leaving the action.
            await tx.RollbackAsync(CancellationToken.None);
        }

        db.ChangeTracker.Clear();

        await using SalesDbContext reader = _postgres.CreateContext(customerId, orgId);

        // Gone. The sequence number the header took is spent — Postgres does not
        // give identity values back — but no row exists, which is the promise.
        Assert.Null(await reader.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId));
    }

    [SkippableFact]
    public async Task Disposing_without_committing_rolls_back_as_well()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using SalesDbContext db = _postgres.CreateContext(customerId, orgId);

        long invoiceId;

        // The early-return path: a service that writes, then finds a reason to
        // refuse and returns an outcome rather than throwing. Nothing calls
        // rollback, and the header must still not survive.
        await using (await db.Database.BeginScopeAsync(CancellationToken.None))
        {
            Invoice invoice = NewInvoice("INV-DISPOSE");

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            invoiceId = invoice.InvoiceId;
        }

        db.ChangeTracker.Clear();

        await using SalesDbContext reader = _postgres.CreateContext(customerId, orgId);

        Assert.Null(await reader.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId));
    }

    private static Invoice NewInvoice(string documentNo) => new()
    {
        TransactionTypeCode = "INV",
        DocumentNo = $"{documentNo}-{Guid.NewGuid():N}"[..20],
        DocumentDate = new DateOnly(2026, 4, 1),
        // chk_invoices_due_date: an INV must carry one. A POS sale is paid at
        // the counter and does not.
        DueDate = new DateOnly(2026, 4, 30),
        ContactId = 1,
        PlaceOfSupplyStateId = 33,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };

    /// <summary>
    /// A line naming an invoice that does not exist, so Postgres refuses it with
    /// a foreign-key violation — 23503. A real failure from the database rather
    /// than an exception thrown by the test, because what is being checked is
    /// what the database does with the header when the second write is rejected.
    /// </summary>
    private static InvoiceDetail NewDetail(long invoiceId, Guid customerId, Guid orgId) => new()
    {
        InvoiceId = invoiceId,
        CustomerId = customerId,
        OrgId = orgId,
        LineNumber = 1,
        LineType = DocumentLineType.Stock,
        Quantity = 1m,
        UnitPrice = 100m,
    };
}
