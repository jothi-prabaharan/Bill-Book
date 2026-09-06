using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The column a document uses to name its print template.
///
/// It sits on <c>DocumentHeaderBase</c>, so every sales and purchase document
/// carries it and a tenth cannot be added without one. It is an unenforced id,
/// not a foreign key: templates live in Master's con schema and these rows live
/// in sal, and there is no cross-schema foreign key anywhere in this product.
/// The consequence worth asserting is that nothing stops a document from
/// outliving the template it names — which is exactly what keeps it printable.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintTemplateColumnTests
{
    private readonly PostgresFixture _postgres;

    public PrintTemplateColumnTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task A_document_stores_and_returns_the_template_it_names()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        long invoiceId;

        await using (SalesDbContext db = _postgres.CreateContext(customer, org))
        {
            Invoice invoice = NewInvoice();
            invoice.PrintTemplateId = 4242;
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.InvoiceId;
        }

        await using SalesDbContext fresh = _postgres.CreateContext(customer, org);
        Invoice stored = await fresh.Invoices.SingleAsync(i => i.InvoiceId == invoiceId, CancellationToken.None);

        Assert.Equal(4242, stored.PrintTemplateId);
    }

    [SkippableFact]
    public async Task A_document_naming_a_template_that_does_not_exist_still_saves()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        invoice.PrintTemplateId = long.MaxValue;
        db.Invoices.Add(invoice);

        // No foreign key, deliberately. A template can be soft-deleted after a
        // document named it, and the document has to stay printable — it falls
        // through to the branch default at print time.
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.NotEqual(0, invoice.InvoiceId);
    }

    [SkippableFact]
    public async Task Naming_no_template_is_the_ordinary_case()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Invoice invoice = NewInvoice();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        // Null means "whatever the branch's default is", which is what almost
        // every document says.
        Assert.Null(invoice.PrintTemplateId);
    }

    [SkippableFact]
    public async Task Every_sales_document_carries_the_column()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> missing = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(DocumentHeaderBase).IsAssignableFrom(e.ClrType))
            .Where(e => e.FindProperty(nameof(DocumentHeaderBase.PrintTemplateId)) is null)
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        // Asserted over the model rather than over a list somebody maintains,
        // so a document type added later is covered without anyone remembering.
        Assert.Empty(missing);
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
