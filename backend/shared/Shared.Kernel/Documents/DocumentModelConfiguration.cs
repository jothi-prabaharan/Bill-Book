using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Kernel.Documents;

/// <summary>
/// The Fluent configuration every sales and purchase document table gets, for
/// the columns the three base classes supply.
///
/// <b>Shared for the same reason the base classes are.</b> The columns are
/// inherited rather than copied, so the precision, the indexes and the check
/// constraints over them are too. `sal` has fifteen tables and `pur` has twelve;
/// twenty-seven hand-written blocks would be twenty-six chances to give one
/// table a different scale or drop one constraint, and the table that got it
/// would be the one nobody looked at again.
///
/// It also settles what "purchase follows the same schema as sales" means. Not
/// "somebody copied the file and renamed the types" — the two schemas are
/// generated from this one place, so a constraint corrected here is corrected on
/// both, and a divergence between them has to be written down deliberately in
/// the calling context rather than arrived at by accident.
///
/// Per-table extras — <c>VendorBillNo</c>, <c>AcceptedQuantity</c>, the POS
/// columns — are configured by the context that owns them. Only what the base
/// classes declare is configured here.
/// </summary>
public static class DocumentModelConfiguration
{
    /// <summary>
    /// Everything <see cref="DocumentHeaderBase"/> implies, for one header table.
    ///
    /// <paramref name="codes"/> is the set of transaction type codes the table
    /// may hold — one for every table except <c>sal.Invoices</c>, which holds
    /// <c>INV</c> and <c>POS</c>. It becomes a check constraint, because the
    /// master table is in another database and no foreign key can say it.
    /// </summary>
    public static void ConfigureHeader<T>(
        this EntityTypeBuilder<T> b, string table, params string[] codes)
        where T : DocumentHeaderBase
    {
        // The number exists from creation, so this is a plain unique index
        // rather than a filtered one — there is no such thing as an unnumbered
        // row here.
        b.HasIndex(e => new { e.OrgId, e.DocumentNo }).IsUnique()
            .HasDatabaseName($"IX_{table}_Number");

        b.HasIndex(e => new { e.OrgId, e.DocumentDate })
            .HasDatabaseName($"IX_{table}_Date");

        b.HasIndex(e => new { e.OrgId, e.ContactId, e.DocumentDate })
            .HasDatabaseName($"IX_{table}_Contact");

        b.HasIndex(e => new { e.OrgId, e.Status })
            .HasDatabaseName($"IX_{table}_Status");

        b.Property(e => e.TransactionTypeCode).HasMaxLength(3);
        b.Property(e => e.DocumentNo).HasMaxLength(30);
        b.Property(e => e.ContactGstin).HasMaxLength(15);
        b.Property(e => e.CurrencyCode).HasMaxLength(3);
        b.Property(e => e.VoidReason).HasMaxLength(300);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,8)");

        foreach (string amount in HeaderAmounts)
        {
            b.Property(amount).HasColumnType("decimal(28,2)");
        }

        string codeList = string.Join(", ", codes.Select(c => $"'{c}'"));
        string lower = table.ToLowerInvariant();

        b.ToTable(t =>
        {
            t.HasCheckConstraint($"chk_{lower}_type", $"\"TransactionTypeCode\" IN ({codeList})");

            // Posted at is set if and only if the document ever reached the
            // books. It is also what tells a void draft from a void posting,
            // which is why there is no separate Cancelled status.
            t.HasCheckConstraint(
                $"chk_{lower}_posted_stamp",
                "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");

            t.HasCheckConstraint(
                $"chk_{lower}_posted_requires_stamp",
                "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");

            // Voided together, or not at all. A void with no reason is the row
            // somebody has to reconstruct from memory a year later.
            t.HasCheckConstraint(
                $"chk_{lower}_void_stamp",
                "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) "
                    + "AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");

            t.HasCheckConstraint($"chk_{lower}_rate_positive", "\"ExchangeRate\" > 0");

            // Every amount is a magnitude except the round-off, which is a
            // correction and runs both ways.
            t.HasCheckConstraint(
                $"chk_{lower}_amounts_non_negative",
                "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 "
                    + "AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 "
                    + "AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 "
                    + "AND \"TotalAmountBase\" >= 0");

            // The foot has to add up. Checked in the database because a total
            // that disagrees with its parts still prints and still posts.
            t.HasCheckConstraint(
                $"chk_{lower}_total",
                "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" "
                    + "+ \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");

            // Intra-state is CGST and SGST; inter-state is IGST. A wrong
            // determination still balances, still prints and still posts — the
            // return is where it would otherwise surface, months later.
            t.HasCheckConstraint(
                $"chk_{lower}_tax_split",
                "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) "
                    + "OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
        });
    }

