using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRestaurantOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE sal.\"RestaurantOrders\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON sal.\"RestaurantOrders\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE sal.\"RestaurantOrderDetails\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON sal.\"RestaurantOrderDetails\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.CreateTable(
                name: "RestaurantOrderDetails",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    VariantId = table.Column<long>(type: "bigint", nullable: true),
                    ModifierIds = table.Column<long[]>(type: "bigint[]", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_RestaurantOrderDetails", x => x.RestaurantOrderDetailId);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrders",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    OrderType = table.Column<int>(type: "integer", nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    WaiterUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_RestaurantOrders", x => x.RestaurantOrderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderDetails_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrderDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON sal.\"RestaurantOrders\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON sal.\"RestaurantOrderDetails\";");
            migrationBuilder.DropTable(
                name: "RestaurantOrderDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrders",
                schema: "sal");
        }
    }
}

