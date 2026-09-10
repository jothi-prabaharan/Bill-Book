using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Undoing changes that have not been saved, and the trap of assuming a
/// database rollback did it for you.
///
/// A transaction rollback undoes the <b>database</b>. It does nothing at all to
/// the DbContext, which still tracks every entity with the new values on it and
/// still holds the identity values Postgres handed out for rows that no longer
/// exist. Reuse that context and it will happily write the stale objects back.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DirtyRecordRollbackTests
{
    private readonly PostgresFixture _pg;

    public DirtyRecordRollbackTests(PostgresFixture pg) => _pg = pg;

    /// <summary>Revert one edited entity to the values it was loaded with.</summary>
    [SkippableFact]
    public async Task Setting_current_values_back_to_original_undoes_one_record()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using SalesDbContext db = _pg.CreateContext(customerId, orgId);

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.Notes = "edited";
        invoice.TotalAmount = 999m;

        var entry = db.Entry(invoice);
        Assert.Equal(EntityState.Modified, entry.State);

        entry.CurrentValues.SetValues(entry.OriginalValues);
        entry.State = EntityState.Unchanged;

        Assert.Null(invoice.Notes);
        Assert.Equal(0m, invoice.TotalAmount);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    /// <summary>Throw away every pending change in one call.</summary>
    [SkippableFact]
    public async Task Clearing_the_change_tracker_drops_every_pending_change()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using SalesDbContext db = _pg.CreateContext(customerId, orgId);

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.Notes = "edited";
        db.Invoices.Add(NewInvoice());

        Assert.True(db.ChangeTracker.HasChanges());

        db.ChangeTracker.Clear();

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(db.ChangeTracker.Entries());

        // The object itself keeps the edit — it is simply no longer tracked, so
        // nothing will write it.
        Assert.Equal("edited", invoice.Notes);

        Invoice fresh = await db.Invoices.SingleAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Null(fresh.Notes);
    }

    /// <summary>Re-read one entity from the database, discarding the edit.</summary>
    [SkippableFact]
    public async Task Reload_replaces_the_edit_with_what_the_database_holds()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using SalesDbContext db = _pg.CreateContext(customerId, orgId);

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.Notes = "edited";

        await db.Entry(invoice).ReloadAsync();

        Assert.Null(invoice.Notes);
        Assert.Equal(EntityState.Unchanged, db.Entry(invoice).State);
    }

    /// <summary>
    /// The trap. A rollback undoes the database and leaves the context dirty.
    /// </summary>
    [SkippableFact]
    public async Task A_rollback_does_not_clean_the_change_tracker()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using SalesDbContext db = _pg.CreateContext(customerId, orgId);

        Invoice invoice = NewInvoice();

        await using (ITransactionScope tx = await db.Database.BeginScopeAsync(CancellationToken.None))
        {
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            await tx.RollbackAsync(CancellationToken.None);
        }

        // The row is gone from the database...
        await using SalesDbContext reader = _pg.CreateContext(customerId, orgId);
        Assert.Null(await reader.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId));

        // ...but the context still tracks it as a saved, unchanged row, and the
        // object still carries the identity value Postgres gave a row that no
        // longer exists. Anything reusing this context is working from a lie.
        Assert.True(invoice.InvoiceId > 0);
        Assert.Equal(EntityState.Unchanged, db.Entry(invoice).State);

        // Which is why ErrorLogStore clears the tracker before it writes.
        db.ChangeTracker.Clear();
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static Invoice NewInvoice() => new()
    {
        TransactionTypeCode = "INV",
        DocumentNo = $"INV{Guid.NewGuid():N}"[..20],
        DocumentDate = new DateOnly(2026, 4, 1),
        DueDate = new DateOnly(2026, 4, 30),
        ContactId = 1,
        PlaceOfSupplyStateId = 33,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };
}
