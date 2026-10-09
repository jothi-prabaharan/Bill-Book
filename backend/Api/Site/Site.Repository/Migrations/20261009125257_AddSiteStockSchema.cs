using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Site.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteStockSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteStockBalances",
                schema: "sit",
                columns: table => new
                {
                    BalanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    DispatchedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReturnedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UsedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OnSiteQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnbilledUsedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BilledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteStockBalances", x => x.BalanceId);
                    table.ForeignKey(
                        name: "FK_SiteStockBalances_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteStockDocuments",
                schema: "sit",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    SourceWarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SiteStockDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SiteStockDocuments_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteStockLines",
                schema: "sit",
                columns: table => new
                {
                    LineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MarginValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BilledQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteStockLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_SiteStockLines_SiteStockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "sit",
                        principalTable: "SiteStockDocuments",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockBalances_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockBalances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockBalances_CustomerId_OrgId_SiteId_ItemId",
                schema: "sit",
                table: "SiteStockBalances",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockBalances_SiteId",
                schema: "sit",
                table: "SiteStockBalances",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId_DocumentNo",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId_SiteId_DocumentType",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_SiteId",
                schema: "sit",
                table: "SiteStockDocuments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_CustomerId_OrgId_DocumentId",
                schema: "sit",
                table: "SiteStockLines",
                columns: new[] { "CustomerId", "OrgId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_DocumentId",
                schema: "sit",
                table: "SiteStockLines",
                column: "DocumentId");

            var tables = new[]
            {
                "SiteStockDocuments",
                "SiteStockLines",
                "SiteStockBalances",
            };

            foreach (var table in tables)
            {
                var policy = $"\"sit_{table.ToLowerInvariant()}_tenant_isolation\"";
                migrationBuilder.Sql($"""
                    ALTER TABLE sit."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sit."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sit."{table}";
                    CREATE POLICY {policy}
                        ON sit."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteStockBalances",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteStockLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteStockDocuments",
                schema: "sit");
        }
    }
}
