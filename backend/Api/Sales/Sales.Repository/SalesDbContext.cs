using Microsoft.EntityFrameworkCore;
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
///
/// Those helpers live in <see cref="DocumentModelConfiguration"/> rather than
/// here, because <c>pur</c> has twelve more tables of the same shape and the
/// argument above does not stop at the schema boundary. What stays in this file
/// is what is true of sales and not of purchase: the conversion links, the
/// POS columns, and the reservation quantities.
///
/// <b>Every header-to-line relationship names its collection navigation, and it
/// has to.</b> Configured as <c>HasOne&lt;Quote&gt;().WithMany()</c> — no
/// navigation — the mapping is a perfectly good relationship that has nothing to
/// do with <c>Quote.Lines</c>, so EF mapped that collection a second time by
/// convention and gave it a shadow foreign key of its own: <c>QuoteId1</c>,
/// <c>SalesOrderId1</c>, and eight more across the schema. Adding a line through
/// the navigation then filled the shadow column and left the real, <c>NOT
/// NULL</c> one at zero, so <b>no sales document with lines could be saved at
/// all</b> — a foreign key violation on every create, in all five document types.
///
/// It survived for the same reason the missing <c>base.OnModelCreating</c> did:
/// nothing wrote to these tables while the schema was being built, and a table
/// nobody inserts into is a table nobody has checked. Six of these relationships
/// are still navigation-less on purpose — the conversion links, quote to order to
/// invoice — and those carry no collection to bind.
/// <c>Sales.Api.Tests.SalesSchemaTests</c> asserts that no shadow key comes back.
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

    public DbSet<SalesRegister> SalesRegister => Set<SalesRegister>();

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
            b.ConfigureHeader("Quotes", "QTE");
        });

        modelBuilder.Entity<QuoteDetail>(b =>
        {
            b.HasKey(e => e.QuoteDetailId);
            b.ConfigureLine("QuoteDetails", "QuoteId");
            b.HasOne<Quote>().WithMany(q => q.Lines).HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuoteDetailTax>(b =>
        {
            b.HasKey(e => e.QuoteDetailTaxId);
            b.ConfigureTax("QuoteDetailTaxes", "QuoteDetailId");
            b.HasOne<QuoteDetail>().WithMany(d => d.Taxes).HasForeignKey(e => e.QuoteDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Sales orders -------------------------------------------------

        modelBuilder.Entity<SalesOrder>(b =>
        {
            b.HasKey(e => e.SalesOrderId);
            b.ConfigureHeader("SalesOrders", "SOR");

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
            b.ConfigureLine("SalesOrderDetails", "SalesOrderId");

            b.Property(e => e.ReservedQuantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.DeliveredQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<SalesOrder>().WithMany(o => o.Lines).HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(t => t.HasCheckConstraint(
                "chk_salesorderdetails_quantities",
                "\"ReservedQuantity\" >= 0 AND \"DeliveredQuantity\" >= 0 "
                    + "AND \"DeliveredQuantity\" <= \"Quantity\""));
        });

        modelBuilder.Entity<SalesOrderDetailTax>(b =>
        {
            b.HasKey(e => e.SalesOrderDetailTaxId);
            b.ConfigureTax("SalesOrderDetailTaxes", "SalesOrderDetailId");
            b.HasOne<SalesOrderDetail>().WithMany(d => d.Taxes).HasForeignKey(e => e.SalesOrderDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Delivery challans --------------------------------------------

        modelBuilder.Entity<DeliveryChallan>(b =>
        {
            b.HasKey(e => e.DeliveryChallanId);
            b.ConfigureHeader("DeliveryChallans", "DLC");

            b.Property(e => e.ChallanType).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(e => new { e.OrgId, e.DispatchDate });

            b.HasOne<SalesOrder>().WithMany().HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryChallanDetail>(b =>
        {
            b.HasKey(e => e.DeliveryChallanDetailId);
            b.ConfigureLine("DeliveryChallanDetails", "DeliveryChallanId");

            b.Property(e => e.InvoicedQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<DeliveryChallan>().WithMany(c => c.Lines).HasForeignKey(e => e.DeliveryChallanId)
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
            b.ConfigureTax("DeliveryChallanDetailTaxes", "DeliveryChallanDetailId");
            b.HasOne<DeliveryChallanDetail>().WithMany(d => d.Taxes)
                .HasForeignKey(e => e.DeliveryChallanDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Invoices and POS sales ---------------------------------------

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(e => e.InvoiceId);
            b.ConfigureHeader("Invoices", "INV", "POS");

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
            b.ConfigureLine("InvoiceDetails", "InvoiceId");

            b.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<Invoice>().WithMany(i => i.Lines).HasForeignKey(e => e.InvoiceId)
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
            b.ConfigureTax("InvoiceDetailTaxes", "InvoiceDetailId");
            b.HasOne<InvoiceDetail>().WithMany(d => d.Taxes).HasForeignKey(e => e.InvoiceDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Credit notes -------------------------------------------------

        modelBuilder.Entity<CreditNote>(b =>
        {
            b.HasKey(e => e.CreditNoteId);
            b.ConfigureHeader("CreditNotes", "CRN");

            b.Property(e => e.ReasonCode).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.InvoiceId });

            b.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditNoteDetail>(b =>
        {
            b.HasKey(e => e.CreditNoteDetailId);
            b.ConfigureLine("CreditNoteDetails", "CreditNoteId");

            b.HasOne<CreditNote>().WithMany(c => c.Lines).HasForeignKey(e => e.CreditNoteId)
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
            b.ConfigureTax("CreditNoteDetailTaxes", "CreditNoteDetailId");
            b.HasOne<CreditNoteDetail>().WithMany(d => d.Taxes).HasForeignKey(e => e.CreditNoteDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Sales Register -----------------------------------------------
        
        modelBuilder.Entity<SalesRegister>(b =>
        {
            b.ToTable("SalesRegister", t =>
            {
                t.HasCheckConstraint(
                    "chk_salesregister_tax_split",
                    "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) "
                        + "OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
            });

            b.HasIndex(e => new { e.TransactionTypeCode, e.SourceId }).IsUnique();
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
