using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccountingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "acc");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "acc",
                columns: table => new
                {
                    AccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    IsContra = table.Column<bool>(type: "boolean", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountSystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentAccountId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    IsJE = table.Column<bool>(type: "boolean", nullable: false),
                    IsLock = table.Column<bool>(type: "boolean", nullable: false),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsPayment = table.Column<bool>(type: "boolean", nullable: false),
                    IsBank = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Banks",
                schema: "acc",
                columns: table => new
                {
                    BankId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.BankId);
                });

            migrationBuilder.CreateTable(
                name: "Journals",
                schema: "acc",
                columns: table => new
                {
                    JournalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    JournalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesJournalId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByJournalId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.JournalId);
                    table.CheckConstraint("chk_journal_number_on_post", "(\"Status\" = 'Draft' AND \"JournalNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"JournalNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_journal_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_journal_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_journal_reversal_distinct", "\"ReversesJournalId\" IS NULL OR \"ReversesJournalId\" <> \"JournalId\"");
                    table.ForeignKey(
                        name: "FK_Journals_Journals_ReversedByJournalId",
                        column: x => x.ReversedByJournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Journals_Journals_ReversesJournalId",
                        column: x => x.ReversesJournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NumberingSeries",
                schema: "acc",
                columns: table => new
                {
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SeriesCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SeriesName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SeriesFor = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Suffix = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Separator = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    IncludeFinancialYear = table.Column<bool>(type: "boolean", nullable: false),
                    FinancialYearFormat = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IncludeBranchCode = table.Column<bool>(type: "boolean", nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NumberLength = table.Column<int>(type: "integer", nullable: false),
                    StartNumber = table.Column<long>(type: "bigint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    ResetFrequency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LastResetOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AllowManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSeries", x => x.NumberingSeriesId);
                    table.CheckConstraint("chk_numbering_branch_code", "\"IncludeBranchCode\" = false OR \"BranchCode\" IS NOT NULL");
                    table.CheckConstraint("chk_numbering_counter", "\"NextNumber\" >= \"StartNumber\" AND \"NumberLength\" BETWEEN 1 AND 12");
                    table.CheckConstraint("chk_numbering_manual_override", "\"SeriesFor\" = 'Master' OR \"AllowManualOverride\" = false");
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                schema: "acc",
                columns: table => new
                {
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.OpeningBalanceId);
                    table.CheckConstraint("chk_opening_finalized_stamp", "(\"Status\" = 'Draft') = (\"FinalizedAt\" IS NULL)");
                    table.CheckConstraint("chk_opening_number_on_finalize", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                schema: "acc",
                columns: table => new
                {
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TermName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TermType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDays = table.Column<int>(type: "integer", nullable: false),
                    DueDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DiscountDays = table.Column<int>(type: "integer", nullable: false),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.PaymentTermId);
                    table.CheckConstraint("chk_term_applicability", "\"IsSales\" = true OR \"IsPurchase\" = true");
                    table.CheckConstraint("chk_term_day_of_month", "(\"TermType\" = 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NOT NULL) OR (\"TermType\" <> 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NULL)");
                    table.CheckConstraint("chk_term_discount_days", "\"DiscountPercent\" = 0 OR \"DiscountDays\" > 0");
                    table.CheckConstraint("chk_term_discount_window", "\"TermType\" <> 'Net' OR \"DiscountDays\" <= \"DueDays\"");
                    table.CheckConstraint("chk_term_due_on_receipt", "\"TermType\" <> 'DueOnReceipt' OR \"DueDays\" = 0");
                });

            migrationBuilder.CreateTable(
                name: "PeriodLocks",
                schema: "acc",
                columns: table => new
                {
                    PeriodLockId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    LockedUpto = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodLocks", x => x.PeriodLockId);
                });

            migrationBuilder.CreateTable(
                name: "TaxMasters",
                schema: "acc",
                columns: table => new
                {
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: false),
                    TaxSystemName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    SgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CessRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxMasters", x => x.TaxMasterId);
                    table.CheckConstraint("chk_tax_applicability", "\"IsSales\" = true OR \"IsPurchase\" = true");
                    table.CheckConstraint("chk_tax_effective_range", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("chk_tax_split", "\"CgstRate\" = \"SgstRate\" AND \"CgstRate\" + \"SgstRate\" = \"TotalRate\" AND \"IgstRate\" = \"TotalRate\"");
                });

            migrationBuilder.CreateTable(
                name: "TransactionRatios",
                schema: "acc",
                columns: table => new
                {
                    TransactionRatioId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    TargetTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TargetTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionRatios", x => x.TransactionRatioId);
                    table.CheckConstraint("chk_transactionratio_amount", "\"Amount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetCategories",
                schema: "acc",
                columns: table => new
                {
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    DepreciationExpenseAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetCategories", x => x.FixedAssetCategoryId);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AccumulatedDepreciationAccoun~",
                        column: x => x.AccumulatedDepreciationAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AssetAccountId",
                        column: x => x.AssetAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DepreciationExpenseAccountId",
                        column: x => x.DepreciationExpenseAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubAccounts",
                schema: "acc",
                columns: table => new
                {
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubAccounts", x => x.SubAccountId);
                    table.ForeignKey(
                        name: "FK_SubAccounts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                schema: "acc",
                columns: table => new
                {
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerAccountId = table.Column<long>(type: "bigint", nullable: true),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Ifsc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Micr = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OdLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.BankAccountId);
                    table.CheckConstraint("chk_bank_account_institution", "\"AccountType\" IN ('Cash', 'Wallet') OR \"BankId\" IS NOT NULL");
                    table.CheckConstraint("chk_bank_account_od_limit", "\"OdLimit\" IS NULL OR \"AccountType\" IN ('OverDraft', 'CashCredit', 'CreditCard')");
                    table.ForeignKey(
                        name: "FK_BankAccounts_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "acc",
                        principalTable: "Banks",
                        principalColumn: "BankId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalanceLines",
                schema: "acc",
                columns: table => new
                {
                    OpeningBalanceLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalanceLines", x => x.OpeningBalanceLineId);
                    table.CheckConstraint("chk_opening_line_exclusive", "\"LineType\" = 'Item' OR (\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
                    table.CheckConstraint("chk_opening_line_item_shape", "(\"LineType\" = 'Item' AND \"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"DebitAmount\" = 0 AND \"CreditAmount\" = 0) OR (\"LineType\" <> 'Item' AND \"Quantity\" IS NULL AND \"UnitCost\" IS NULL)");
                    table.CheckConstraint("chk_opening_line_names_its_subject", "(\"LineType\" = 'GlAccount' AND \"AccountId\" IS NOT NULL AND \"ContactId\" IS NULL AND \"ItemId\" IS NULL) OR (\"LineType\" IN ('ContactReceivable', 'ContactPayable') AND \"ContactId\" IS NOT NULL AND \"AccountId\" IS NULL AND \"ItemId\" IS NULL) OR (\"LineType\" = 'Item' AND \"ItemId\" IS NOT NULL AND \"AccountId\" IS NULL AND \"ContactId\" IS NULL)");
                    table.CheckConstraint("chk_opening_line_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                        column: x => x.OpeningBalanceId,
                        principalSchema: "acc",
                        principalTable: "OpeningBalances",
                        principalColumn: "OpeningBalanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                schema: "acc",
                columns: table => new
                {
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.FixedAssetId);
                    table.ForeignKey(
                        name: "FK_FixedAssets_FixedAssetCategories_FixedAssetCategoryId",
                        column: x => x.FixedAssetCategoryId,
                        principalSchema: "acc",
                        principalTable: "FixedAssetCategories",
                        principalColumn: "FixedAssetCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalDetails",
                schema: "acc",
                columns: table => new
                {
                    JournalDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: true),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebitAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReversesJournalDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByJournalDetailId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalDetails", x => x.JournalDetailId);
                    table.CheckConstraint("chk_journal_detail_exclusive", "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
                    table.CheckConstraint("chk_journal_detail_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_JournalDetails_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_JournalDetails_ReversedByJournalDetailId",
                        column: x => x.ReversedByJournalDetailId,
                        principalSchema: "acc",
                        principalTable: "JournalDetails",
                        principalColumn: "JournalDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_JournalDetails_ReversesJournalDetailId",
                        column: x => x.ReversesJournalDetailId,
                        principalSchema: "acc",
                        principalTable: "JournalDetails",
                        principalColumn: "JournalDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_Journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalDetails_SubAccounts_SubAccountId",
                        column: x => x.SubAccountId,
                        principalSchema: "acc",
                        principalTable: "SubAccounts",
                        principalColumn: "SubAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalLedger",
                schema: "acc",
                columns: table => new
                {
                    LedgerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LedgerDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionDetailId = table.Column<long>(type: "bigint", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebitAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    TaxExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerTypeId = table.Column<int>(type: "integer", nullable: false),
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionDesc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    JournalId = table.Column<long>(type: "bigint", nullable: true),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    BankStatementLineId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLedger", x => x.LedgerId);
                    table.CheckConstraint("chk_ledger_base_exclusive", "\"DebitAmountBase\" = 0 OR \"CreditAmountBase\" = 0");
                    table.CheckConstraint("chk_ledger_exclusive", "(\"DebitAmount\" = 0) <> (\"CreditAmount\" = 0)");
                    table.CheckConstraint("chk_ledger_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_JournalLedger_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalLedger_SubAccounts_SubAccountId",
                        column: x => x.SubAccountId,
                        principalSchema: "acc",
                        principalTable: "SubAccounts",
                        principalColumn: "SubAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatements",
                schema: "acc",
                columns: table => new
                {
                    BankStatementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    StatementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatements", x => x.BankStatementId);
                    table.CheckConstraint("chk_statement_period", "\"ToDate\" >= \"FromDate\"");
                    table.ForeignKey(
                        name: "FK_BankStatements_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiveMoney",
                schema: "acc",
                columns: table => new
                {
                    ReceiveMoneyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiveMoney", x => x.ReceiveMoneyId);
                    table.CheckConstraint("chk_receivemoney_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("chk_receivemoney_mapping_paired", "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
                    table.CheckConstraint("chk_receivemoney_number_on_post", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_receivemoney_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_receivemoney_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_receivemoney_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ReceiveMoney_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpendMoney",
                schema: "acc",
                columns: table => new
                {
                    SpendMoneyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendMoney", x => x.SpendMoneyId);
                    table.CheckConstraint("chk_spendmoney_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("chk_spendmoney_mapping_paired", "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
                    table.CheckConstraint("chk_spendmoney_number_on_post", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_spendmoney_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_spendmoney_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_spendmoney_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SpendMoney_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementImportProfiles",
                schema: "acc",
                columns: table => new
                {
                    StatementImportProfileId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SkipRows = table.Column<int>(type: "integer", nullable: false),
                    HasHeaderRow = table.Column<bool>(type: "boolean", nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DateColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueDateColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DescriptionColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WithdrawalColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepositColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AmountColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NegativeIsDeposit = table.Column<bool>(type: "boolean", nullable: false),
                    BalanceColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImportProfiles", x => x.StatementImportProfileId);
                    table.CheckConstraint("chk_import_profile_amount_shape", "(\"WithdrawalColumn\" IS NOT NULL AND \"DepositColumn\" IS NOT NULL AND \"AmountColumn\" IS NULL) OR (\"AmountColumn\" IS NOT NULL AND \"WithdrawalColumn\" IS NULL AND \"DepositColumn\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_StatementImportProfiles_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferMoney",
                schema: "acc",
                columns: table => new
                {
                    TransferMoneyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromBankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ToBankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferMoney", x => x.TransferMoneyId);
                    table.CheckConstraint("chk_transfer_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("chk_transfer_distinct_accounts", "\"ToBankAccountId\" <> \"FromBankAccountId\"");
                    table.CheckConstraint("chk_transfer_number_on_post", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_transfer_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_transfer_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_transfer_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TransferMoney_BankAccounts_FromBankAccountId",
                        column: x => x.FromBankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferMoney_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationSchedules",
                schema: "acc",
                columns: table => new
                {
                    DepreciationScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepreciationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    UsefulLifeYears = table.Column<int>(type: "integer", nullable: false),
                    DepreciationStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SalvageValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationSchedules", x => x.DepreciationScheduleId);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalSchema: "acc",
                        principalTable: "FixedAssets",
                        principalColumn: "FixedAssetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                schema: "acc",
                columns: table => new
                {
                    BankStatementLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankStatementId = table.Column<long>(type: "bigint", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WithdrawalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    RunningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MatchedTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MatchedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MatchedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RowHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.BankStatementLineId);
                    table.CheckConstraint("chk_statement_line_exclusive", "(\"WithdrawalAmount\" > 0 AND \"DepositAmount\" = 0) OR (\"DepositAmount\" > 0 AND \"WithdrawalAmount\" = 0)");
                    table.CheckConstraint("chk_statement_line_ignored_reason", "\"Status\" <> 'Ignored' OR \"Note\" IS NOT NULL");
                    table.CheckConstraint("chk_statement_line_match", "(\"Status\" = 'Matched' AND \"MatchedTransactionTypeCode\" IS NOT NULL AND \"MatchedTransactionId\" IS NOT NULL AND \"MatchedAt\" IS NOT NULL) OR (\"Status\" <> 'Matched' AND \"MatchedTransactionTypeCode\" IS NULL AND \"MatchedTransactionId\" IS NULL AND \"MatchedAt\" IS NULL)");
                    table.CheckConstraint("chk_statement_line_matched_type", "\"MatchedTransactionTypeCode\" IS NULL OR \"MatchedTransactionTypeCode\" IN ('SPM', 'RCM', 'TRM')");
                    table.CheckConstraint("chk_statement_line_non_negative", "\"WithdrawalAmount\" >= 0 AND \"DepositAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankStatements_BankStatementId",
                        column: x => x.BankStatementId,
                        principalSchema: "acc",
                        principalTable: "BankStatements",
                        principalColumn: "BankStatementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiveMoneyDetails",
                schema: "acc",
                columns: table => new
                {
                    ReceiveMoneyDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiveMoneyId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiveMoneyDetails", x => x.ReceiveMoneyDetailId);
                    table.CheckConstraint("chk_receivemoneydetail_amount_positive", "\"Amount\" > 0 AND \"AmountBase\" > 0");
                    table.CheckConstraint("chk_receivemoneydetail_mapping_paired", "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReceiveMoneyDetails_ReceiveMoney_ReceiveMoneyId",
                        column: x => x.ReceiveMoneyId,
                        principalSchema: "acc",
                        principalTable: "ReceiveMoney",
                        principalColumn: "ReceiveMoneyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpendMoneyDetails",
                schema: "acc",
                columns: table => new
                {
                    SpendMoneyDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpendMoneyId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendMoneyDetails", x => x.SpendMoneyDetailId);
                    table.CheckConstraint("chk_spendmoneydetail_amount_positive", "\"Amount\" > 0 AND \"AmountBase\" > 0");
                    table.CheckConstraint("chk_spendmoneydetail_mapping_paired", "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_SpendMoneyDetails_SpendMoney_SpendMoneyId",
                        column: x => x.SpendMoneyId,
                        principalSchema: "acc",
                        principalTable: "SpendMoney",
                        principalColumn: "SpendMoneyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransactions",
                schema: "acc",
                columns: table => new
                {
                    AssetTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepreciationScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    JournalId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTransactions", x => x.AssetTransactionId);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_DepreciationSchedules_DepreciationSchedul~",
                        column: x => x.DepreciationScheduleId,
                        principalSchema: "acc",
                        principalTable: "DepreciationSchedules",
                        principalColumn: "DepreciationScheduleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalSchema: "acc",
                        principalTable: "FixedAssets",
                        principalColumn: "FixedAssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_Journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Bank",
                schema: "acc",
                table: "Accounts",
                column: "OrgId",
                filter: "\"IsBank\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CustomerId_OrgId",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_AccountCode",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_AccountTypeId",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                schema: "acc",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Purchase",
                schema: "acc",
                table: "Accounts",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Sales",
                schema: "acc",
                table: "Accounts",
                column: "OrgId",
                filter: "\"IsSales\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SystemName",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountSystemName" },
                unique: true,
                filter: "\"AccountSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_CustomerId_OrgId",
                schema: "acc",
                table: "AssetTransactions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_DepreciationScheduleId",
                schema: "acc",
                table: "AssetTransactions",
                column: "DepreciationScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_FixedAssetId",
                schema: "acc",
                table: "AssetTransactions",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_JournalId",
                schema: "acc",
                table: "AssetTransactions",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_OrgId_FixedAssetId_TransactionDate",
                schema: "acc",
                table: "AssetTransactions",
                columns: new[] { "OrgId", "FixedAssetId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankId",
                schema: "acc",
                table: "BankAccounts",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "BankAccounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Default",
                schema: "acc",
                table: "BankAccounts",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Ledger",
                schema: "acc",
                table: "BankAccounts",
                columns: new[] { "OrgId", "LedgerAccountId" },
                unique: true,
                filter: "\"LedgerAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Order",
                schema: "acc",
                table: "BankAccounts",
                columns: new[] { "OrgId", "DisplayOrder", "AccountName" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_OrgId_BankId_AccountNumber",
                schema: "acc",
                table: "BankAccounts",
                columns: new[] { "OrgId", "BankId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankStatementId_LineNumber",
                schema: "acc",
                table: "BankStatementLines",
                columns: new[] { "BankStatementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatementLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrgId_BankAccountId_Status_TransactionDa~",
                schema: "acc",
                table: "BankStatementLines",
                columns: new[] { "OrgId", "BankAccountId", "Status", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_Row",
                schema: "acc",
                table: "BankStatementLines",
                columns: new[] { "OrgId", "BankAccountId", "RowHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_BankAccountId",
                schema: "acc",
                table: "BankStatements",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatements",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_OrgId_BankAccountId_FromDate",
                schema: "acc",
                table: "BankStatements",
                columns: new[] { "OrgId", "BankAccountId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_CustomerId_OrgId",
                schema: "acc",
                table: "Banks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Order",
                schema: "acc",
                table: "Banks",
                columns: new[] { "OrgId", "DisplayOrder", "BankName" });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_OrgId_BankCode",
                schema: "acc",
                table: "Banks",
                columns: new[] { "OrgId", "BankCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_CustomerId_OrgId",
                schema: "acc",
                table: "DepreciationSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_FixedAssetId",
                schema: "acc",
                table: "DepreciationSchedules",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_OrgId_FixedAssetId_ScheduleType",
                schema: "acc",
                table: "DepreciationSchedules",
                columns: new[] { "OrgId", "FixedAssetId", "ScheduleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AccumulatedDepreciationAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AssetAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "AssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssetCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DepreciationExpenseAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_OrgId_CategoryName",
                schema: "acc",
                table: "FixedAssetCategories",
                columns: new[] { "OrgId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_FixedAssetCategoryId",
                schema: "acc",
                table: "FixedAssets",
                column: "FixedAssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_OrgId_AssetCode",
                schema: "acc",
                table: "FixedAssets",
                columns: new[] { "OrgId", "AssetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_AccountId",
                schema: "acc",
                table: "JournalDetails",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_CustomerId_OrgId",
                schema: "acc",
                table: "JournalDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_JournalId_LineNumber",
                schema: "acc",
                table: "JournalDetails",
                columns: new[] { "JournalId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_ReversedByJournalDetailId",
                schema: "acc",
                table: "JournalDetails",
                column: "ReversedByJournalDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_ReversesJournalDetailId",
                schema: "acc",
                table: "JournalDetails",
                column: "ReversesJournalDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_SubAccountId",
                schema: "acc",
                table: "JournalDetails",
                column: "SubAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_AccountId",
                schema: "acc",
                table: "JournalLedger",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_CustomerId_OrgId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_Mapping",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_AccountId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "AccountId", "LedgerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_ContactId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "LedgerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_SubAccountId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "SubAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_SubAccountId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "SubAccountId", "LedgerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_Posting",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "TransactionTypeCode", "TransactionId", "TransactionDetailId", "LedgerTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_SubAccountId",
                schema: "acc",
                table: "JournalLedger",
                column: "SubAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_CustomerId_OrgId",
                schema: "acc",
                table: "Journals",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_Number",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "JournalNo" },
                unique: true,
                filter: "\"JournalNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId_JournalDate",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "JournalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId_TransactionTypeCode_SourceId",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "TransactionTypeCode", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_ReversedByJournalId",
                schema: "acc",
                table: "Journals",
                column: "ReversedByJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_ReversesJournalId",
                schema: "acc",
                table: "Journals",
                column: "ReversesJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_CustomerId_OrgId",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Default",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Order",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "DisplayOrder", "SeriesName" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_OrgId_SeriesName",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_SystemName",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesSystemName" },
                unique: true,
                filter: "\"SeriesSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_AccountId",
                schema: "acc",
                table: "OpeningBalanceLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OpeningBalanceId_LineNumber",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OpeningBalanceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId_ContactId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId_ItemId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_Org",
                schema: "acc",
                table: "OpeningBalances",
                column: "OrgId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_CustomerId_OrgId",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Default",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Order",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "DisplayOrder", "TermName" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_OrgId_TermName",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "TermName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Purchase",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Sales",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                filter: "\"IsSales\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_SystemName",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "TermSystemName" },
                unique: true,
                filter: "\"TermSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_CustomerId_OrgId",
                schema: "acc",
                table: "PeriodLocks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_OrgId_RoleId",
                schema: "acc",
                table: "PeriodLocks",
                columns: new[] { "OrgId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Account",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_BankAccountId",
                schema: "acc",
                table: "ReceiveMoney",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Mapping",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Number",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId_ContactId",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId_TransactionDate",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetail_Mapping",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_ReceiveMoneyId_LineNumber",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                columns: new[] { "ReceiveMoneyId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Account",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "OrgId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_BankAccountId",
                schema: "acc",
                table: "SpendMoney",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Mapping",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Number",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId_ContactId",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId_TransactionDate",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetail_Mapping",
                schema: "acc",
                table: "SpendMoneyDetails",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoneyDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_SpendMoneyId_LineNumber",
                schema: "acc",
                table: "SpendMoneyDetails",
                columns: new[] { "SpendMoneyId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_Account",
                schema: "acc",
                table: "StatementImportProfiles",
                columns: new[] { "OrgId", "BankAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_BankAccountId",
                schema: "acc",
                table: "StatementImportProfiles",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_CustomerId_OrgId",
                schema: "acc",
                table: "StatementImportProfiles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponen~",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "AccountId", "ReferenceType", "ReferenceId", "TaxComponent", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_OrgId_ReferenceType_ReferenceId",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "OrgId", "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_Purpose",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "OrgId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_CustomerId_OrgId",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_OrgId_EffectiveFrom_EffectiveTo",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "OrgId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_OrgId_TaxGroupId_EffectiveFrom",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "OrgId", "TaxGroupId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_Purchase",
                schema: "acc",
                table: "TaxMasters",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_Sales",
                schema: "acc",
                table: "TaxMasters",
                column: "OrgId",
                filter: "\"IsSales\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_CustomerId_OrgId",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId_SourceTransactionTypeCode_SourceTra~",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "OrgId", "SourceTransactionTypeCode", "SourceTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId_TargetTransactionTypeCode_TargetTra~",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "OrgId", "TargetTransactionTypeCode", "TargetTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_CustomerId_OrgId",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_From",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "OrgId", "FromBankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_FromBankAccountId",
                schema: "acc",
                table: "TransferMoney",
                column: "FromBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_Number",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_OrgId_TransactionDate",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_To",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "OrgId", "ToBankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_ToBankAccountId",
                schema: "acc",
                table: "TransferMoney",
                column: "ToBankAccountId");

            // Row-level security: the database-level half of tenant isolation,
            // independent of the EF query filter. No LINQ equivalent exists.
            foreach (string table in new[]
            {
                "Accounts",
                "SubAccounts",
                "TaxMasters",
                "PaymentTerms",
                "NumberingSeries",
                "Journals",
                "JournalDetails",
                "JournalLedger",
                "PeriodLocks",
                "OpeningBalances",
                "OpeningBalanceLines",
                "Banks",
                "BankAccounts",
                "SpendMoney",
                "SpendMoneyDetails",
                "ReceiveMoney",
                "ReceiveMoneyDetails",
                "TransferMoney",
                "BankStatements",
                "BankStatementLines",
                "StatementImportProfiles",
                "TransactionRatios",
                "FixedAssetCategories",
                "FixedAssets",
                "DepreciationSchedules",
                "AssetTransactions",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON acc.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }

            // Deferred constraint triggers: the third of the three balance checks
            // (domain guard on Post, SaveChangesInterceptor, this). Deferred so a
            // multi-line insert does not trip on intermediate imbalance, and only
            // meaningful once a document leaves Draft.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_ledger_balanced() RETURNS trigger AS $$
                DECLARE
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalLedger""
                     WHERE ""OrgId"" = NEW.""OrgId"";

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'The branch ledger does not balance: debits %, credits %',
                            debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_ledger_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc.""JournalLedger""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_ledger_balanced();
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced() RETURNS trigger AS $$
                DECLARE
                    journal bigint;
                    state varchar(10);
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        journal := OLD.""JournalId"";
                    ELSE
                        journal := NEW.""JournalId"";
                    END IF;

                    SELECT ""Status"" INTO state FROM acc.""Journals"" WHERE ""JournalId"" = journal;

                    IF state IS NULL OR state = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalDetails""
                     WHERE ""JournalId"" = journal;

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            journal, debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_journal_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc.""JournalDetails""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced();
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced_on_post() RETURNS trigger AS $$
                DECLARE
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF NEW.""Status"" = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalDetails""
                     WHERE ""JournalId"" = NEW.""JournalId"";

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            NEW.""JournalId"", debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_journal_balanced_on_post
                AFTER INSERT OR UPDATE ON acc.""Journals""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced_on_post();
                ");

            foreach (var doc in new[]
            {
                new { parent = "SpendMoney", child = "SpendMoneyDetails", key = "SpendMoneyId" },
                new { parent = "ReceiveMoney", child = "ReceiveMoneyDetails", key = "ReceiveMoneyId" }
            })
            {
                string fn = doc.parent.ToLowerInvariant();

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated() RETURNS trigger AS $$
                    DECLARE
                        doc bigint;
                        state varchar(10);
                        header numeric(18,2);
                        allocated numeric(18,2);
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            doc := OLD.""{doc.key}"";
                        ELSE
                            doc := NEW.""{doc.key}"";
                        END IF;

                        SELECT ""Status"", ""Amount"" INTO state, header
                          FROM acc.""{doc.parent}"" WHERE ""{doc.key}"" = doc;

                        IF state IS NULL OR state = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{doc.child}"" WHERE ""{doc.key}"" = doc;

                        IF allocated <> header THEN
                            RAISE EXCEPTION
                                '{doc.parent} % is allocated %, but its amount is %',
                                doc, allocated, header;
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON acc.\"{doc.child}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated
                    AFTER INSERT OR UPDATE OR DELETE ON acc.""{doc.child}""
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION acc.assert_{fn}_allocated();
                    ");

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated_on_post() RETURNS trigger AS $$
                    DECLARE
                        allocated numeric(18,2);
                    BEGIN
                        IF NEW.""Status"" = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{doc.child}"" WHERE ""{doc.key}"" = NEW.""{doc.key}"";

                        IF allocated <> NEW.""Amount"" THEN
                            RAISE EXCEPTION
                                '{doc.parent} % is allocated %, but its amount is %',
                                NEW.""{doc.key}"", allocated, NEW.""Amount"";
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON acc.\"{doc.parent}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated_on_post
                    AFTER INSERT OR UPDATE ON acc.""{doc.parent}""
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION acc.assert_{fn}_allocated_on_post();
                    ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetTransactions",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "BankStatementLines",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "JournalDetails",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "JournalLedger",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "NumberingSeries",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "OpeningBalanceLines",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "PaymentTerms",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "PeriodLocks",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ReceiveMoneyDetails",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SpendMoneyDetails",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "StatementImportProfiles",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TaxMasters",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TransactionRatios",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TransferMoney",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "DepreciationSchedules",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "BankStatements",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Journals",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SubAccounts",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "OpeningBalances",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ReceiveMoney",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SpendMoney",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "FixedAssets",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "BankAccounts",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "FixedAssetCategories",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Banks",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "acc");
        }
    }
}
