using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sal");

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "sal",
                columns: table => new
                {
                    QuoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_Quotes", x => x.QuoteId);
                    table.CheckConstraint("chk_quotes_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_quotes_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_quotes_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_quotes_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_quotes_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_quotes_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_quotes_type", "\"TransactionTypeCode\" IN ('QTE')");
                    table.CheckConstraint("chk_quotes_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "SalesRegister",
                schema: "sal",
                columns: table => new
                {
                    SalesRegisterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    SupplyType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ReverseCharge = table.Column<bool>(type: "boolean", nullable: false),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    GstRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxableAmountBase = table.Column<decimal>(type: "numeric", nullable: false),
                    OriginalInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalInvoiceNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OriginalInvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesRegister", x => x.SalesRegisterId);
                    table.CheckConstraint("chk_salesregister_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                });

            migrationBuilder.CreateTable(
                name: "QuoteDetails",
                schema: "sal",
                columns: table => new
                {
                    QuoteDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_QuoteDetails", x => x.QuoteDetailId);
                    table.CheckConstraint("chk_quotedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_quotedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_quotedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_quotedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_quotedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_quotedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_quotedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_quotedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_quotedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_quotedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_QuoteDetails_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuoteDetails_Quotes_QuoteId1",
                        column: x => x.QuoteId1,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId");
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FulfilmentStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SalesOrders", x => x.SalesOrderId);
                    table.CheckConstraint("chk_salesorders_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_salesorders_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_salesorders_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_salesorders_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_salesorders_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_salesorders_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_salesorders_type", "\"TransactionTypeCode\" IN ('SOR')");
                    table.CheckConstraint("chk_salesorders_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesOrders_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    QuoteDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteDetailId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteDetailId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_QuoteDetailTaxes", x => x.QuoteDetailTaxId);
                    table.CheckConstraint("chk_quotedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_QuoteDetailTaxes_QuoteDetails_QuoteDetailId",
                        column: x => x.QuoteDetailId,
                        principalSchema: "sal",
                        principalTable: "QuoteDetails",
                        principalColumn: "QuoteDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuoteDetailTaxes_QuoteDetails_QuoteDetailId1",
                        column: x => x.QuoteDetailId1,
                        principalSchema: "sal",
                        principalTable: "QuoteDetails",
                        principalColumn: "QuoteDetailId");
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallans",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ChallanType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DispatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransporterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EwayBillNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EwayBillDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_DeliveryChallans", x => x.DeliveryChallanId);
                    table.CheckConstraint("chk_deliverychallans_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_deliverychallans_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_deliverychallans_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_deliverychallans_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_deliverychallans_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_deliverychallans_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_deliverychallans_type", "\"TransactionTypeCode\" IN ('DLC')");
                    table.CheckConstraint("chk_deliverychallans_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DeliveryChallans_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderDetails",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DeliveredQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    SalesOrderId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SalesOrderDetails", x => x.SalesOrderDetailId);
                    table.CheckConstraint("chk_salesorderdetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_salesorderdetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_salesorderdetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_salesorderdetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_salesorderdetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_salesorderdetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_salesorderdetails_quantities", "\"ReservedQuantity\" >= 0 AND \"DeliveredQuantity\" >= 0 AND \"DeliveredQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_salesorderdetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_salesorderdetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_salesorderdetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_salesorderdetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderDetails_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOrderDetails_SalesOrders_SalesOrderId1",
                        column: x => x.SalesOrderId1,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "sal",
                columns: table => new
                {
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: true),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TillId = table.Column<long>(type: "bigint", nullable: true),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TenderedAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.CheckConstraint("chk_invoices_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_invoices_due_date", "(\"TransactionTypeCode\" <> 'INV') OR (\"DueDate\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoices_pos_fields", "(\"TransactionTypeCode\" <> 'POS') OR (\"TillId\" IS NOT NULL AND \"PaymentMode\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoices_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_invoices_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_invoices_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_invoices_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_invoices_tender_non_negative", "(\"TenderedAmount\" IS NULL OR \"TenderedAmount\" >= 0) AND (\"ChangeAmount\" IS NULL OR \"ChangeAmount\" >= 0)");
                    table.CheckConstraint("chk_invoices_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_invoices_type", "\"TransactionTypeCode\" IN ('INV', 'POS')");
                    table.CheckConstraint("chk_invoices_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Invoices_DeliveryChallans_DeliveryChallanId",
                        column: x => x.DeliveryChallanId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallans",
                        principalColumn: "DeliveryChallanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanDetails",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    InvoicedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DeliveryChallanId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_DeliveryChallanDetails", x => x.DeliveryChallanDetailId);
                    table.CheckConstraint("chk_deliverychallandetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_deliverychallandetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_deliverychallandetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_deliverychallandetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_deliverychallandetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_deliverychallandetails_invoiced", "\"InvoicedQuantity\" >= 0 AND \"InvoicedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_deliverychallandetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_deliverychallandetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_deliverychallandetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_deliverychallandetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_deliverychallandetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetails_DeliveryChallans_DeliveryChallanId",
                        column: x => x.DeliveryChallanId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallans",
                        principalColumn: "DeliveryChallanId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetails_DeliveryChallans_DeliveryChallanId1",
                        column: x => x.DeliveryChallanId1,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallans",
                        principalColumn: "DeliveryChallanId");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetails_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderDetailId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SalesOrderDetailTaxes", x => x.SalesOrderDetailTaxId);
                    table.CheckConstraint("chk_salesorderdetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderDetailTaxes_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOrderDetailTaxes_SalesOrderDetails_SalesOrderDetailId1",
                        column: x => x.SalesOrderDetailId1,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId");
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_CreditNotes", x => x.CreditNoteId);
                    table.CheckConstraint("chk_creditnotes_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_creditnotes_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_creditnotes_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_creditnotes_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_creditnotes_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_creditnotes_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_creditnotes_type", "\"TransactionTypeCode\" IN ('CRN')");
                    table.CheckConstraint("chk_creditnotes_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CreditNotes_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                schema: "sal",
                columns: table => new
                {
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    InvoiceId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InvoiceDetails", x => x.InvoiceDetailId);
                    table.CheckConstraint("chk_invoicedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_invoicedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_invoicedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_invoicedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_invoicedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_invoicedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoicedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_invoicedetails_returned", "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_invoicedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_invoicedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_invoicedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Invoices_InvoiceId1",
                        column: x => x.InvoiceId1,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId");
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryChallanDetailId = table.Column<long>(type: "bigint", nullable: false),
                    DeliveryChallanDetailId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_DeliveryChallanDetailTaxes", x => x.DeliveryChallanDetailTaxId);
                    table.CheckConstraint("chk_deliverychallandetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetailTaxes_DeliveryChallanDetails_DeliveryC~",
                        column: x => x.DeliveryChallanDetailId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallanDetails",
                        principalColumn: "DeliveryChallanDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetailTaxes_DeliveryChallanDetails_Delivery~1",
                        column: x => x.DeliveryChallanDetailId1,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallanDetails",
                        principalColumn: "DeliveryChallanDetailId");
                });

            migrationBuilder.CreateTable(
                name: "CreditNoteDetails",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditNoteId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreditNoteId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_CreditNoteDetails", x => x.CreditNoteDetailId);
                    table.CheckConstraint("chk_creditnotedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_creditnotedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_creditnotedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_creditnotedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_creditnotedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_creditnotedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_creditnotedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_creditnotedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_creditnotedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_creditnotedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_CreditNoteDetails_CreditNotes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "sal",
                        principalTable: "CreditNotes",
                        principalColumn: "CreditNoteId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditNoteDetails_CreditNotes_CreditNoteId1",
                        column: x => x.CreditNoteId1,
                        principalSchema: "sal",
                        principalTable: "CreditNotes",
                        principalColumn: "CreditNoteId");
                    table.ForeignKey(
                        name: "FK_CreditNoteDetails_InvoiceDetails_InvoiceDetailId",
                        column: x => x.InvoiceDetailId,
                        principalSchema: "sal",
                        principalTable: "InvoiceDetails",
                        principalColumn: "InvoiceDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    InvoiceDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceDetailId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InvoiceDetailTaxes", x => x.InvoiceDetailTaxId);
                    table.CheckConstraint("chk_invoicedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceDetailTaxes_InvoiceDetails_InvoiceDetailId",
                        column: x => x.InvoiceDetailId,
                        principalSchema: "sal",
                        principalTable: "InvoiceDetails",
                        principalColumn: "InvoiceDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceDetailTaxes_InvoiceDetails_InvoiceDetailId1",
                        column: x => x.InvoiceDetailId1,
                        principalSchema: "sal",
                        principalTable: "InvoiceDetails",
                        principalColumn: "InvoiceDetailId");
                });

            migrationBuilder.CreateTable(
                name: "CreditNoteDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditNoteDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreditNoteDetailId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_CreditNoteDetailTaxes", x => x.CreditNoteDetailTaxId);
                    table.CheckConstraint("chk_creditnotedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_CreditNoteDetailTaxes_CreditNoteDetails_CreditNoteDetailId",
                        column: x => x.CreditNoteDetailId,
                        principalSchema: "sal",
                        principalTable: "CreditNoteDetails",
                        principalColumn: "CreditNoteDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditNoteDetailTaxes_CreditNoteDetails_CreditNoteDetailId1",
                        column: x => x.CreditNoteDetailId1,
                        principalSchema: "sal",
                        principalTable: "CreditNoteDetails",
                        principalColumn: "CreditNoteDetailId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                column: "CreditNoteDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_Grain",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                columns: new[] { "CreditNoteDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "CreditNoteId1");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_InvoiceDetailId",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "InvoiceDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_Item",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_Line",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "CreditNoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Contact",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Date",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_InvoiceId",
                schema: "sal",
                table: "CreditNotes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Number",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_OrgId_InvoiceId",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Status",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_DeliveryChallanDetailId1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                column: "DeliveryChallanDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_Grain",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                columns: new[] { "DeliveryChallanDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "DeliveryChallanId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_Item",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_Line",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "DeliveryChallanId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_SalesOrderDetailId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "SalesOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Contact",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Date",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Number",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_OrgId_DispatchDate",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DispatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_SalesOrderId",
                schema: "sal",
                table: "DeliveryChallans",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Status",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_Grain",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                columns: new[] { "InvoiceDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                column: "InvoiceDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails",
                column: "InvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_Item",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_Line",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_SalesOrderDetailId",
                schema: "sal",
                table: "InvoiceDetails",
                column: "SalesOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Contact",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Date",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DeliveryChallanId",
                schema: "sal",
                table: "Invoices",
                column: "DeliveryChallanId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Due",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_QuoteId",
                schema: "sal",
                table: "Invoices",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SalesOrderId",
                schema: "sal",
                table: "Invoices",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_Grain",
                schema: "sal",
                table: "QuoteDetailTaxes",
                columns: new[] { "QuoteDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes",
                column: "QuoteDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_Item",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_Line",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "QuoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_QuoteId1",
                schema: "sal",
                table: "QuoteDetails",
                column: "QuoteId1");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Contact",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Date",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Number",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_Grain",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                columns: new[] { "SalesOrderDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                column: "SalesOrderDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_Item",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_Line",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "SalesOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails",
                column: "SalesOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Contact",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Date",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Fulfilment",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "FulfilmentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Number",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_QuoteId",
                schema: "sal",
                table: "SalesOrders",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Status",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRegister_TransactionTypeCode_SourceId",
                schema: "sal",
                table: "SalesRegister",
                columns: new[] { "TransactionTypeCode", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditNoteDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallanDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "QuoteDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrderDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesRegister",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "CreditNoteDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallanDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "QuoteDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "CreditNotes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrderDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallans",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrders",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "sal");
        }
    }
}