    /// <summary>Everything <see cref="DocumentLineBase"/> implies, for one detail table.</summary>
    public static void ConfigureLine<T>(
        this EntityTypeBuilder<T> b, string table, string parent)
        where T : DocumentLineBase
    {
        // Unique on (document, line number). LineNumber is what the ledger's
        // ITEM leg keys on, so two lines claiming position three would leave one
        // posting pointing at whichever the database returned first. The index
        // also serves the plain lookup by parent, being its prefix.
        b.HasIndex([parent, nameof(DocumentLineBase.LineNumber)])
            .IsUnique()
            .HasDatabaseName($"IX_{table}_Line");

        b.HasIndex(e => new { e.OrgId, e.ItemId }).HasDatabaseName($"IX_{table}_Item");

        b.Property(e => e.HsnSacCode).HasMaxLength(8);
        b.Property(e => e.Description).HasMaxLength(500);
        b.Property(e => e.LineNotes).HasMaxLength(300);
        b.Property(e => e.TaxTreatment).HasConversion<string>().HasMaxLength(10);
        b.Property(e => e.LineType).HasConversion<string>().HasMaxLength(10);

        b.Property(e => e.Quantity).HasColumnType("decimal(18,6)");
        b.Property(e => e.ConversionFactor).HasColumnType("decimal(18,6)");
        b.Property(e => e.BaseQuantity).HasColumnType("decimal(18,6)");
        b.Property(e => e.UnitPrice).HasColumnType("decimal(28,6)");
        b.Property(e => e.DiscountPercent).HasColumnType("decimal(9,6)");

        foreach (string amount in LineAmounts)
        {
            b.Property(amount).HasColumnType("decimal(28,2)");
        }

        string lower = table.ToLowerInvariant();

        b.ToTable(t =>
        {
            t.HasCheckConstraint($"chk_{lower}_quantity", "\"Quantity\" > 0");

            // Rounded to the column's own scale before comparing, and that is
            // not pedantry. Both operands are decimal(18,6), so their product
            // carries twelve decimal places while the column holds six: a
            // quantity of 1.000001 at a factor of 1.5 gives 1.5000015, which
            // stores as 1.500002 and would fail a bare equality against the
            // unrounded product. The rule being expressed is "BaseQuantity is
            // the product", not "the product happens to need no rounding".
            t.HasCheckConstraint(
                $"chk_{lower}_base_quantity",
                "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");

            t.HasCheckConstraint(
                $"chk_{lower}_discount",
                "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");

            t.HasCheckConstraint(
                $"chk_{lower}_total",
                "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");

            // A line describes something, one way or the other.
            t.HasCheckConstraint(
                $"chk_{lower}_describes",
                "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");

            // No item means nothing to move, so it cannot be stock and it must
            // name the account it posts to instead. Without this, such a line
            // lands on a control account, which is the posting that makes a
            // subledger stop tying.
            t.HasCheckConstraint(
                $"chk_{lower}_free_text",
                "\"ItemId\" IS NOT NULL "
                    + "OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");

            t.HasCheckConstraint(
                $"chk_{lower}_line_type",
                "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) "
                    + "AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) "
                    + "AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");

            // Exempt, nil-rated and outside-GST carry no tax. Zero-rated does
            // carry rows, at rate zero — which is why it is not in this list.
            t.HasCheckConstraint(
                $"chk_{lower}_untaxed",
                "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");

            t.HasCheckConstraint(
                $"chk_{lower}_tax_master",
                "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");

            t.HasCheckConstraint(
                $"chk_{lower}_amounts_non_negative",
                "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 "
                    + "AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
        });
    }

    /// <summary>Everything <see cref="DocumentLineTaxBase"/> implies, for one tax table.</summary>
    public static void ConfigureTax<T>(
        this EntityTypeBuilder<T> b, string table, string parent)
        where T : DocumentLineTaxBase
    {
        // The grain, enforced. Two CGST rows on one line would double the tax
        // and still foot, because both would be summed.
        b.HasIndex([parent, nameof(DocumentLineTaxBase.TaxComponent)])
            .IsUnique()
            .HasDatabaseName($"IX_{table}_Grain");

        b.Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(6);
        b.Property(e => e.Rate).HasColumnType("decimal(9,4)");
        b.Property(e => e.TaxableAmount).HasColumnType("decimal(28,2)");
        b.Property(e => e.Amount).HasColumnType("decimal(28,2)");
        b.Property(e => e.AmountBase).HasColumnType("decimal(28,2)");

        string lower = table.ToLowerInvariant();

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                $"chk_{lower}_non_negative",
                "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 "
                    + "AND \"AmountBase\" >= 0");
        });
    }

    private static readonly string[] HeaderAmounts =
    [
        "SubTotal", "DiscountAmount", "TaxableAmount",
        "CgstAmount", "SgstAmount", "IgstAmount", "CessAmount",
        "RoundOffAmount", "TotalAmount", "TotalAmountBase",
    ];

    private static readonly string[] LineAmounts =
    [
        "DiscountAmount", "GrossAmount", "TaxableAmount", "TaxAmount", "LineTotal",
    ];
}
