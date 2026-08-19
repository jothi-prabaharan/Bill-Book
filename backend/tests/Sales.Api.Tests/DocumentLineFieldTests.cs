using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// What the save path actually puts on a line, checked against what the table
/// requires.
///
/// <b>Three of the five sales documents build their lines from five fields</b> —
/// item, quantity, unit price, discount percent and tax group — where the quote
/// and the sales order set the full surface. The columns they leave alone are not
/// all optional, and one of them is load-bearing: <c>BaseQuantity</c> has a check
/// constraint tying it to <c>Quantity × ConversionFactor</c>, so a line that
/// never sets it is refused by the database rather than merely incomplete.
///
/// These tests pin that difference so it is a failing build rather than a 500 on
/// the first save.
/// </summary>
public sealed class DocumentLineFieldTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public DocumentLineFieldTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task A_line_that_never_sets_BaseQuantity_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        // Exactly the fields InvoiceService.SaveAsync sets, and no others.
        db.InvoiceDetails.Add(new InvoiceDetail
        {
            InvoiceId = invoice.InvoiceId,
            LineNumber = 1,
            ItemId = 1,
            Quantity = 10m,
            UnitPrice = 100m,
            DiscountPercent = 0m,
            DiscountAmount = 0m,
        });

        // BaseQuantity defaults to 0 while ConversionFactor defaults to 1, so
        // the constraint reads 0 = round(10 × 1, 6) and refuses. Every invoice,
        // delivery challan and credit note line goes in this way.
        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(CancellationToken.None));

        Assert.Contains(
            "chk_invoicedetails_base_quantity", error.InnerException?.Message ?? string.Empty);
    }

    [SkippableFact]
    public async Task The_same_line_saves_once_BaseQuantity_is_set()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        db.InvoiceDetails.Add(new InvoiceDetail
        {
            InvoiceId = invoice.InvoiceId,
            LineNumber = 1,
            ItemId = 1,
            Quantity = 10m,
            ConversionFactor = 1m,
            BaseQuantity = 10m,
            UnitPrice = 100m,
            DiscountPercent = 0m,
            DiscountAmount = 0m,
        });

        // The one line the three services are missing. Nothing else about the
        // line changed.
        await db.SaveChangesAsync(CancellationToken.None);
        Assert.Equal(1, await db.InvoiceDetails.CountAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task A_second_line_that_never_sets_LineNumber_collides_with_the_first()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        // LineNumber is never assigned by InvoiceService, DeliveryChallanService
        // or CreditNoteService — it appears nowhere in any of the three — so
        // every line on a document takes the default of zero. BaseQuantity is
        // set here so this test fails on the line number and nothing else.
        for (int i = 0; i < 2; i++)
        {
            db.InvoiceDetails.Add(new InvoiceDetail
            {
                InvoiceId = invoice.InvoiceId,
                ItemId = 1,
                Quantity = 1m,
                ConversionFactor = 1m,
                BaseQuantity = 1m,
                UnitPrice = 100m,
            });
        }

        // IX_InvoiceDetails_Line is unique on (InvoiceId, LineNumber), and the
        // ledger's ITEM leg keys on the line number — two lines claiming
        // position zero would leave a posting pointing at whichever the database
        // returned first, which is why the index exists.
        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task An_unset_treatment_and_line_type_fall_back_to_taxable_stock()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        var line = new InvoiceDetail
        {
            InvoiceId = invoice.InvoiceId,
            LineNumber = 1,
            ItemId = 1,
            Quantity = 1m,
            ConversionFactor = 1m,
            BaseQuantity = 1m,
            UnitPrice = 100m,
        };

        db.InvoiceDetails.Add(line);
        await db.SaveChangesAsync(CancellationToken.None);

        // Neither is sent by SaveInvoiceLineRequest, so every invoice line is a
        // taxable stock line whatever the item actually is. An exempt supply, a
        // zero-rated export and a service charge are all unreachable from the
        // invoice API as it stands — which is a functional gap rather than a
        // defect in this row.
        Assert.Equal(TaxTreatment.Taxable, line.TaxTreatment);
        Assert.Equal(DocumentLineType.Stock, line.LineType);
    }

    private static Invoice NewInvoice() => new()
    {
        TransactionTypeCode = "INV",
        DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
        DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };
}
