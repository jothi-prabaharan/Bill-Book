using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RecipesAndWastage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "inv",
                columns: table => new
                {
                    RecipeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Yield = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.CheckConstraint("chk_recipe_yield", "\"Yield\" > 0");
                    table.ForeignKey(
                        name: "FK_Recipes_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WastageEntries",
                schema: "inv",
                columns: table => new
                {
                    WastageEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_WastageEntries", x => x.WastageEntryId);
                    table.CheckConstraint("chk_wastage_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_WastageEntries_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WastageEntries_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WastageEntries_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeLines",
                schema: "inv",
                columns: table => new
                {
                    RecipeLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<long>(type: "bigint", nullable: false),
                    IngredientItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_RecipeLines", x => x.RecipeLineId);
                    table.CheckConstraint("chk_recipe_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RecipeLines_Items_IngredientItemId",
                        column: x => x.IngredientItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeLines_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "inv",
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeLines_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_CustomerId_OrgId",
                schema: "inv",
                table: "RecipeLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_IngredientItemId",
                schema: "inv",
                table: "RecipeLines",
                column: "IngredientItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_RecipeId_IngredientItemId",
                schema: "inv",
                table: "RecipeLines",
                columns: new[] { "RecipeId", "IngredientItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_UomId",
                schema: "inv",
                table: "RecipeLines",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CustomerId_OrgId",
                schema: "inv",
                table: "Recipes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_MenuItemId",
                schema: "inv",
                table: "Recipes",
                column: "MenuItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_CustomerId_OrgId",
                schema: "inv",
                table: "WastageEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_ItemId",
                schema: "inv",
                table: "WastageEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_OrgId_EntryDate",
                schema: "inv",
                table: "WastageEntries",
                columns: new[] { "OrgId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_StockAdjustmentId",
                schema: "inv",
                table: "WastageEntries",
                column: "StockAdjustmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_UomId",
                schema: "inv",
                table: "WastageEntries",
                column: "UomId");

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "inv", "Recipes");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "RecipeLines");
            TenantRlsMigration.Enable(migrationBuilder, "inv", "WastageEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeLines",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "WastageEntries",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "inv");
        }
    }
}
