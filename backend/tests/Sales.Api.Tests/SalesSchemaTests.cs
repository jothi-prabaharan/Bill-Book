using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sales.Repository;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The shape of the model, asserted over the whole of it.
///
/// <b>Both of the bugs this covers were invisible for the same reason</b>: the
/// <c>sal</c> tables were written and then not queried or inserted into for
/// weeks, and a table nobody uses is a table nobody has checked. One of them —
/// ten shadow foreign keys, from ten collection navigations that were never
/// bound — meant no sales document could be saved at all.
///
/// Asserted over <c>db.Model</c> rather than by saving one document, so a
/// sixteenth table added tomorrow with the same mistake fails here rather than
/// in whichever service reaches it first.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesSchemaTests
{
    private readonly PostgresFixture _postgres;

    public SalesSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public void No_relationship_in_the_schema_carries_a_shadow_foreign_key()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        // A shadow property on a foreign key is EF saying "I mapped this
        // navigation myself because you did not tell me which column it uses".
        // On a header-to-line relationship that is always a mistake: the column
        // is right there, declared and NOT NULL.
        List<string> shadowed = [.. db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()
                .SelectMany(fk => fk.Properties)
                .Where(p => p.IsShadowProperty())
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .OrderBy(name => name)];

        Assert.Empty(shadowed);
    }

    [SkippableFact]
    public void Every_document_line_collection_is_bound_to_its_headers_foreign_key()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        // The five headers and the five line tables beneath them. Named rather
        // than discovered, because "every collection navigation" would also
        // catch relationships that are meant to have none.
        (string Parent, string Child, string ForeignKey)[] expected =
        [
            ("Quote", "Lines", "QuoteId"),
            ("QuoteDetail", "Taxes", "QuoteDetailId"),
            ("SalesOrder", "Lines", "SalesOrderId"),
            ("SalesOrderDetail", "Taxes", "SalesOrderDetailId"),
            ("DeliveryChallan", "Lines", "DeliveryChallanId"),
            ("DeliveryChallanDetail", "Taxes", "DeliveryChallanDetailId"),
            ("Invoice", "Lines", "InvoiceId"),
            ("InvoiceDetail", "Taxes", "InvoiceDetailId"),
            ("CreditNote", "Lines", "CreditNoteId"),
            ("CreditNoteDetail", "Taxes", "CreditNoteDetailId"),
        ];

        List<string> unbound = [];

        foreach ((string parent, string child, string foreignKey) in expected)
        {
            IEntityType? entity = db.Model.GetEntityTypes()
                .FirstOrDefault(e => e.ClrType.Name == parent);

            INavigation? navigation = entity?.FindNavigation(child);

            if (navigation is null
                || navigation.ForeignKey.Properties.Count != 1
                || navigation.ForeignKey.Properties[0].Name != foreignKey
                || navigation.ForeignKey.Properties[0].IsShadowProperty())
            {
                unbound.Add($"{parent}.{child}");
            }
        }

        Assert.Empty(unbound);
    }
}
