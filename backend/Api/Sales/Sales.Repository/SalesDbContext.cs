using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Sales.Repository;

/// <summary>
/// The <c>sal</c> schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
///
/// <b>Five header/line/tax triples, configured by three helpers.</b> The columns
/// are shared through the base classes, so the Fluent configuration is shared
/// too — fifteen hand-written blocks would be twelve chances to give one table a
/// different precision or drop one check constraint, and the table that got it
/// would be the one nobody looked at again.
/// </summary>
public class SalesDbContext : TenantDbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteDetail> QuoteDetails => Set<QuoteDetail>();

    public DbSet<QuoteDetailTax> QuoteDetailTaxes => Set<QuoteDetailTax>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<SalesOrderDetail> SalesOrderDetails => Set<SalesOrderDetail>();

    public DbSet<SalesOrderDetailTax> SalesOrderDetailTaxes => Set<SalesOrderDetailTax>();

    public DbSet<DeliveryChallan> DeliveryChallans => Set<DeliveryChallan>();

    public DbSet<DeliveryChallanDetail> DeliveryChallanDetails => Set<DeliveryChallanDetail>();

    public DbSet<DeliveryChallanDetailTax> DeliveryChallanDetailTaxes =>
        Set<DeliveryChallanDetailTax>();

    /// <summary>Invoices and POS sales both. A POS sale is a row here, not a table.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();

    public DbSet<InvoiceDetailTax> InvoiceDetailTaxes => Set<InvoiceDetailTax>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<CreditNoteDetail> CreditNoteDetails => Set<CreditNoteDetail>();

    public DbSet<CreditNoteDetailTax> CreditNoteDetailTaxes => Set<CreditNoteDetailTax>();

    /// <summary>Mapped, not migrated — Accounting owns the table.</summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sal");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        // ---- Quotes -------------------------------------------------------

        modelBuilder.Entity<Quote>(b =>
        {
            b.HasKey(e => e.QuoteId);
            ConfigureHeader(b, "Quotes", ["QTE"]);
        });

        modelBuilder.Entity<QuoteDetail>(b =>
        {
            b.HasKey(e => e.QuoteDetailId);
            ConfigureLine(b, "QuoteDetails", "QuoteId");
            b.HasOne<Quote>().WithMany().HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuoteDetailTax>(b =>
        {
            b.HasKey(e => e.QuoteDetailTaxId);
            ConfigureTax(b, "QuoteDetailTaxes", "QuoteDetailId");
            b.HasOne<QuoteDetail>().WithMany().HasForeignKey(e => e.QuoteDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Sales orders -------------------------------------------------

        modelBuilder.Entity<SalesOrder>(b =>
        {
            b.HasKey(e => e.SalesOrderId);
            ConfigureHeader(b, "SalesOrders", ["SOR"]);

            b.Property(e => e.FulfilmentStatus).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(e => new { e.OrgId, e.FulfilmentStatus })
                .HasDatabaseName("IX_SalesOrders_Fulfilment");

            // Restrict, not cascade: deleting a quote out from under the order
            // that came from it would erase what was agreed. Nothing here is
            // ever deleted anyway — that is what Void is for — so this is the
            // second line of defence rather than the first.
            b.HasOne<Quote>().WithMany().HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesOrderDetail>(b =>
        {
            b.HasKey(e => e.SalesOrderDetailId);
            ConfigureLine(b, "SalesOrderDetails", "SalesOrderId");

            b.Property(e => e.ReservedQuantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.DeliveredQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<SalesOrder>().WithMany().HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(t => t.HasCheckConstraint(
                "chk_salesorderdetails_quantities",
                "\"ReservedQuantity\" >= 0 AND \"DeliveredQuantity\" >= 0 "
                    + "AND \"DeliveredQuantity\" <= \"Quantity\""));
        });

        modelBuilder.Entity<SalesOrderDetailTax>(b =>
        {
            b.HasKey(e => e.SalesOrderDetailTaxId);
            ConfigureTax(b, "SalesOrderDetailTaxes", "SalesOrderDetailId");
            b.HasOne<SalesOrderDetail>().WithMany().HasForeignKey(e => e.SalesOrderDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Delivery challans --------------------------------------------

        modelBuilder.Entity<DeliveryChallan>(b =>
        {
            b.HasKey(e => e.DeliveryChallanId);
            ConfigureHeader(b, "DeliveryChallans", ["DLC"]);

            b.Property(e => e.ChallanType).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(e => new { e.OrgId, e.DispatchDate });

            b.HasOne<SalesOrder>().WithMany().HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryChallanDetail>(b =>
        {
            b.HasKey(e => e.DeliveryChallanDetailId);
            ConfigureLine(b, "DeliveryChallanDetails", "DeliveryChallanId");

            b.Property(e => e.InvoicedQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<DeliveryChallan>().WithMany().HasForeignKey(e => e.DeliveryChallanId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<SalesOrderDetail>().WithMany().HasForeignKey(e => e.SalesOrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t => t.HasCheckConstraint(
                "chk_deliverychallandetails_invoiced",
                "\"InvoicedQuantity\" >= 0 AND \"InvoicedQuantity\" <= \"Quantity\""));
        });

        modelBuilder.Entity<DeliveryChallanDetailTax>(b =>
        {
            b.HasKey(e => e.DeliveryChallanDetailTaxId);
            ConfigureTax(b, "DeliveryChallanDetailTaxes", "DeliveryChallanDetailId");
            b.HasOne<DeliveryChallanDetail>().WithMany()
                .HasForeignKey(e => e.DeliveryChallanDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Invoices and POS sales ---------------------------------------

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(e => e.InvoiceId);
            ConfigureHeader(b, "Invoices", ["INV", "POS"]);

            b.Property(e => e.PaymentMode).HasMaxLength(20);
            b.Property(e => e.TenderedAmount).HasColumnType("decimal(28,2)");
            b.Property(e => e.ChangeAmount).HasColumnType("decimal(28,2)");

            // What an aging report reads.
            b.HasIndex(e => new { e.OrgId, e.DueDate }).HasDatabaseName("IX_Invoices_Due");

            b.HasOne<Quote>().WithMany().HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesOrder>().WithMany().HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DeliveryChallan>().WithMany().HasForeignKey(e => e.DeliveryChallanId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t =>
            {
                // A POS row needs a till and a payment mode; an INV needs a due
                // date. This is the one place the two types genuinely differ, and
                // it is enforced here rather than in C# because it is the reason
                // somebody would argue for two tables.
                t.HasCheckConstraint(
                    "chk_invoices_pos_fields",
                    "(\"TransactionTypeCode\" <> 'POS') "
                        + "OR (\"TillId\" IS NOT NULL AND \"PaymentMode\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "chk_invoices_due_date",
                    "(\"TransactionTypeCode\" <> 'INV') OR (\"DueDate\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "chk_invoices_tender_non_negative",
                    "(\"TenderedAmount\" IS NULL OR \"TenderedAmount\" >= 0) "
                        + "AND (\"ChangeAmount\" IS NULL OR \"ChangeAmount\" >= 0)");
            });
        });

        modelBuilder.Entity<InvoiceDetail>(b =>
        {
            b.HasKey(e => e.InvoiceDetailId);
            ConfigureLine(b, "InvoiceDetails", "InvoiceId");

            b.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<SalesOrderDetail>().WithMany().HasForeignKey(e => e.SalesOrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            // Credited more than was sold is a double refund. The running total
            // is a column rather than a sum over credit notes because the guard
            // has to hold inside one transaction.
            b.ToTable(t => t.HasCheckConstraint(
                "chk_invoicedetails_returned",
                "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\""));
        });

        modelBuilder.Entity<InvoiceDetailTax>(b =>
        {
            b.HasKey(e => e.InvoiceDetailTaxId);
            ConfigureTax(b, "InvoiceDetailTaxes", "InvoiceDetailId");
            b.HasOne<InvoiceDetail>().WithMany().HasForeignKey(e => e.InvoiceDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Credit notes -------------------------------------------------

        modelBuilder.Entity<CreditNote>(b =>
        {
            b.HasKey(e => e.CreditNoteId);
            ConfigureHeader(b, "CreditNotes", ["CRN"]);

            b.Property(e => e.ReasonCode).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.InvoiceId });

            b.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditNoteDetail>(b =>
        {
            b.HasKey(e => e.CreditNoteDetailId);
            ConfigureLine(b, "CreditNoteDetails", "CreditNoteId");

            b.HasOne<CreditNote>().WithMany().HasForeignKey(e => e.CreditNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: the invoice line is how stock finds the cost layer it
            // came from. Losing it would leave the return to be valued at
            // whatever the running average is today.
            b.HasOne<InvoiceDetail>().WithMany().HasForeignKey(e => e.InvoiceDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditNoteDetailTax>(b =>
        {
            b.HasKey(e => e.CreditNoteDetailTaxId);
            ConfigureTax(b, "CreditNoteDetailTaxes", "CreditNoteDetailId");
            b.HasOne<CreditNoteDetail>().WithMany().HasForeignKey(e => e.CreditNoteDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Base class applies query filters, OrgId indexes and xmin last so
        // it sees every entity configured above.
        //
        // Without this call the fifteen sal tables had no EF query filter at
        // all — the first line of defence against one branch reading
        // another's documents — no OrgId index, and no concurrency token.
        // RLS still held at the database, which is why nothing failed
        // loudly; what surfaced was NumberingSeries trying to write a real
        // Version column instead of mapping xmin.
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Everything <see cref="DocumentHeaderBase"/> implies, for one header table.
    ///
    /// <paramref name="codes"/> is the set of transaction type codes the table
    /// may hold — one for every table except <c>Invoices</c>, which holds
    /// <c>INV</c> and <c>POS</c>. It becomes a check constraint, because the
    /// master table is in another database and no foreign key can say it.
    /// </summary>
    private static void ConfigureHeader<T>(
        EntityTypeBuilder<T> b, string table, string[] codes)
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
    private static void ConfigureLine<T>(EntityTypeBuilder<T> b, string table, string parent)
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
    private static void ConfigureTax<T>(EntityTypeBuilder<T> b, string table, string parent)
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
