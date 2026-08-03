using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Banking.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiveMoney",
                schema: "bnk",
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
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpendMoney",
                schema: "bnk",
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
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferMoney",
                schema: "bnk",
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
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferMoney_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiveMoneyDetails",
                schema: "bnk",
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
                        principalSchema: "bnk",
                        principalTable: "ReceiveMoney",
                        principalColumn: "ReceiveMoneyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpendMoneyDetails",
                schema: "bnk",
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
                        principalSchema: "bnk",
                        principalTable: "SpendMoney",
                        principalColumn: "SpendMoneyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Account",
                schema: "bnk",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_BankAccountId",
                schema: "bnk",
                table: "ReceiveMoney",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Mapping",
                schema: "bnk",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_Number",
                schema: "bnk",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId",
                schema: "bnk",
                table: "ReceiveMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId_ContactId",
                schema: "bnk",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId_TransactionDate",
                schema: "bnk",
                table: "ReceiveMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetail_Mapping",
                schema: "bnk",
                table: "ReceiveMoneyDetails",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_OrgId",
                schema: "bnk",
                table: "ReceiveMoneyDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_ReceiveMoneyId_LineNumber",
                schema: "bnk",
                table: "ReceiveMoneyDetails",
                columns: new[] { "ReceiveMoneyId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Account",
                schema: "bnk",
                table: "SpendMoney",
                columns: new[] { "OrgId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_BankAccountId",
                schema: "bnk",
                table: "SpendMoney",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Mapping",
                schema: "bnk",
                table: "SpendMoney",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_Number",
                schema: "bnk",
                table: "SpendMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId",
                schema: "bnk",
                table: "SpendMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId_ContactId",
                schema: "bnk",
                table: "SpendMoney",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId_TransactionDate",
                schema: "bnk",
                table: "SpendMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetail_Mapping",
                schema: "bnk",
                table: "SpendMoneyDetails",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_OrgId",
                schema: "bnk",
                table: "SpendMoneyDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_SpendMoneyId_LineNumber",
                schema: "bnk",
                table: "SpendMoneyDetails",
                columns: new[] { "SpendMoneyId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_From",
                schema: "bnk",
                table: "TransferMoney",
                columns: new[] { "OrgId", "FromBankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_FromBankAccountId",
                schema: "bnk",
                table: "TransferMoney",
                column: "FromBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_Number",
                schema: "bnk",
                table: "TransferMoney",
                columns: new[] { "OrgId", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_OrgId",
                schema: "bnk",
                table: "TransferMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_OrgId_TransactionDate",
                schema: "bnk",
                table: "TransferMoney",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_To",
                schema: "bnk",
                table: "TransferMoney",
                columns: new[] { "OrgId", "ToBankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_ToBankAccountId",
                schema: "bnk",
                table: "TransferMoney",
                column: "ToBankAccountId");
            // The lines have to add up to the header, on both sides that have
            // lines. Not a check constraint: it spans rows, so it has to be a
            // trigger — and one pair per document, because three tables mean
            // three parent/child relationships rather than one.
            //
            // DEFERRABLE INITIALLY DEFERRED, because a document is several lines
            // and only adds up once all of them are in.
            //
            // **Posted documents only.** A draft is allowed not to add up — that
            // is what a draft is for. Someone allocating a payment across nine
            // bills is short for eight of them.
            foreach ((string parent, string child, string key) in new[]
            {
                ("SpendMoney", "SpendMoneyDetails", "SpendMoneyId"),
                ("ReceiveMoney", "ReceiveMoneyDetails", "ReceiveMoneyId"),
            })
            {
                string fn = parent.ToLowerInvariant();

                migrationBuilder.Sql($"""
                    CREATE OR REPLACE FUNCTION bnk.assert_{fn}_allocated() RETURNS trigger AS $$
                    DECLARE
                        doc bigint;
                        state varchar(10);
                        header numeric(18,2);
                        allocated numeric(18,2);
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            doc := OLD."{key}";
                        ELSE
                            doc := NEW."{key}";
                        END IF;

                        SELECT "Status", "Amount" INTO state, header
                          FROM bnk."{parent}" WHERE "{key}" = doc;

                        -- The header is gone: the document was deleted and its
                        -- lines cascaded. Nothing left to reconcile.
                        IF state IS NULL OR state = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM("Amount"), 0) INTO allocated
                          FROM bnk."{child}" WHERE "{key}" = doc;

                        IF allocated <> header THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount is %',
                                doc, allocated, header;
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    """);

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON bnk.\"{child}\";");

                migrationBuilder.Sql($"""
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated
                    AFTER INSERT OR UPDATE OR DELETE ON bnk."{child}"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION bnk.assert_{fn}_allocated();
                    """);

                // Posting is a header update and the lines do not move, so the
                // line trigger never fires for it. Without this second one, a
                // draft whose allocation is short could be posted by flipping its
                // status — the one path that matters most. The same pair
                // acc.Journals carries, for the same reason.
                migrationBuilder.Sql($"""
                    CREATE OR REPLACE FUNCTION bnk.assert_{fn}_allocated_on_post() RETURNS trigger AS $$
                    DECLARE
                        allocated numeric(18,2);
                    BEGIN
                        IF NEW."Status" = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM("Amount"), 0) INTO allocated
                          FROM bnk."{child}" WHERE "{key}" = NEW."{key}";

                        IF allocated <> NEW."Amount" THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount is %',
                                NEW."{key}", allocated, NEW."Amount";
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    """);

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON bnk.\"{parent}\";");

                migrationBuilder.Sql($"""
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated_on_post
                    AFTER INSERT OR UPDATE ON bnk."{parent}"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION bnk.assert_{fn}_allocated_on_post();
                    """);
            }

            // Row-level security, as on every other per-customer table. The EF
            // query filter is the first line of defence, not the last.
            foreach (string table in new[]
            {
                "SpendMoney", "SpendMoneyDetails",
                "ReceiveMoney", "ReceiveMoneyDetails",
                "TransferMoney",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE bnk.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "SpendMoney", "SpendMoneyDetails",
                "ReceiveMoney", "ReceiveMoneyDetails",
                "TransferMoney",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\";");
            }

            foreach ((string parent, string child) in new[]
            {
                ("SpendMoney", "SpendMoneyDetails"),
                ("ReceiveMoney", "ReceiveMoneyDetails"),
            })
            {
                string fn = parent.ToLowerInvariant();
                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON bnk.\"{parent}\";");
                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON bnk.\"{child}\";");
            }
            migrationBuilder.DropTable(
                name: "ReceiveMoneyDetails",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "SpendMoneyDetails",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "TransferMoney",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "ReceiveMoney",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "SpendMoney",
                schema: "bnk");

            foreach (string fn in new[] { "spendmoney", "receivemoney" })
            {
                migrationBuilder.Sql($"DROP FUNCTION IF EXISTS bnk.assert_{fn}_allocated_on_post();");
                migrationBuilder.Sql($"DROP FUNCTION IF EXISTS bnk.assert_{fn}_allocated();");
            }
        }
    }
}
