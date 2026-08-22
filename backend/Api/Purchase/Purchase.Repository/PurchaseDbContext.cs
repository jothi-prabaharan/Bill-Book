using Microsoft.EntityFrameworkCore;
using Purchase.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository;

/// <summary>
/// The <c>pur</c> schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
///
/// <b>Four header/line/tax triples, configured by the same three helpers Sales
/// uses.</b> They live in <see cref="DocumentModelConfiguration"/>, so the
/// twenty-seven document tables across both schemas get their precision, indexes
/// and check constraints from one place. "Purchase follows the same schema as
/// sales" is generated here rather than asserted: a constraint corrected in that
/// file is corrected on both sides, and a difference between them has to be
/// written in this file deliberately.
///
/// What is written here is what is true of purchase and not of sales — and per
/// <c>docs/Purchase.md</c> §1 there are five such things, four of which
/// reach the schema:
///
/// <list type="bullet">
/// <item>the order reserves nothing, so there is no reserved quantity anywhere;</item>
/// <item>stock moves at the receipt, so the accepted/rejected split lives on
/// <c>GoodsReceiptDetails</c>;</item>
/// <item>the vendor issues the number, so <c>Bills</c> carries theirs under a
/// unique index as well as ours;</item>
/// <item>all three <c>LineType</c> kinds are used, which the shared line
/// constraints already enforce.</item>
/// </list>
///
/// The fifth — input GST is an asset where output GST is a liability — is a
/// difference in meaning with no difference in shape, and so appears nowhere in
/// this file.
/// </summary>
public class PurchaseDbContext : TenantDbContext
{
    public PurchaseDbContext(DbContextOptions<PurchaseDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();

    public DbSet<PurchaseOrderDetailTax> PurchaseOrderDetailTaxes =>
        Set<PurchaseOrderDetailTax>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<GoodsReceiptDetail> GoodsReceiptDetails => Set<GoodsReceiptDetail>();

    public DbSet<GoodsReceiptDetailTax> GoodsReceiptDetailTaxes =>
        Set<GoodsReceiptDetailTax>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<BillDetail> BillDetails => Set<BillDetail>();

    public DbSet<BillDetailTax> BillDetailTaxes => Set<BillDetailTax>();

    public DbSet<DebitNote> DebitNotes => Set<DebitNote>();

    public DbSet<DebitNoteDetail> DebitNoteDetails => Set<DebitNoteDetail>();

    public DbSet<DebitNoteDetailTax> DebitNoteDetailTaxes => Set<DebitNoteDetailTax>();

    /// <summary>Mapped, not migrated — Accounting owns the table.</summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pur");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        // ---- Purchase orders ----------------------------------------------

        modelBuilder.Entity<PurchaseOrder>(b =>
        {
            b.HasKey(e => e.PurchaseOrderId);
            b.ConfigureHeader("PurchaseOrders", "POR");

            b.Property(e => e.FulfilmentStatus).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(e => new { e.OrgId, e.FulfilmentStatus })
                .HasDatabaseName("IX_PurchaseOrders_Fulfilment");
        });

        modelBuilder.Entity<PurchaseOrderDetail>(b =>
        {
            b.HasKey(e => e.PurchaseOrderDetailId);
            b.ConfigureLine("PurchaseOrderDetails", "PurchaseOrderId");

            b.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.BilledQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<PurchaseOrder>().WithMany(h => h.Lines).HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Neither running total may exceed what was ordered. They are not
            // compared to each other: goods can arrive before the bill and a
            // bill can arrive before the goods, so either may lead.
            b.ToTable(t => t.HasCheckConstraint(
                "chk_purchaseorderdetails_quantities",
                "\"ReceivedQuantity\" >= 0 AND \"BilledQuantity\" >= 0 "
                    + "AND \"ReceivedQuantity\" <= \"Quantity\" "
                    + "AND \"BilledQuantity\" <= \"Quantity\""));
        });

        modelBuilder.Entity<PurchaseOrderDetailTax>(b =>
        {
            b.HasKey(e => e.PurchaseOrderDetailTaxId);
            b.ConfigureTax("PurchaseOrderDetailTaxes", "PurchaseOrderDetailId");
            b.HasOne<PurchaseOrderDetail>().WithMany(l => l.Taxes)
                .HasForeignKey(e => e.PurchaseOrderDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Goods receipts -----------------------------------------------

        modelBuilder.Entity<GoodsReceipt>(b =>
        {
            b.HasKey(e => e.GoodsReceiptId);
            b.ConfigureHeader("GoodsReceipts", "GRN");

            b.Property(e => e.VendorDeliveryNoteNo).HasMaxLength(50);

            // Restrict, not cascade: deleting an order out from under the
            // receipt that came against it would erase what was agreed. Nothing
            // here is ever deleted anyway — that is what Void is for — so this
            // is the second line of defence rather than the first.
            b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptDetail>(b =>
        {
            b.HasKey(e => e.GoodsReceiptDetailId);
            b.ConfigureLine("GoodsReceiptDetails", "GoodsReceiptId");

            b.Property(e => e.AcceptedQuantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.RejectedQuantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.RejectionReason).HasMaxLength(300);

            b.HasOne<GoodsReceipt>().WithMany(h => h.Lines).HasForeignKey(e => e.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<PurchaseOrderDetail>().WithMany()
                .HasForeignKey(e => e.PurchaseOrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t =>
            {
                // What arrived is what was accepted plus what was refused. In the
                // database rather than in C# because only the accepted quantity
                // becomes stock: if these three ever disagree, the difference is
                // inventory that either exists and was never recorded or was
                // recorded and never existed.
                t.HasCheckConstraint(
                    "chk_grn_accepted",
                    "\"AcceptedQuantity\" >= 0 AND \"RejectedQuantity\" >= 0 "
                        + "AND \"AcceptedQuantity\" + \"RejectedQuantity\" = \"Quantity\"");

                // A rejection with no reason is an argument with the vendor that
                // nobody can reconstruct.
                t.HasCheckConstraint(
                    "chk_grn_rejection_reason",
                    "\"RejectedQuantity\" = 0 OR \"RejectionReason\" IS NOT NULL");
            });
        });

        modelBuilder.Entity<GoodsReceiptDetailTax>(b =>
        {
            b.HasKey(e => e.GoodsReceiptDetailTaxId);
            b.ConfigureTax("GoodsReceiptDetailTaxes", "GoodsReceiptDetailId");
            b.HasOne<GoodsReceiptDetail>().WithMany(l => l.Taxes)
                .HasForeignKey(e => e.GoodsReceiptDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Bills --------------------------------------------------------

        modelBuilder.Entity<Bill>(b =>
        {
            b.HasKey(e => e.BillId);
            b.ConfigureHeader("Bills", "BIL");

            b.Property(e => e.VendorBillNo).HasMaxLength(50);
            b.Property(e => e.LandedCostAmount).HasColumnType("decimal(28,2)");

            // Postgres computes it; C# never writes it. See the property's own
            // summary for why the year is derived in the database and why April
            // is hardcoded where Numbering's start month is configurable.
            b.Property(e => e.VendorBillFinancialYear)
                .HasComputedColumnSql(
                    "CASE WHEN EXTRACT(MONTH FROM \"VendorBillDate\") >= 4 "
                        + "THEN EXTRACT(YEAR FROM \"VendorBillDate\") "
                        + "ELSE EXTRACT(YEAR FROM \"VendorBillDate\") - 1 END",
                    stored: true);

            // The index that stops a duplicate input tax credit claim. One vendor
            // cannot bill the same number twice in one GST year, and refusing it
            // at entry is the difference between a correction and a demand notice.
            b.HasIndex(e => new
            {
                e.OrgId,
                e.ContactId,
                e.VendorBillNo,
                e.VendorBillFinancialYear,
            })
                .IsUnique()
                .HasDatabaseName("IX_Bills_VendorBillNo");

            // What an aging report reads.
            b.HasIndex(e => new { e.OrgId, e.DueDate }).HasDatabaseName("IX_Bills_Due");

            b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<GoodsReceipt>().WithMany().HasForeignKey(e => e.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "chk_bills_landed_cost_non_negative",
                    "\"LandedCostAmount\" >= 0");

                // The vendor cannot have dated their bill after we received it.
                // Catching it here stops a bill landing in a tax period that had
                // already been filed.
                t.HasCheckConstraint(
                    "chk_bills_vendor_date",
                    "\"VendorBillDate\" <= \"DocumentDate\"");
            });
        });

        modelBuilder.Entity<BillDetail>(b =>
        {
            b.HasKey(e => e.BillDetailId);
            b.ConfigureLine("BillDetails", "BillId");

            b.Property(e => e.ApportionedLandedCost).HasColumnType("decimal(28,2)");
            b.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18,6)");

            b.HasOne<Bill>().WithMany(h => h.Lines).HasForeignKey(e => e.BillId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<GoodsReceiptDetail>().WithMany()
                .HasForeignKey(e => e.GoodsReceiptDetailId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseOrderDetail>().WithMany()
                .HasForeignKey(e => e.PurchaseOrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t =>
            {
                // Returned more than was bought is a double reclaim of input tax.
                t.HasCheckConstraint(
                    "chk_billdetails_returned",
                    "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");

                t.HasCheckConstraint(
                    "chk_billdetails_landed_cost_non_negative",
                    "\"ApportionedLandedCost\" >= 0");
            });
        });

        modelBuilder.Entity<BillDetailTax>(b =>
        {
            b.HasKey(e => e.BillDetailTaxId);
            b.ConfigureTax("BillDetailTaxes", "BillDetailId");
            b.HasOne<BillDetail>().WithMany(l => l.Taxes).HasForeignKey(e => e.BillDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Debit notes --------------------------------------------------

        modelBuilder.Entity<DebitNote>(b =>
        {
            b.HasKey(e => e.DebitNoteId);
            b.ConfigureHeader("DebitNotes", "DBN");

            b.Property(e => e.ReasonCode).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.BillId });

            b.HasOne<Bill>().WithMany().HasForeignKey(e => e.BillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebitNoteDetail>(b =>
        {
            b.HasKey(e => e.DebitNoteDetailId);
            b.ConfigureLine("DebitNoteDetails", "DebitNoteId");

            b.HasOne<DebitNote>().WithMany(h => h.Lines).HasForeignKey(e => e.DebitNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: the bill line is how returning stock finds the cost layer
            // it arrived on. Losing it would leave the return to be valued at
            // whatever the running average is today.
            b.HasOne<BillDetail>().WithMany().HasForeignKey(e => e.BillDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebitNoteDetailTax>(b =>
        {
            b.HasKey(e => e.DebitNoteDetailTaxId);
            b.ConfigureTax("DebitNoteDetailTaxes", "DebitNoteDetailId");
            b.HasOne<DebitNoteDetail>().WithMany(l => l.Taxes)
                .HasForeignKey(e => e.DebitNoteDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        //
        // <b>Not optional, and not a formality.</b> Without this line
        // TenantDbContext never runs: no global OrgId query filter on any table
        // in this schema, no OrgId index, and Version mapped as an ordinary
        // bigint rather than the xmin system column, so concurrency checks
        // silently stop working too. Inventory and Accounting both end their
        // OnModelCreating this way. SalesDbContext does not, which is why `sal`
        // has neither of the two isolation layers it is supposed to have — see
        // the note in docs/Purchase.md §11.
        base.OnModelCreating(modelBuilder);
    }
}
