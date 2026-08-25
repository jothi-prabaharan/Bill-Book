using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Master
{
    /// <inheritdoc />
    public partial class SyncMenusVerticalAndDropCustomerDatabases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDatabases",
                schema: "mst");

            migrationBuilder.AddColumn<string>(
                name: "Vertical",
                schema: "mst",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Menus",
                schema: "mst",
                columns: table => new
                {
                    MenuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.MenuId);
                });

            migrationBuilder.CreateTable(
                name: "SubMenus",
                schema: "mst",
                columns: table => new
                {
                    SubMenuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoutePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubMenus", x => x.SubMenuId);
                    table.ForeignKey(
                        name: "FK_SubMenus_Menus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "mst",
                        principalTable: "Menus",
                        principalColumn: "MenuId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubMenuPermissions",
                schema: "mst",
                columns: table => new
                {
                    SubMenuPermissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubMenuId = table.Column<int>(type: "integer", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubMenuPermissions", x => x.SubMenuPermissionId);
                    table.ForeignKey(
                        name: "FK_SubMenuPermissions_SubMenus_SubMenuId",
                        column: x => x.SubMenuId,
                        principalSchema: "mst",
                        principalTable: "SubMenus",
                        principalColumn: "SubMenuId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "dashboard", null, null, 1, "home", true, null, null, "Dashboard" },
                    { 2, "sales", null, null, 2, "shopping_cart", true, null, null, "Sales" },
                    { 3, "purchase", null, null, 3, "inventory", true, null, null, "Purchase" },
                    { 4, "inventory", null, null, 4, "warehouse", true, null, null, "Inventory" },
                    { 5, "accounting", null, null, 5, "account_balance", true, null, null, "Accounts" },
                    { 6, "banking", null, null, 6, "account_balance_wallet", true, null, null, "Banking" },
                    { 7, "contacts", null, null, 7, "people", true, null, null, "Contacts" },
                    { 8, "reports", null, null, 8, "bar_chart", true, null, null, "Reports" },
                    { 9, "settings", null, null, 9, "settings", true, null, null, "Settings" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "SubMenus",
                columns: new[] { "SubMenuId", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "MenuId", "ModifiedAt", "ModifiedBy", "Name", "RoutePath" },
                values: new object[,]
                {
                    { 1, "dashboard", null, null, 1, "dashboard", true, 1, null, null, "Dashboard", "/dashboard" },
                    { 2, "sales_invoice", null, null, 1, "receipt", true, 2, null, null, "Invoice", "/sales/invoices" },
                    { 3, "sales_order", null, null, 2, "assignment", true, 2, null, null, "Sales Order", "/sales/orders" },
                    { 4, "sales_quote", null, null, 3, "description", true, 2, null, null, "Quote", "/sales/quotes" },
                    { 5, "sales_delivery_challan", null, null, 4, "local_shipping", true, 2, null, null, "Delivery Challan", "/sales/delivery-challans" },
                    { 6, "sales_credit_note", null, null, 5, "credit_card", true, 2, null, null, "Credit Note", "/sales/credit-notes" },
                    { 7, "sales_pos", null, null, 6, "point_of_sale", true, 2, null, null, "POS Sale", "/sales/pos" },
                    { 8, "purchase_bill", null, null, 1, "receipt_long", true, 3, null, null, "Bill", "/purchase/bills" },
                    { 9, "purchase_order", null, null, 2, "shopping_cart", true, 3, null, null, "Purchase Order", "/purchase/orders" },
                    { 10, "purchase_goods_receipt", null, null, 3, "inventory_2", true, 3, null, null, "Goods Receipt", "/purchase/goods-receipts" },
                    { 11, "purchase_debit_note", null, null, 4, "note", true, 3, null, null, "Debit Note", "/purchase/debit-notes" },
                    { 12, "inventory_items", null, null, 1, "inventory", true, 4, null, null, "Items", "/inventory/items" },
                    { 13, "inventory_categories", null, null, 2, "category", true, 4, null, null, "Categories", "/inventory/categories" },
                    { 14, "inventory_stock", null, null, 3, "storage", true, 4, null, null, "Stock", "/inventory/stock" },
                    { 15, "inventory_warehouses", null, null, 4, "warehouse", true, 4, null, null, "Warehouses", "/inventory/warehouses" },
                    { 16, "inventory_uom", null, null, 5, "straighten", true, 4, null, null, "Units of Measure", "/inventory/uom" },
                    { 17, "inventory_stock_adjustment", null, null, 6, "tune", true, 4, null, null, "Stock Adjustment", "/inventory/stock-adjustments" },
                    { 18, "accounting_chart", null, null, 1, "account_tree", true, 5, null, null, "Chart of Accounts", "/accounting/chart-of-accounts" },
                    { 19, "accounting_journal", null, null, 2, "edit_note", true, 5, null, null, "Journal Entries", "/accounting/journals" },
                    { 20, "accounting_ledger", null, null, 3, "ledger", true, 5, null, null, "Account Ledger", "/accounting/ledger" },
                    { 21, "accounting_trial_balance", null, null, 4, "balance", true, 5, null, null, "Trial Balance", "/accounting/trial-balance" },
                    { 22, "accounting_opening_balance", null, null, 5, "open_in_new", true, 5, null, null, "Opening Balance", "/accounting/opening-balance" },
                    { 23, "accounting_period_lock", null, null, 6, "lock", true, 5, null, null, "Period Locks", "/accounting/period-locks" },
                    { 24, "accounting_tax", null, null, 7, "receipt", true, 5, null, null, "Tax Master", "/accounting/tax-master" },
                    { 25, "accounting_payment_terms", null, null, 8, "schedule", true, 5, null, null, "Payment Terms", "/accounting/payment-terms" },
                    { 26, "accounting_numbering", null, null, 9, "format_list_numbered", true, 5, null, null, "Numbering Series", "/accounting/numbering-series" },
                    { 27, "banking_banks", null, null, 1, "business", true, 6, null, null, "Banks", "/banking/banks" },
                    { 28, "banking_accounts", null, null, 2, "account_balance", true, 6, null, null, "Bank Accounts", "/banking/accounts" },
                    { 29, "banking_spend_money", null, null, 3, "money_off", true, 6, null, null, "Spend Money", "/banking/spend-money" },
                    { 30, "banking_receive_money", null, null, 4, "monetization_on", true, 6, null, null, "Receive Money", "/banking/receive-money" },
                    { 31, "banking_transfer_money", null, null, 5, "swap_horiz", true, 6, null, null, "Transfer Money", "/banking/transfer-money" },
                    { 32, "banking_statements", null, null, 6, "description", true, 6, null, null, "Bank Statements", "/banking/statements" },
                    { 33, "contacts_list", null, null, 1, "person", true, 7, null, null, "Contacts", "/contacts" },
                    { 34, "reports_list", null, null, 1, "list", true, 8, null, null, "Reports", "/reports" },
                    { 35, "settings_organization", null, null, 1, "business", true, 9, null, null, "Organization", "/settings/organization" },
                    { 36, "settings_currencies", null, null, 2, "currency_exchange", true, 9, null, null, "Currencies", "/settings/currencies" },
                    { 37, "settings_smtp", null, null, 3, "email", true, 9, null, null, "Email Settings", "/settings/email" },
                    { 38, "settings_users", null, null, 4, "person_add", true, 9, null, null, "Users", "/settings/users" },
                    { 39, "settings_roles", null, null, 5, "security", true, 9, null, null, "Roles", "/settings/roles" },
                    { 40, "settings_branches", null, null, 6, "location_city", true, 9, null, null, "Branches", "/settings/branches" },
                    { 41, "settings_config", null, null, 7, "tune", true, 9, null, null, "Configuration", "/settings/configuration" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "SubMenuPermissions",
                columns: new[] { "SubMenuPermissionId", "Action", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode", "SubMenuId" },
                values: new object[,]
                {
                    { 1, "view", null, null, null, null, "dashboard", "dashboard.view", 1 },
                    { 2, "view", null, null, null, null, "sales", "sales.view", 2 },
                    { 3, "create", null, null, null, null, "sales", "sales.create", 2 },
                    { 4, "edit", null, null, null, null, "sales", "sales.edit", 2 },
                    { 5, "delete", null, null, null, null, "sales", "sales.delete", 2 },
                    { 6, "approve", null, null, null, null, "sales", "sales.approve", 2 },
                    { 7, "void", null, null, null, null, "sales", "sales.void", 2 },
                    { 8, "print", null, null, null, null, "sales", "sales.print", 2 },
                    { 9, "export", null, null, null, null, "sales", "sales.export", 2 },
                    { 10, "import", null, null, null, null, "sales", "sales.import", 2 },
                    { 11, "AllUserData", null, null, null, null, "sales", "sales.AllUserData", 2 },
                    { 12, "view", null, null, null, null, "sales", "sales.view", 3 },
                    { 13, "create", null, null, null, null, "sales", "sales.create", 3 },
                    { 14, "edit", null, null, null, null, "sales", "sales.edit", 3 },
                    { 15, "approve", null, null, null, null, "sales", "sales.approve", 3 },
                    { 16, "void", null, null, null, null, "sales", "sales.void", 3 },
                    { 17, "print", null, null, null, null, "sales", "sales.print", 3 },
                    { 18, "export", null, null, null, null, "sales", "sales.export", 3 },
                    { 19, "view", null, null, null, null, "sales", "sales.view", 4 },
                    { 20, "create", null, null, null, null, "sales", "sales.create", 4 },
                    { 21, "edit", null, null, null, null, "sales", "sales.edit", 4 },
                    { 22, "approve", null, null, null, null, "sales", "sales.approve", 4 },
                    { 23, "void", null, null, null, null, "sales", "sales.void", 4 },
                    { 24, "print", null, null, null, null, "sales", "sales.print", 4 },
                    { 25, "export", null, null, null, null, "sales", "sales.export", 4 },
                    { 26, "view", null, null, null, null, "sales", "sales.view", 5 },
                    { 27, "create", null, null, null, null, "sales", "sales.create", 5 },
                    { 28, "edit", null, null, null, null, "sales", "sales.edit", 5 },
                    { 29, "void", null, null, null, null, "sales", "sales.void", 5 },
                    { 30, "print", null, null, null, null, "sales", "sales.print", 5 },
                    { 31, "export", null, null, null, null, "sales", "sales.export", 5 },
                    { 32, "view", null, null, null, null, "sales", "sales.view", 6 },
                    { 33, "create", null, null, null, null, "sales", "sales.create", 6 },
                    { 34, "edit", null, null, null, null, "sales", "sales.edit", 6 },
                    { 35, "void", null, null, null, null, "sales", "sales.void", 6 },
                    { 36, "print", null, null, null, null, "sales", "sales.print", 6 },
                    { 37, "export", null, null, null, null, "sales", "sales.export", 6 },
                    { 38, "view", null, null, null, null, "sales", "sales.view", 7 },
                    { 39, "create", null, null, null, null, "sales", "sales.create", 7 },
                    { 40, "print", null, null, null, null, "sales", "sales.print", 7 },
                    { 41, "view", null, null, null, null, "purchase", "purchase.view", 8 },
                    { 42, "create", null, null, null, null, "purchase", "purchase.create", 8 },
                    { 43, "edit", null, null, null, null, "purchase", "purchase.edit", 8 },
                    { 44, "delete", null, null, null, null, "purchase", "purchase.delete", 8 },
                    { 45, "approve", null, null, null, null, "purchase", "purchase.approve", 8 },
                    { 46, "void", null, null, null, null, "purchase", "purchase.void", 8 },
                    { 47, "print", null, null, null, null, "purchase", "purchase.print", 8 },
                    { 48, "export", null, null, null, null, "purchase", "purchase.export", 8 },
                    { 49, "import", null, null, null, null, "purchase", "purchase.import", 8 },
                    { 50, "AllUserData", null, null, null, null, "purchase", "purchase.AllUserData", 8 },
                    { 51, "view", null, null, null, null, "purchase", "purchase.view", 9 },
                    { 52, "create", null, null, null, null, "purchase", "purchase.create", 9 },
                    { 53, "edit", null, null, null, null, "purchase", "purchase.edit", 9 },
                    { 54, "approve", null, null, null, null, "purchase", "purchase.approve", 9 },
                    { 55, "void", null, null, null, null, "purchase", "purchase.void", 9 },
                    { 56, "print", null, null, null, null, "purchase", "purchase.print", 9 },
                    { 57, "export", null, null, null, null, "purchase", "purchase.export", 9 },
                    { 58, "view", null, null, null, null, "purchase", "purchase.view", 10 },
                    { 59, "create", null, null, null, null, "purchase", "purchase.create", 10 },
                    { 60, "edit", null, null, null, null, "purchase", "purchase.edit", 10 },
                    { 61, "void", null, null, null, null, "purchase", "purchase.void", 10 },
                    { 62, "print", null, null, null, null, "purchase", "purchase.print", 10 },
                    { 63, "export", null, null, null, null, "purchase", "purchase.export", 10 },
                    { 64, "view", null, null, null, null, "purchase", "purchase.view", 11 },
                    { 65, "create", null, null, null, null, "purchase", "purchase.create", 11 },
                    { 66, "edit", null, null, null, null, "purchase", "purchase.edit", 11 },
                    { 67, "void", null, null, null, null, "purchase", "purchase.void", 11 },
                    { 68, "print", null, null, null, null, "purchase", "purchase.print", 11 },
                    { 69, "export", null, null, null, null, "purchase", "purchase.export", 11 },
                    { 70, "view", null, null, null, null, "inventory", "inventory.view", 12 },
                    { 71, "create", null, null, null, null, "inventory", "inventory.create", 12 },
                    { 72, "edit", null, null, null, null, "inventory", "inventory.edit", 12 },
                    { 73, "delete", null, null, null, null, "inventory", "inventory.delete", 12 },
                    { 74, "print", null, null, null, null, "inventory", "inventory.print", 12 },
                    { 75, "export", null, null, null, null, "inventory", "inventory.export", 12 },
                    { 76, "import", null, null, null, null, "inventory", "inventory.import", 12 },
                    { 77, "AllUserData", null, null, null, null, "inventory", "inventory.AllUserData", 12 },
                    { 78, "view", null, null, null, null, "inventory", "inventory.view", 13 },
                    { 79, "create", null, null, null, null, "inventory", "inventory.create", 13 },
                    { 80, "edit", null, null, null, null, "inventory", "inventory.edit", 13 },
                    { 81, "delete", null, null, null, null, "inventory", "inventory.delete", 13 },
                    { 82, "view", null, null, null, null, "inventory", "inventory.view", 14 },
                    { 83, "export", null, null, null, null, "inventory", "inventory.export", 14 },
                    { 84, "view", null, null, null, null, "inventory", "inventory.view", 15 },
                    { 85, "create", null, null, null, null, "inventory", "inventory.create", 15 },
                    { 86, "edit", null, null, null, null, "inventory", "inventory.edit", 15 },
                    { 87, "view", null, null, null, null, "inventory", "inventory.view", 16 },
                    { 88, "create", null, null, null, null, "inventory", "inventory.create", 16 },
                    { 89, "edit", null, null, null, null, "inventory", "inventory.edit", 16 },
                    { 90, "view", null, null, null, null, "inventory", "inventory.view", 17 },
                    { 91, "create", null, null, null, null, "inventory", "inventory.create", 17 },
                    { 92, "void", null, null, null, null, "inventory", "inventory.void", 17 },
                    { 93, "view", null, null, null, null, "accounting", "accounting.view", 18 },
                    { 94, "create", null, null, null, null, "accounting", "accounting.create", 18 },
                    { 95, "edit", null, null, null, null, "accounting", "accounting.edit", 18 },
                    { 96, "delete", null, null, null, null, "accounting", "accounting.delete", 18 },
                    { 97, "print", null, null, null, null, "accounting", "accounting.print", 18 },
                    { 98, "export", null, null, null, null, "accounting", "accounting.export", 18 },
                    { 99, "AllUserData", null, null, null, null, "accounting", "accounting.AllUserData", 18 },
                    { 100, "view", null, null, null, null, "accounting", "accounting.view", 19 },
                    { 101, "create", null, null, null, null, "accounting", "accounting.create", 19 },
                    { 102, "edit", null, null, null, null, "accounting", "accounting.edit", 19 },
                    { 103, "approve", null, null, null, null, "accounting", "accounting.approve", 19 },
                    { 104, "void", null, null, null, null, "accounting", "accounting.void", 19 },
                    { 105, "print", null, null, null, null, "accounting", "accounting.print", 19 },
                    { 106, "export", null, null, null, null, "accounting", "accounting.export", 19 },
                    { 107, "view", null, null, null, null, "accounting", "accounting.view", 20 },
                    { 108, "export", null, null, null, null, "accounting", "accounting.export", 20 },
                    { 109, "view", null, null, null, null, "accounting", "accounting.view", 21 },
                    { 110, "export", null, null, null, null, "accounting", "accounting.export", 21 },
                    { 111, "view", null, null, null, null, "accounting", "accounting.view", 22 },
                    { 112, "create", null, null, null, null, "accounting", "accounting.create", 22 },
                    { 113, "edit", null, null, null, null, "accounting", "accounting.edit", 22 },
                    { 114, "void", null, null, null, null, "accounting", "accounting.void", 22 },
                    { 115, "view", null, null, null, null, "accounting", "accounting.view", 23 },
                    { 116, "edit", null, null, null, null, "accounting", "accounting.edit", 23 },
                    { 117, "view", null, null, null, null, "accounting", "accounting.view", 24 },
                    { 118, "create", null, null, null, null, "accounting", "accounting.create", 24 },
                    { 119, "edit", null, null, null, null, "accounting", "accounting.edit", 24 },
                    { 120, "export", null, null, null, null, "accounting", "accounting.export", 24 },
                    { 121, "view", null, null, null, null, "accounting", "accounting.view", 25 },
                    { 122, "create", null, null, null, null, "accounting", "accounting.create", 25 },
                    { 123, "edit", null, null, null, null, "accounting", "accounting.edit", 25 },
                    { 124, "delete", null, null, null, null, "accounting", "accounting.delete", 25 },
                    { 125, "view", null, null, null, null, "accounting", "accounting.view", 26 },
                    { 126, "create", null, null, null, null, "accounting", "accounting.create", 26 },
                    { 127, "edit", null, null, null, null, "accounting", "accounting.edit", 26 },
                    { 128, "delete", null, null, null, null, "accounting", "accounting.delete", 26 },
                    { 129, "view", null, null, null, null, "banking", "banking.view", 27 },
                    { 130, "create", null, null, null, null, "banking", "banking.create", 27 },
                    { 131, "edit", null, null, null, null, "banking", "banking.edit", 27 },
                    { 132, "delete", null, null, null, null, "banking", "banking.delete", 27 },
                    { 133, "view", null, null, null, null, "banking", "banking.view", 28 },
                    { 134, "create", null, null, null, null, "banking", "banking.create", 28 },
                    { 135, "edit", null, null, null, null, "banking", "banking.edit", 28 },
                    { 136, "delete", null, null, null, null, "banking", "banking.delete", 28 },
                    { 137, "view", null, null, null, null, "banking", "banking.view", 29 },
                    { 138, "create", null, null, null, null, "banking", "banking.create", 29 },
                    { 139, "edit", null, null, null, null, "banking", "banking.edit", 29 },
                    { 140, "void", null, null, null, null, "banking", "banking.void", 29 },
                    { 141, "print", null, null, null, null, "banking", "banking.print", 29 },
                    { 142, "export", null, null, null, null, "banking", "banking.export", 29 },
                    { 143, "view", null, null, null, null, "banking", "banking.view", 30 },
                    { 144, "create", null, null, null, null, "banking", "banking.create", 30 },
                    { 145, "edit", null, null, null, null, "banking", "banking.edit", 30 },
                    { 146, "void", null, null, null, null, "banking", "banking.void", 30 },
                    { 147, "print", null, null, null, null, "banking", "banking.print", 30 },
                    { 148, "export", null, null, null, null, "banking", "banking.export", 30 },
                    { 149, "view", null, null, null, null, "banking", "banking.view", 31 },
                    { 150, "create", null, null, null, null, "banking", "banking.create", 31 },
                    { 151, "void", null, null, null, null, "banking", "banking.void", 31 },
                    { 152, "view", null, null, null, null, "banking", "banking.view", 32 },
                    { 153, "create", null, null, null, null, "banking", "banking.create", 32 },
                    { 154, "edit", null, null, null, null, "banking", "banking.edit", 32 },
                    { 155, "print", null, null, null, null, "banking", "banking.print", 32 },
                    { 156, "export", null, null, null, null, "banking", "banking.export", 32 },
                    { 157, "view", null, null, null, null, "contacts", "contacts.view", 33 },
                    { 158, "create", null, null, null, null, "contacts", "contacts.create", 33 },
                    { 159, "edit", null, null, null, null, "contacts", "contacts.edit", 33 },
                    { 160, "delete", null, null, null, null, "contacts", "contacts.delete", 33 },
                    { 161, "export", null, null, null, null, "contacts", "contacts.export", 33 },
                    { 162, "AllUserData", null, null, null, null, "contacts", "contacts.AllUserData", 33 },
                    { 163, "view", null, null, null, null, "reports", "reports.view", 34 },
                    { 164, "export", null, null, null, null, "reports", "reports.export", 34 },
                    { 165, "AllUserData", null, null, null, null, "reports", "reports.AllUserData", 34 },
                    { 166, "view", null, null, null, null, "settings", "settings.view", 35 },
                    { 167, "edit", null, null, null, null, "settings", "settings.edit", 35 },
                    { 168, "view", null, null, null, null, "settings", "settings.view", 36 },
                    { 169, "edit", null, null, null, null, "settings", "settings.edit", 36 },
                    { 170, "view", null, null, null, null, "settings", "settings.view", 37 },
                    { 171, "edit", null, null, null, null, "settings", "settings.edit", 37 },
                    { 172, "view", null, null, null, null, "settings", "settings.view", 38 },
                    { 173, "create", null, null, null, null, "settings", "settings.create", 38 },
                    { 174, "edit", null, null, null, null, "settings", "settings.edit", 38 },
                    { 175, "delete", null, null, null, null, "settings", "settings.delete", 38 },
                    { 176, "view", null, null, null, null, "settings", "settings.view", 39 },
                    { 177, "create", null, null, null, null, "settings", "settings.create", 39 },
                    { 178, "edit", null, null, null, null, "settings", "settings.edit", 39 },
                    { 179, "delete", null, null, null, null, "settings", "settings.delete", 39 },
                    { 180, "view", null, null, null, null, "settings", "settings.view", 40 },
                    { 181, "create", null, null, null, null, "settings", "settings.create", 40 },
                    { 182, "edit", null, null, null, null, "settings", "settings.edit", 40 },
                    { 183, "view", null, null, null, null, "settings", "settings.view", 41 },
                    { 184, "edit", null, null, null, null, "settings", "settings.edit", 41 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Menus_Code",
                schema: "mst",
                table: "Menus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubMenuPermissions_SubMenuId_PermissionCode",
                schema: "mst",
                table: "SubMenuPermissions",
                columns: new[] { "SubMenuId", "PermissionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubMenus_MenuId_Code",
                schema: "mst",
                table: "SubMenus",
                columns: new[] { "MenuId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubMenuPermissions",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "SubMenus",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Menus",
                schema: "mst");

            migrationBuilder.DropColumn(
                name: "Vertical",
                schema: "mst",
                table: "Organizations");

            migrationBuilder.CreateTable(
                name: "CustomerDatabases",
                schema: "mst",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionSecretRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DatabaseName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvisionedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDatabases", x => x.CustomerId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDatabases_DatabaseName",
                schema: "mst",
                table: "CustomerDatabases",
                column: "DatabaseName",
                unique: true);
        }
    }
}
