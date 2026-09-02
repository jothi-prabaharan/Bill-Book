using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Purchase.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialPurchaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pur");

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "pur",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FulfilmentStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.PurchaseOrderId);
                    table.CheckConstraint("chk_purchaseorders_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_purchaseorders_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_purchaseorders_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_purchaseorders_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_purchaseorders_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_purchaseorders_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_purchaseorders_type", "\"TransactionTypeCode\" IN ('POR')");
                    table.CheckConstraint("chk_purchaseorders_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                schema: "pur",
                columns: table => new
                {
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    VendorDeliveryNoteNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorDeliveryNoteDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.GoodsReceiptId);
                    table.CheckConstraint("chk_goodsreceipts_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_goodsreceipts_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_goodsreceipts_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_goodsreceipts_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_goodsreceipts_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_goodsreceipts_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_goodsreceipts_type", "\"TransactionTypeCode\" IN ('GRN')");
                    table.CheckConstraint("chk_goodsreceipts_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDetails",
                schema: "pur",
                columns: table => new
                {
                    PurchaseOrderDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BilledQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDetails", x => x.PurchaseOrderDetailId);
                    table.CheckConstraint("chk_purchaseorderdetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_purchaseorderdetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_purchaseorderdetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_purchaseorderdetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_purchaseorderdetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_purchaseorderdetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_purchaseorderdetails_quantities", "\"ReceivedQuantity\" >= 0 AND \"BilledQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\" AND \"BilledQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_purchaseorderdetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_purchaseorderdetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_purchaseorderdetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_purchaseorderdetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDetails_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                schema: "pur",
                columns: table => new
                {
                    BillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    VendorBillNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorBillDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VendorBillFinancialYear = table.Column<int>(type: "integer", nullable: false, computedColumnSql: "CASE WHEN EXTRACT(MONTH FROM \"VendorBillDate\") >= 4 THEN EXTRACT(YEAR FROM \"VendorBillDate\") ELSE EXTRACT(YEAR FROM \"VendorBillDate\") - 1 END", stored: true),
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LandedCostAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.BillId);
                    table.CheckConstraint("chk_bills_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_bills_landed_cost_non_negative", "\"LandedCostAmount\" >= 0");
                    table.CheckConstraint("chk_bills_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_bills_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_bills_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_bills_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_bills_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_bills_type", "\"TransactionTypeCode\" IN ('BIL')");
                    table.CheckConstraint("chk_bills_vendor_date", "\"VendorBillDate\" <= \"DocumentDate\"");
                    table.CheckConstraint("chk_bills_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Bills_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "pur",
                        principalTable: "GoodsReceipts",
                        principalColumn: "GoodsReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptDetails",
                schema: "pur",
                columns: table => new
                {
                    GoodsReceiptDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    AcceptedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptDetails", x => x.GoodsReceiptDetailId);
                    table.CheckConstraint("chk_goodsreceiptdetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_goodsreceiptdetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_goodsreceiptdetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_goodsreceiptdetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_goodsreceiptdetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_goodsreceiptdetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_goodsreceiptdetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_goodsreceiptdetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_goodsreceiptdetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_goodsreceiptdetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.CheckConstraint("chk_grn_accepted", "\"AcceptedQuantity\" >= 0 AND \"RejectedQuantity\" >= 0 AND \"AcceptedQuantity\" + \"RejectedQuantity\" = \"Quantity\"");
                    table.CheckConstraint("chk_grn_rejection_reason", "\"RejectedQuantity\" = 0 OR \"RejectionReason\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptDetails_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "pur",
                        principalTable: "GoodsReceipts",
                        principalColumn: "GoodsReceiptId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptDetails_PurchaseOrderDetails_PurchaseOrderDetai~",
                        column: x => x.PurchaseOrderDetailId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrderDetails",
                        principalColumn: "PurchaseOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDetailTaxes",
                schema: "pur",
                columns: table => new
                {
                    PurchaseOrderDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDetailTaxes", x => x.PurchaseOrderDetailTaxId);
                    table.CheckConstraint("chk_purchaseorderdetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDetailTaxes_PurchaseOrderDetails_PurchaseOrder~",
                        column: x => x.PurchaseOrderDetailId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrderDetails",
                        principalColumn: "PurchaseOrderDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebitNotes",
                schema: "pur",
                columns: table => new
                {
                    DebitNoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebitNotes", x => x.DebitNoteId);
                    table.CheckConstraint("chk_debitnotes_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_debitnotes_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_debitnotes_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_debitnotes_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_debitnotes_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_debitnotes_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_debitnotes_type", "\"TransactionTypeCode\" IN ('DBN')");
                    table.CheckConstraint("chk_debitnotes_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DebitNotes_Bills_BillId",
                        column: x => x.BillId,
                        principalSchema: "pur",
                        principalTable: "Bills",
                        principalColumn: "BillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillDetails",
                schema: "pur",
                columns: table => new
                {
                    BillDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    GoodsReceiptDetailId = table.Column<long>(type: "bigint", nullable: true),
                    PurchaseOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ApportionedLandedCost = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillDetails", x => x.BillDetailId);
                    table.CheckConstraint("chk_billdetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_billdetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_billdetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_billdetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_billdetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_billdetails_landed_cost_non_negative", "\"ApportionedLandedCost\" >= 0");
                    table.CheckConstraint("chk_billdetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_billdetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_billdetails_returned", "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_billdetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_billdetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_billdetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_BillDetails_Bills_BillId",
                        column: x => x.BillId,
                        principalSchema: "pur",
                        principalTable: "Bills",
                        principalColumn: "BillId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillDetails_GoodsReceiptDetails_GoodsReceiptDetailId",
                        column: x => x.GoodsReceiptDetailId,
                        principalSchema: "pur",
                        principalTable: "GoodsReceiptDetails",
                        principalColumn: "GoodsReceiptDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillDetails_PurchaseOrderDetails_PurchaseOrderDetailId",
                        column: x => x.PurchaseOrderDetailId,
                        principalSchema: "pur",
                        principalTable: "PurchaseOrderDetails",
                        principalColumn: "PurchaseOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptDetailTaxes",
                schema: "pur",
                columns: table => new
                {
                    GoodsReceiptDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoodsReceiptDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptDetailTaxes", x => x.GoodsReceiptDetailTaxId);
                    table.CheckConstraint("chk_goodsreceiptdetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptDetailTaxes_GoodsReceiptDetails_GoodsReceiptDet~",
                        column: x => x.GoodsReceiptDetailId,
                        principalSchema: "pur",
                        principalTable: "GoodsReceiptDetails",
                        principalColumn: "GoodsReceiptDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillDetailTaxes",
                schema: "pur",
                columns: table => new
                {
                    BillDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillDetailTaxes", x => x.BillDetailTaxId);
                    table.CheckConstraint("chk_billdetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_BillDetailTaxes_BillDetails_BillDetailId",
                        column: x => x.BillDetailId,
                        principalSchema: "pur",
                        principalTable: "BillDetails",
                        principalColumn: "BillDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebitNoteDetails",
                schema: "pur",
                columns: table => new
                {
                    DebitNoteDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DebitNoteId = table.Column<long>(type: "bigint", nullable: false),
                    BillDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebitNoteDetails", x => x.DebitNoteDetailId);
                    table.CheckConstraint("chk_debitnotedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_debitnotedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_debitnotedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_debitnotedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_debitnotedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_debitnotedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_debitnotedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_debitnotedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_debitnotedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_debitnotedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_DebitNoteDetails_BillDetails_BillDetailId",
                        column: x => x.BillDetailId,
                        principalSchema: "pur",
                        principalTable: "BillDetails",
                        principalColumn: "BillDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebitNoteDetails_DebitNotes_DebitNoteId",
                        column: x => x.DebitNoteId,
                        principalSchema: "pur",
                        principalTable: "DebitNotes",
                        principalColumn: "DebitNoteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebitNoteDetailTaxes",
                schema: "pur",
                columns: table => new
                {
                    DebitNoteDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DebitNoteDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebitNoteDetailTaxes", x => x.DebitNoteDetailTaxId);
                    table.CheckConstraint("chk_debitnotedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_DebitNoteDetailTaxes_DebitNoteDetails_DebitNoteDetailId",
                        column: x => x.DebitNoteDetailId,
                        principalSchema: "pur",
                        principalTable: "DebitNoteDetails",
                        principalColumn: "DebitNoteDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetailTaxes_Grain",
                schema: "pur",
                table: "BillDetailTaxes",
                columns: new[] { "BillDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_GoodsReceiptDetailId",
                schema: "pur",
                table: "BillDetails",
                column: "GoodsReceiptDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_Item",
                schema: "pur",
                table: "BillDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_Line",
                schema: "pur",
                table: "BillDetails",
                columns: new[] { "BillId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_PurchaseOrderDetailId",
                schema: "pur",
                table: "BillDetails",
                column: "PurchaseOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Contact",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId_OrgId",
                schema: "pur",
                table: "Bills",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Date",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Due",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_GoodsReceiptId",
                schema: "pur",
                table: "Bills",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Number",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PurchaseOrderId",
                schema: "pur",
                table: "Bills",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Status",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_VendorBillNo",
                schema: "pur",
                table: "Bills",
                columns: new[] { "OrgId", "ContactId", "VendorBillNo", "VendorBillFinancialYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetailTaxes_Grain",
                schema: "pur",
                table: "DebitNoteDetailTaxes",
                columns: new[] { "DebitNoteDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_BillDetailId",
                schema: "pur",
                table: "DebitNoteDetails",
                column: "BillDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_Item",
                schema: "pur",
                table: "DebitNoteDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_Line",
                schema: "pur",
                table: "DebitNoteDetails",
                columns: new[] { "DebitNoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_BillId",
                schema: "pur",
                table: "DebitNotes",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_Contact",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_Date",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_Number",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_OrgId_BillId",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "OrgId", "BillId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_Status",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetailTaxes_Grain",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes",
                columns: new[] { "GoodsReceiptDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_Item",
                schema: "pur",
                table: "GoodsReceiptDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_Line",
                schema: "pur",
                table: "GoodsReceiptDetails",
                columns: new[] { "GoodsReceiptId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_PurchaseOrderDetailId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                column: "PurchaseOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_Contact",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_Date",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_Number",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseOrderId",
                schema: "pur",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_Status",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetailTaxes_Grain",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes",
                columns: new[] { "PurchaseOrderDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetails_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetails_Item",
                schema: "pur",
                table: "PurchaseOrderDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetails_Line",
                schema: "pur",
                table: "PurchaseOrderDetails",
                columns: new[] { "PurchaseOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Contact",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Date",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Fulfilment",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "OrgId", "FulfilmentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Number",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "OrgId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillDetailTaxes",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "DebitNoteDetailTaxes",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "GoodsReceiptDetailTaxes",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDetailTaxes",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "DebitNoteDetails",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "BillDetails",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "DebitNotes",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "GoodsReceiptDetails",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "Bills",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDetails",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "GoodsReceipts",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "pur");
        }
    }
}
