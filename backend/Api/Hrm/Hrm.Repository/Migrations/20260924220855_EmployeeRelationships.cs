using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hrm.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelationshipTypes",
                schema: "hrm",
                columns: table => new
                {
                    RelationshipTypeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_RelationshipTypes", x => x.RelationshipTypeId);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeRelationships",
                schema: "hrm",
                columns: table => new
                {
                    EmployeeRelationshipId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    RelationshipTypeId = table.Column<long>(type: "bigint", nullable: false),
                    RelatedEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeRelationships", x => x.EmployeeRelationshipId);
                    table.ForeignKey(
                        name: "FK_EmployeeRelationships_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hrm",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeRelationships_Employees_RelatedEmployeeId",
                        column: x => x.RelatedEmployeeId,
                        principalSchema: "hrm",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeRelationships_RelationshipTypes_RelationshipTypeId",
                        column: x => x.RelationshipTypeId,
                        principalSchema: "hrm",
                        principalTable: "RelationshipTypes",
                        principalColumn: "RelationshipTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRelationships_CustomerId_OrgId",
                schema: "hrm",
                table: "EmployeeRelationships",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRelationships_EmployeeId_RelationshipTypeId_FromDate",
                schema: "hrm",
                table: "EmployeeRelationships",
                columns: new[] { "EmployeeId", "RelationshipTypeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRelationships_RelatedEmployeeId",
                schema: "hrm",
                table: "EmployeeRelationships",
                column: "RelatedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRelationships_RelationshipTypeId",
                schema: "hrm",
                table: "EmployeeRelationships",
                column: "RelationshipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipTypes_CustomerId_OrgId",
                schema: "hrm",
                table: "RelationshipTypes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipTypes_CustomerId_OrgId_Code",
                schema: "hrm",
                table: "RelationshipTypes",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            // The same policy as every other hrm table (TK-48): ENABLE, FORCE,
            // one policy on CustomerId and OrgId, NULLIF for "no tenant".
            foreach (string table in new[] { "RelationshipTypes", "EmployeeRelationships" })
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE hrm."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE hrm."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON hrm."{table}";
                    CREATE POLICY {policy}
                        ON hrm."{table}"
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
                name: "EmployeeRelationships",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "RelationshipTypes",
                schema: "hrm");
        }
    }
}
