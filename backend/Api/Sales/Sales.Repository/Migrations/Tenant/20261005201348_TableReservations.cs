using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Shared.Kernel.Persistence;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class TableReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TableReservations",
                schema: "sal",
                columns: table => new
                {
                    TableReservationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    ReservedFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SeatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_TableReservations", x => x.TableReservationId);
                    table.CheckConstraint("chk_table_reservation_duration", "\"DurationMinutes\" BETWEEN 15 AND 720");
                    table.CheckConstraint("chk_table_reservation_party", "\"PartySize\" BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_TableReservations_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableReservations_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaitingListEntries",
                schema: "sal",
                columns: table => new
                {
                    WaitingListEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_WaitingListEntries", x => x.WaitingListEntryId);
                    table.CheckConstraint("chk_waiting_list_party", "\"PartySize\" BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_WaitingListEntries_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitingListEntries_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_CustomerId_OrgId",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_OrgId_ReservedFor",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "OrgId", "ReservedFor" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_OrgId_RestaurantTableId_Status_ReservedFor",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "OrgId", "RestaurantTableId", "Status", "ReservedFor" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_RestaurantOrderId",
                schema: "sal",
                table: "TableReservations",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_RestaurantTableId",
                schema: "sal",
                table: "TableReservations",
                column: "RestaurantTableId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_CustomerId_OrgId",
                schema: "sal",
                table: "WaitingListEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_OrgId_Status_JoinedAt",
                schema: "sal",
                table: "WaitingListEntries",
                columns: new[] { "OrgId", "Status", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_RestaurantOrderId",
                schema: "sal",
                table: "WaitingListEntries",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_RestaurantTableId",
                schema: "sal",
                table: "WaitingListEntries",
                column: "RestaurantTableId");

            // Row-level security on both new tables (the TK-71 template), and on
            // sal.AggregatorOrders, whose own migration (TK-167) left it out.
            TenantRlsMigration.Enable(migrationBuilder, "sal", "TableReservations");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "WaitingListEntries");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "AggregatorOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TenantRlsMigration.Disable(migrationBuilder, "sal", "AggregatorOrders");

            migrationBuilder.DropTable(
                name: "TableReservations",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "WaitingListEntries",
                schema: "sal");
        }
    }
}
