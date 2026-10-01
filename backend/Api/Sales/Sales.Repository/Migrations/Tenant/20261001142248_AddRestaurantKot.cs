using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRestaurantKot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE sal.\"Kots\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON sal.\"Kots\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE sal.\"KotLines\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON sal.\"KotLines\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.CreateTable(
                name: "KotLines",
                schema: "sal",
                columns: table => new
                {
                    KotLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KotId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantOrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_KotLines", x => x.KotLineId);
                });

            migrationBuilder.CreateTable(
                name: "Kots",
                schema: "sal",
                columns: table => new
                {
                    KotId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Station = table.Column<int>(type: "integer", nullable: false),
                    KotNumber = table.Column<string>(type: "text", nullable: false),
                    PrintedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Kots", x => x.KotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_CustomerId_OrgId",
                schema: "sal",
                table: "KotLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Kots_CustomerId_OrgId",
                schema: "sal",
                table: "Kots",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON sal.\"Kots\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON sal.\"KotLines\";");
            migrationBuilder.DropTable(
                name: "KotLines",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Kots",
                schema: "sal");
        }
    }
}

