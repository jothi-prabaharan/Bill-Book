using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mst");

            migrationBuilder.CreateTable(
                name: "AccountTypes",
                schema: "mst",
                columns: table => new
                {
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalBalance = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ReportSection = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.AccountTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "mst",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PhoneCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.CountryId);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "mst",
                columns: table => new
                {
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    SymbolPosition = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.CurrencyId);
                });

            migrationBuilder.CreateTable(
                name: "HsnSacCodes",
                schema: "mst",
                columns: table => new
                {
                    HsnSacCodeId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CodeType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ChapterCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DefaultGstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DigitLength = table.Column<byte>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsnSacCodes", x => x.HsnSacCodeId);
                });

            migrationBuilder.CreateTable(
                name: "LedgerSources",
                schema: "mst",
                columns: table => new
                {
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerSources", x => x.LedgerSourceId);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTypes",
                schema: "mst",
                columns: table => new
                {
                    LedgerTypeId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTypes", x => x.LedgerTypeId);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTypes",
                schema: "mst",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsLedgerPosting = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "States",
                schema: "mst",
                columns: table => new
                {
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    StateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.StateId);
                    table.ForeignKey(
                        name: "FK_States_Countries_CountryId",
                        column: x => x.CountryId,
                        principalSchema: "mst",
                        principalTable: "Countries",
                        principalColumn: "CountryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "AccountTypes",
                columns: new[] { "AccountTypeId", "CreatedAt", "CreatedBy", "DisplayName", "IsActive", "ModifiedAt", "ModifiedBy", "NormalBalance", "ReportSection", "SortOrder", "SystemName" },
                values: new object[,]
                {
                    { 1, null, null, "Asset", true, null, null, "Debit", "BalanceSheet", (short)1, "Asset" },
                    { 2, null, null, "Liability", true, null, null, "Credit", "BalanceSheet", (short)2, "Liability" },
                    { 3, null, null, "Equity", true, null, null, "Credit", "BalanceSheet", (short)3, "Equity" },
                    { 4, null, null, "Income", true, null, null, "Credit", "ProfitAndLoss", (short)4, "Income" },
                    { 5, null, null, "Expense", true, null, null, "Debit", "ProfitAndLoss", (short)5, "Expense" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Countries",
                columns: new[] { "CountryId", "CountryCode", "CountryName", "CreatedAt", "CreatedBy", "CurrencyCode", "IsActive", "ModifiedAt", "ModifiedBy", "PhoneCode" },
                values: new object[,]
                {
                    { 1, "IN", "India", null, null, "INR", true, null, null, "+91" },
                    { 2, "US", "United States", null, null, "USD", true, null, null, "+1" },
                    { 3, "GB", "United Kingdom", null, null, "GBP", true, null, null, "+44" },
                    { 4, "AE", "United Arab Emirates", null, null, "AED", true, null, null, "+971" },
                    { 5, "SG", "Singapore", null, null, "SGD", true, null, null, "+65" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Currencies",
                columns: new[] { "CurrencyId", "Code", "CreatedAt", "CreatedBy", "DecimalPlaces", "Format", "IsActive", "ModifiedAt", "ModifiedBy", "Name", "Symbol", "SymbolPosition" },
                values: new object[,]
                {
                    { 1, "INR", null, null, 2, "##,##,##0.00", true, null, null, "Indian Rupee", "₹", "Prefix" },
                    { 2, "USD", null, null, 2, "###,###,##0.00", true, null, null, "US Dollar", "$", "Prefix" },
                    { 3, "GBP", null, null, 2, "###,###,##0.00", true, null, null, "Pound Sterling", "£", "Prefix" },
                    { 4, "AED", null, null, 2, "###,###,##0.00", true, null, null, "UAE Dirham", "د.إ", "Prefix" },
                    { 5, "SGD", null, null, 2, "###,###,##0.00", true, null, null, "Singapore Dollar", "S$", "Prefix" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "HsnSacCodes",
                columns: new[] { "HsnSacCodeId", "ChapterCode", "Code", "CodeType", "CreatedAt", "CreatedBy", "DefaultGstRate", "Description", "DigitLength", "IsActive", "ModifiedAt", "ModifiedBy" },
                values: new object[,]
                {
                    { 1, "01", "01", "Hsn", null, null, null, "Live animals", (byte)2, true, null, null },
                    { 2, "02", "02", "Hsn", null, null, null, "Meat and edible meat offal", (byte)2, true, null, null },
                    { 3, "03", "03", "Hsn", null, null, null, "Fish and crustaceans, molluscs and other aquatic invertebrates", (byte)2, true, null, null },
                    { 4, "04", "04", "Hsn", null, null, null, "Dairy produce; birds' eggs; natural honey; edible products of animal origin", (byte)2, true, null, null },
                    { 5, "05", "05", "Hsn", null, null, null, "Products of animal origin, not elsewhere specified or included", (byte)2, true, null, null },
                    { 6, "06", "06", "Hsn", null, null, null, "Live trees and other plants; bulbs, roots; cut flowers and ornamental foliage", (byte)2, true, null, null },
                    { 7, "07", "07", "Hsn", null, null, null, "Edible vegetables and certain roots and tubers", (byte)2, true, null, null },
                    { 8, "08", "08", "Hsn", null, null, null, "Edible fruit and nuts; peel of citrus fruit or melons", (byte)2, true, null, null },
                    { 9, "09", "09", "Hsn", null, null, null, "Coffee, tea, mate and spices", (byte)2, true, null, null },
                    { 10, "10", "10", "Hsn", null, null, null, "Cereals", (byte)2, true, null, null },
                    { 11, "11", "11", "Hsn", null, null, null, "Products of the milling industry; malt; starches; inulin; wheat gluten", (byte)2, true, null, null },
                    { 12, "12", "12", "Hsn", null, null, null, "Oil seeds and oleaginous fruits; miscellaneous grains, seeds and fruit", (byte)2, true, null, null },
                    { 13, "13", "13", "Hsn", null, null, null, "Lac; gums, resins and other vegetable saps and extracts", (byte)2, true, null, null },
                    { 14, "14", "14", "Hsn", null, null, null, "Vegetable plaiting materials; vegetable products not elsewhere specified", (byte)2, true, null, null },
                    { 15, "15", "15", "Hsn", null, null, null, "Animal or vegetable fats and oils and their cleavage products", (byte)2, true, null, null },
                    { 16, "16", "16", "Hsn", null, null, null, "Preparations of meat, fish or crustaceans, molluscs or other aquatic invertebrates", (byte)2, true, null, null },
                    { 17, "17", "17", "Hsn", null, null, null, "Sugars and sugar confectionery", (byte)2, true, null, null },
                    { 18, "18", "18", "Hsn", null, null, null, "Cocoa and cocoa preparations", (byte)2, true, null, null },
                    { 19, "19", "19", "Hsn", null, null, null, "Preparations of cereals, flour, starch or milk; pastrycooks' products", (byte)2, true, null, null },
                    { 20, "20", "20", "Hsn", null, null, null, "Preparations of vegetables, fruit, nuts or other parts of plants", (byte)2, true, null, null },
                    { 21, "21", "21", "Hsn", null, null, null, "Miscellaneous edible preparations", (byte)2, true, null, null },
                    { 22, "22", "22", "Hsn", null, null, null, "Beverages, spirits and vinegar", (byte)2, true, null, null },
                    { 23, "23", "23", "Hsn", null, null, null, "Residues and waste from the food industries; prepared animal fodder", (byte)2, true, null, null },
                    { 24, "24", "24", "Hsn", null, null, null, "Tobacco and manufactured tobacco substitutes", (byte)2, true, null, null },
                    { 25, "25", "25", "Hsn", null, null, null, "Salt; sulphur; earths and stone; plastering materials, lime and cement", (byte)2, true, null, null },
                    { 26, "26", "26", "Hsn", null, null, null, "Ores, slag and ash", (byte)2, true, null, null },
                    { 27, "27", "27", "Hsn", null, null, null, "Mineral fuels, mineral oils and products of their distillation", (byte)2, true, null, null },
                    { 28, "28", "28", "Hsn", null, null, null, "Inorganic chemicals; compounds of precious metals and rare-earth metals", (byte)2, true, null, null },
                    { 29, "29", "29", "Hsn", null, null, null, "Organic chemicals", (byte)2, true, null, null },
                    { 30, "30", "30", "Hsn", null, null, null, "Pharmaceutical products", (byte)2, true, null, null },
                    { 31, "31", "31", "Hsn", null, null, null, "Fertilisers", (byte)2, true, null, null },
                    { 32, "32", "32", "Hsn", null, null, null, "Tanning or dyeing extracts; dyes, pigments, paints, varnishes, putty and inks", (byte)2, true, null, null },
                    { 33, "33", "33", "Hsn", null, null, null, "Essential oils and resinoids; perfumery, cosmetic or toilet preparations", (byte)2, true, null, null },
                    { 34, "34", "34", "Hsn", null, null, null, "Soap, organic surface-active agents, washing and lubricating preparations", (byte)2, true, null, null },
                    { 35, "35", "35", "Hsn", null, null, null, "Albuminoidal substances; modified starches; glues; enzymes", (byte)2, true, null, null },
                    { 36, "36", "36", "Hsn", null, null, null, "Explosives; pyrotechnic products; matches; certain combustible preparations", (byte)2, true, null, null },
                    { 37, "37", "37", "Hsn", null, null, null, "Photographic or cinematographic goods", (byte)2, true, null, null },
                    { 38, "38", "38", "Hsn", null, null, null, "Miscellaneous chemical products", (byte)2, true, null, null },
                    { 39, "39", "39", "Hsn", null, null, null, "Plastics and articles thereof", (byte)2, true, null, null },
                    { 40, "40", "40", "Hsn", null, null, null, "Rubber and articles thereof", (byte)2, true, null, null },
                    { 41, "41", "41", "Hsn", null, null, null, "Raw hides and skins (other than furskins) and leather", (byte)2, true, null, null },
                    { 42, "42", "42", "Hsn", null, null, null, "Articles of leather; saddlery and harness; travel goods, handbags", (byte)2, true, null, null },
                    { 43, "43", "43", "Hsn", null, null, null, "Furskins and artificial fur; manufactures thereof", (byte)2, true, null, null },
                    { 44, "44", "44", "Hsn", null, null, null, "Wood and articles of wood; wood charcoal", (byte)2, true, null, null },
                    { 45, "45", "45", "Hsn", null, null, null, "Cork and articles of cork", (byte)2, true, null, null },
                    { 46, "46", "46", "Hsn", null, null, null, "Manufactures of straw, esparto or other plaiting materials; basketware", (byte)2, true, null, null },
                    { 47, "47", "47", "Hsn", null, null, null, "Pulp of wood or other fibrous cellulosic material; recovered paper or paperboard", (byte)2, true, null, null },
                    { 48, "48", "48", "Hsn", null, null, null, "Paper and paperboard; articles of paper pulp, of paper or of paperboard", (byte)2, true, null, null },
                    { 49, "49", "49", "Hsn", null, null, null, "Printed books, newspapers, pictures and other products of the printing industry", (byte)2, true, null, null },
                    { 50, "50", "50", "Hsn", null, null, null, "Silk", (byte)2, true, null, null },
                    { 51, "51", "51", "Hsn", null, null, null, "Wool, fine or coarse animal hair; horsehair yarn and woven fabric", (byte)2, true, null, null },
                    { 52, "52", "52", "Hsn", null, null, null, "Cotton", (byte)2, true, null, null },
                    { 53, "53", "53", "Hsn", null, null, null, "Other vegetable textile fibres; paper yarn and woven fabric of paper yarn", (byte)2, true, null, null },
                    { 54, "54", "54", "Hsn", null, null, null, "Man-made filaments; strip and the like of man-made textile materials", (byte)2, true, null, null },
                    { 55, "55", "55", "Hsn", null, null, null, "Man-made staple fibres", (byte)2, true, null, null },
                    { 56, "56", "56", "Hsn", null, null, null, "Wadding, felt and nonwovens; special yarns; twine, cordage, ropes and cables", (byte)2, true, null, null },
                    { 57, "57", "57", "Hsn", null, null, null, "Carpets and other textile floor coverings", (byte)2, true, null, null },
                    { 58, "58", "58", "Hsn", null, null, null, "Special woven fabrics; tufted textile fabrics; lace; tapestries; embroidery", (byte)2, true, null, null },
                    { 59, "59", "59", "Hsn", null, null, null, "Impregnated, coated, covered or laminated textile fabrics", (byte)2, true, null, null },
                    { 60, "60", "60", "Hsn", null, null, null, "Knitted or crocheted fabrics", (byte)2, true, null, null },
                    { 61, "61", "61", "Hsn", null, null, null, "Articles of apparel and clothing accessories, knitted or crocheted", (byte)2, true, null, null },
                    { 62, "62", "62", "Hsn", null, null, null, "Articles of apparel and clothing accessories, not knitted or crocheted", (byte)2, true, null, null },
                    { 63, "63", "63", "Hsn", null, null, null, "Other made-up textile articles; sets; worn clothing and worn textile articles", (byte)2, true, null, null },
                    { 64, "64", "64", "Hsn", null, null, null, "Footwear, gaiters and the like; parts of such articles", (byte)2, true, null, null },
                    { 65, "65", "65", "Hsn", null, null, null, "Headgear and parts thereof", (byte)2, true, null, null },
                    { 66, "66", "66", "Hsn", null, null, null, "Umbrellas, sun umbrellas, walking sticks, whips, riding crops and parts", (byte)2, true, null, null },
                    { 67, "67", "67", "Hsn", null, null, null, "Prepared feathers and down; artificial flowers; articles of human hair", (byte)2, true, null, null },
                    { 68, "68", "68", "Hsn", null, null, null, "Articles of stone, plaster, cement, asbestos, mica or similar materials", (byte)2, true, null, null },
                    { 69, "69", "69", "Hsn", null, null, null, "Ceramic products", (byte)2, true, null, null },
                    { 70, "70", "70", "Hsn", null, null, null, "Glass and glassware", (byte)2, true, null, null },
                    { 71, "71", "71", "Hsn", null, null, null, "Natural or cultured pearls, precious stones, precious metals; imitation jewellery", (byte)2, true, null, null },
                    { 72, "72", "72", "Hsn", null, null, null, "Iron and steel", (byte)2, true, null, null },
                    { 73, "73", "73", "Hsn", null, null, null, "Articles of iron or steel", (byte)2, true, null, null },
                    { 74, "74", "74", "Hsn", null, null, null, "Copper and articles thereof", (byte)2, true, null, null },
                    { 75, "75", "75", "Hsn", null, null, null, "Nickel and articles thereof", (byte)2, true, null, null },
                    { 76, "76", "76", "Hsn", null, null, null, "Aluminium and articles thereof", (byte)2, true, null, null },
                    { 77, "78", "78", "Hsn", null, null, null, "Lead and articles thereof", (byte)2, true, null, null },
                    { 78, "79", "79", "Hsn", null, null, null, "Zinc and articles thereof", (byte)2, true, null, null },
                    { 79, "80", "80", "Hsn", null, null, null, "Tin and articles thereof", (byte)2, true, null, null },
                    { 80, "81", "81", "Hsn", null, null, null, "Other base metals; cermets; articles thereof", (byte)2, true, null, null },
                    { 81, "82", "82", "Hsn", null, null, null, "Tools, implements, cutlery, spoons and forks, of base metal", (byte)2, true, null, null },
                    { 82, "83", "83", "Hsn", null, null, null, "Miscellaneous articles of base metal", (byte)2, true, null, null },
                    { 83, "84", "84", "Hsn", null, null, null, "Nuclear reactors, boilers, machinery and mechanical appliances; parts thereof", (byte)2, true, null, null },
                    { 84, "85", "85", "Hsn", null, null, null, "Electrical machinery and equipment and parts thereof; sound and TV apparatus", (byte)2, true, null, null },
                    { 85, "86", "86", "Hsn", null, null, null, "Railway or tramway locomotives, rolling stock and parts; track fixtures", (byte)2, true, null, null },
                    { 86, "87", "87", "Hsn", null, null, null, "Vehicles other than railway or tramway rolling stock, and parts thereof", (byte)2, true, null, null },
                    { 87, "88", "88", "Hsn", null, null, null, "Aircraft, spacecraft, and parts thereof", (byte)2, true, null, null },
                    { 88, "89", "89", "Hsn", null, null, null, "Ships, boats and floating structures", (byte)2, true, null, null },
                    { 89, "90", "90", "Hsn", null, null, null, "Optical, photographic, measuring, checking, precision and medical instruments", (byte)2, true, null, null },
                    { 90, "91", "91", "Hsn", null, null, null, "Clocks and watches and parts thereof", (byte)2, true, null, null },
                    { 91, "92", "92", "Hsn", null, null, null, "Musical instruments; parts and accessories of such articles", (byte)2, true, null, null },
                    { 92, "93", "93", "Hsn", null, null, null, "Arms and ammunition; parts and accessories thereof", (byte)2, true, null, null },
                    { 93, "94", "94", "Hsn", null, null, null, "Furniture; bedding, mattresses, cushions; lamps and lighting fittings", (byte)2, true, null, null },
                    { 94, "95", "95", "Hsn", null, null, null, "Toys, games and sports requisites; parts and accessories thereof", (byte)2, true, null, null },
                    { 95, "96", "96", "Hsn", null, null, null, "Miscellaneous manufactured articles", (byte)2, true, null, null },
                    { 96, "97", "97", "Hsn", null, null, null, "Works of art, collectors' pieces and antiques", (byte)2, true, null, null },
                    { 97, "98", "98", "Hsn", null, null, null, "Project imports; laboratory chemicals; passengers' baggage", (byte)2, true, null, null },
                    { 98, "99", "99", "Sac", null, null, null, "Services", (byte)2, true, null, null },
                    { 99, "99", "9954", "Sac", null, null, null, "Construction services", (byte)4, true, null, null },
                    { 100, "99", "9961", "Sac", null, null, null, "Services in wholesale trade", (byte)4, true, null, null },
                    { 101, "99", "9962", "Sac", null, null, null, "Services in retail trade", (byte)4, true, null, null },
                    { 102, "99", "9963", "Sac", null, null, null, "Accommodation, food and beverage services", (byte)4, true, null, null },
                    { 103, "99", "9964", "Sac", null, null, null, "Passenger transport services", (byte)4, true, null, null },
                    { 104, "99", "9965", "Sac", null, null, null, "Goods transport services", (byte)4, true, null, null },
                    { 105, "99", "9966", "Sac", null, null, null, "Rental services of transport vehicles with operators", (byte)4, true, null, null },
                    { 106, "99", "9967", "Sac", null, null, null, "Supporting services in transport", (byte)4, true, null, null },
                    { 107, "99", "9968", "Sac", null, null, null, "Postal and courier services", (byte)4, true, null, null },
                    { 108, "99", "9969", "Sac", null, null, null, "Electricity, gas, water and other distribution services", (byte)4, true, null, null },
                    { 109, "99", "9971", "Sac", null, null, null, "Financial and related services", (byte)4, true, null, null },
                    { 110, "99", "9972", "Sac", null, null, null, "Real estate services", (byte)4, true, null, null },
                    { 111, "99", "9973", "Sac", null, null, null, "Leasing or rental services without operator", (byte)4, true, null, null },
                    { 112, "99", "9981", "Sac", null, null, null, "Research and development services", (byte)4, true, null, null },
                    { 113, "99", "9982", "Sac", null, null, null, "Legal and accounting services", (byte)4, true, null, null },
                    { 114, "99", "9983", "Sac", null, null, null, "Other professional, technical and business services", (byte)4, true, null, null },
                    { 115, "99", "9984", "Sac", null, null, null, "Telecommunications, broadcasting and information supply services", (byte)4, true, null, null },
                    { 116, "99", "9985", "Sac", null, null, null, "Support services", (byte)4, true, null, null },
                    { 117, "99", "9986", "Sac", null, null, null, "Support services to agriculture, hunting, forestry, fishing and mining", (byte)4, true, null, null },
                    { 118, "99", "9987", "Sac", null, null, null, "Maintenance, repair and installation services", (byte)4, true, null, null },
                    { 119, "99", "9988", "Sac", null, null, null, "Manufacturing services on physical inputs owned by others", (byte)4, true, null, null },
                    { 120, "99", "9989", "Sac", null, null, null, "Other manufacturing services; publishing, printing and reproduction", (byte)4, true, null, null },
                    { 121, "99", "9991", "Sac", null, null, null, "Public administration and other services to the community", (byte)4, true, null, null },
                    { 122, "99", "9992", "Sac", null, null, null, "Education services", (byte)4, true, null, null },
                    { 123, "99", "9993", "Sac", null, null, null, "Human health and social care services", (byte)4, true, null, null },
                    { 124, "99", "9994", "Sac", null, null, null, "Sewage and waste collection, treatment and disposal services", (byte)4, true, null, null },
                    { 125, "99", "9995", "Sac", null, null, null, "Services of membership organisations", (byte)4, true, null, null },
                    { 126, "99", "9996", "Sac", null, null, null, "Recreational, cultural and sporting services", (byte)4, true, null, null },
                    { 127, "99", "9997", "Sac", null, null, null, "Other services", (byte)4, true, null, null },
                    { 128, "99", "9998", "Sac", null, null, null, "Domestic services", (byte)4, true, null, null },
                    { 129, "99", "9999", "Sac", null, null, null, "Services provided by extraterritorial organisations and bodies", (byte)4, true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerSources",
                columns: new[] { "LedgerSourceId", "Code", "CreatedAt", "CreatedBy", "Direction", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "TRANSACTION", null, null, "Both", true, null, null, "Document posting" },
                    { 2, "BILLPAYMENT", null, null, "Out", true, null, null, "Bill payment" },
                    { 3, "INVOICEPAYMENT", null, null, "In", true, null, null, "Invoice payment" },
                    { 4, "BILLREFUND", null, null, "In", true, null, null, "Bill refund received" },
                    { 5, "INVOICEREFUND", null, null, "Out", true, null, null, "Invoice refund paid" },
                    { 6, "CREDITNOTEREFUND", null, null, "Out", true, null, null, "Credit note refund paid" },
                    { 7, "DEBITNOTEREFUND", null, null, "In", true, null, null, "Debit note refund received" },
                    { 8, "VENDORPREPAYMENT", null, null, "Out", true, null, null, "Advance paid to vendor" },
                    { 9, "CUSTOMERPREPAYMENT", null, null, "In", true, null, null, "Advance received from customer" },
                    { 10, "ALLOCATION", null, null, "Both", true, null, null, "Credit note, debit note or prepayment allocation" },
                    { 11, "MONEYTRANSFER", null, null, "Both", true, null, null, "Bank or cash transfer" },
                    { 12, "JOURNAL", null, null, "Both", true, null, null, "Manual journal" },
                    { 13, "OPENINGBALANCE", null, null, "Both", true, null, null, "Opening balance" },
                    { 14, "DEPRECIATION", null, null, "Out", true, null, null, "Depreciation" },
                    { 15, "STOCKADJUSTMENT", null, null, "Both", true, null, null, "Stock adjustment" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerTypes",
                columns: new[] { "LedgerTypeId", "Code", "CreatedAt", "CreatedBy", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "ITEM", null, null, true, null, null, "Line item" },
                    { 2, "TAX", null, null, true, null, null, "Tax" },
                    { 3, "CONTROL", null, null, true, null, null, "AP / AR / bank / cash control leg" },
                    { 4, "COGS", null, null, true, null, null, "Cost of goods sold" },
                    { 5, "FX", null, null, true, null, null, "Realized exchange gain or loss" },
                    { 6, "ROUNDOFF", null, null, true, null, null, "Rounding" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { "BIL", null, null, true, true, null, null, "Bill" },
                    { "CRN", null, null, true, true, null, null, "Credit Note" },
                    { "DBN", null, null, true, true, null, null, "Debit Note" },
                    { "DEP", null, null, true, true, null, null, "Depreciation" },
                    { "GRN", null, null, true, true, null, null, "Goods Receipt" },
                    { "INV", null, null, true, true, null, null, "Invoice" },
                    { "JRN", null, null, true, true, null, null, "Journal" },
                    { "OPB", null, null, true, true, null, null, "Opening Balance" },
                    { "POR", null, null, true, false, null, null, "Purchase Order" },
                    { "POS", null, null, true, true, null, null, "POS Sale" },
                    { "QTE", null, null, true, false, null, null, "Quote" },
                    { "RCM", null, null, true, true, null, null, "Receive Money" },
                    { "SOR", null, null, true, false, null, null, "Sales Order" },
                    { "SPM", null, null, true, true, null, null, "Spend Money" },
                    { "STA", null, null, true, true, null, null, "Stock Adjustment" },
                    { "TRM", null, null, true, true, null, null, "Transfer Money" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "States",
                columns: new[] { "StateId", "CountryId", "CreatedAt", "CreatedBy", "IsActive", "ModifiedAt", "ModifiedBy", "StateCode", "StateName" },
                values: new object[,]
                {
                    { 1, 1, null, null, true, null, null, "01", "Jammu and Kashmir" },
                    { 2, 1, null, null, true, null, null, "02", "Himachal Pradesh" },
                    { 3, 1, null, null, true, null, null, "03", "Punjab" },
                    { 4, 1, null, null, true, null, null, "04", "Chandigarh" },
                    { 5, 1, null, null, true, null, null, "05", "Uttarakhand" },
                    { 6, 1, null, null, true, null, null, "06", "Haryana" },
                    { 7, 1, null, null, true, null, null, "07", "Delhi" },
                    { 8, 1, null, null, true, null, null, "08", "Rajasthan" },
                    { 9, 1, null, null, true, null, null, "09", "Uttar Pradesh" },
                    { 10, 1, null, null, true, null, null, "10", "Bihar" },
                    { 11, 1, null, null, true, null, null, "11", "Sikkim" },
                    { 12, 1, null, null, true, null, null, "12", "Arunachal Pradesh" },
                    { 13, 1, null, null, true, null, null, "13", "Nagaland" },
                    { 14, 1, null, null, true, null, null, "14", "Manipur" },
                    { 15, 1, null, null, true, null, null, "15", "Mizoram" },
                    { 16, 1, null, null, true, null, null, "16", "Tripura" },
                    { 17, 1, null, null, true, null, null, "17", "Meghalaya" },
                    { 18, 1, null, null, true, null, null, "18", "Assam" },
                    { 19, 1, null, null, true, null, null, "19", "West Bengal" },
                    { 20, 1, null, null, true, null, null, "20", "Jharkhand" },
                    { 21, 1, null, null, true, null, null, "21", "Odisha" },
                    { 22, 1, null, null, true, null, null, "22", "Chhattisgarh" },
                    { 23, 1, null, null, true, null, null, "23", "Madhya Pradesh" },
                    { 24, 1, null, null, true, null, null, "24", "Gujarat" },
                    { 25, 1, null, null, true, null, null, "26", "Dadra and Nagar Haveli and Daman and Diu" },
                    { 26, 1, null, null, true, null, null, "27", "Maharashtra" },
                    { 27, 1, null, null, true, null, null, "29", "Karnataka" },
                    { 28, 1, null, null, true, null, null, "30", "Goa" },
                    { 29, 1, null, null, true, null, null, "31", "Lakshadweep" },
                    { 30, 1, null, null, true, null, null, "32", "Kerala" },
                    { 31, 1, null, null, true, null, null, "33", "Tamil Nadu" },
                    { 32, 1, null, null, true, null, null, "34", "Puducherry" },
                    { 33, 1, null, null, true, null, null, "35", "Andaman and Nicobar Islands" },
                    { 34, 1, null, null, true, null, null, "36", "Telangana" },
                    { 35, 1, null, null, true, null, null, "37", "Andhra Pradesh" },
                    { 36, 1, null, null, true, null, null, "38", "Ladakh" },
                    { 37, 1, null, null, true, null, null, "97", "Other Territory" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_SystemName",
                schema: "mst",
                table: "AccountTypes",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CountryCode",
                schema: "mst",
                table: "Countries",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                schema: "mst",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsnSacCodes_Code",
                schema: "mst",
                table: "HsnSacCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsnSacCodes_CodeType_ChapterCode",
                schema: "mst",
                table: "HsnSacCodes",
                columns: new[] { "CodeType", "ChapterCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerSources_Code",
                schema: "mst",
                table: "LedgerSources",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTypes_Code",
                schema: "mst",
                table: "LedgerTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_States_CountryId_StateCode",
                schema: "mst",
                table: "States",
                columns: new[] { "CountryId", "StateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_Name",
                schema: "mst",
                table: "TransactionTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "HsnSacCodes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "LedgerSources",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "LedgerTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "States",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "TransactionTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "mst");
        }
    }
}
