using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RestaurantMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuCategories",
                schema: "inv",
                columns: table => new
                {
                    MenuCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_MenuCategories", x => x.MenuCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "MenuSchedules",
                schema: "inv",
                columns: table => new
                {
                    MenuScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
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
                    table.PrimaryKey("PK_MenuSchedules", x => x.MenuScheduleId);
                    table.CheckConstraint("chk_menu_schedule_days", "\"Days\" > 0 AND \"Days\" < 128");
                    table.CheckConstraint("chk_menu_schedule_times", "\"StartTime\" <> \"EndTime\"");
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "inv",
                columns: table => new
                {
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    MenuCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    MenuScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_MenuItems", x => x.MenuItemId);
                    table.ForeignKey(
                        name: "FK_MenuItems_Items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalSchema: "inv",
                        principalTable: "MenuCategories",
                        principalColumn: "MenuCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuSchedules_MenuScheduleId",
                        column: x => x.MenuScheduleId,
                        principalSchema: "inv",
                        principalTable: "MenuSchedules",
                        principalColumn: "MenuScheduleId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItemVariants",
                schema: "inv",
                columns: table => new
                {
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PortionFactor = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
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
                    table.PrimaryKey("PK_ItemVariants", x => x.ItemVariantId);
                    table.CheckConstraint("chk_item_variant_portion", "\"PortionFactor\" > 0");
                    table.CheckConstraint("chk_item_variant_price", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_ItemVariants_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                schema: "inv",
                columns: table => new
                {
                    ModifierGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinChoices = table.Column<int>(type: "integer", nullable: false),
                    MaxChoices = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ModifierGroups", x => x.ModifierGroupId);
                    table.CheckConstraint("chk_modifier_group_choices", "\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\"");
                    table.ForeignKey(
                        name: "FK_ModifierGroups_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemPrices",
                schema: "inv",
                columns: table => new
                {
                    MenuItemPriceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: true),
                    OrderType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_MenuItemPrices", x => x.MenuItemPriceId);
                    table.CheckConstraint("chk_menu_item_price", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_MenuItemPrices_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalSchema: "inv",
                        principalTable: "ItemVariants",
                        principalColumn: "ItemVariantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemPrices_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modifiers",
                schema: "inv",
                columns: table => new
                {
                    ModifierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModifierGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkedItemId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Modifiers", x => x.ModifierId);
                    table.CheckConstraint("chk_modifier_linked_quantity", "(\"LinkedItemId\" IS NULL) = (\"LinkedQuantity\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Modifiers_Items_LinkedItemId",
                        column: x => x.LinkedItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Modifiers_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalSchema: "inv",
                        principalTable: "ModifierGroups",
                        principalColumn: "ModifierGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_CustomerId_OrgId",
                schema: "inv",
                table: "ItemVariants",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_MenuItemId_Name",
                schema: "inv",
                table: "ItemVariants",
                columns: new[] { "MenuItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_CustomerId_OrgId",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_OrgId_DisplayOrder",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "OrgId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_OrgId_Name",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_CustomerId_OrgId",
                schema: "inv",
                table: "MenuItemPrices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_ItemVariantId",
                schema: "inv",
                table: "MenuItemPrices",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_MenuItemId_ItemVariantId_OrderType",
                schema: "inv",
                table: "MenuItemPrices",
                columns: new[] { "MenuItemId", "ItemVariantId", "OrderType" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CustomerId_OrgId",
                schema: "inv",
                table: "MenuItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuCategoryId",
                schema: "inv",
                table: "MenuItems",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuScheduleId",
                schema: "inv",
                table: "MenuItems",
                column: "MenuScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_OrgId_MenuCategoryId_DisplayOrder",
                schema: "inv",
                table: "MenuItems",
                columns: new[] { "OrgId", "MenuCategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_CustomerId_OrgId",
                schema: "inv",
                table: "MenuSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_OrgId_Name",
                schema: "inv",
                table: "MenuSchedules",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_CustomerId_OrgId",
                schema: "inv",
                table: "ModifierGroups",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_MenuItemId_Name",
                schema: "inv",
                table: "ModifierGroups",
                columns: new[] { "MenuItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_CustomerId_OrgId",
                schema: "inv",
                table: "Modifiers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_LinkedItemId",
                schema: "inv",
                table: "Modifiers",
                column: "LinkedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_ModifierGroupId_Name",
                schema: "inv",
                table: "Modifiers",
                columns: new[] { "ModifierGroupId", "Name" },
                unique: true);

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "inv", "MenuCategories");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "MenuSchedules");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "MenuItems");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "ItemVariants");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "ModifierGroups");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "MenuItemPrices");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "Modifiers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemPrices",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Modifiers",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemVariants",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ModifierGroups",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuCategories",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuSchedules",
                schema: "inv");
        }
    }
}
