using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class RetailBusinessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Branches stored as General become Retail (TK-136). An UpdateData
            // keyed on the column rather than the primary key, so every such row
            // changes in one statement; the seeded menu and permission rows
            // below change one by one.
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Organizations",
                keyColumn: "BusinessType",
                keyValue: "General",
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Organizations",
                keyColumn: "Vertical",
                keyValue: "General",
                column: "Vertical",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 3,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 4,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 5,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 6,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 7,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 8,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 102,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 103,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 104,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 105,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 106,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 113,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 116,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 117,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 123,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1002,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1003,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1004,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1005,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1006,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1007,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1008,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1009,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1010,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1011,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1012,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1013,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1014,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1015,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1016,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1017,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1018,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1019,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1020,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1021,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1022,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1023,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1024,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1025,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1026,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1027,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1028,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1029,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1030,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1031,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1032,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1038,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1039,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1091,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1092,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1093,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1127,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1128,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1129,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1130,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1131,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1132,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1138,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 1,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 2,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 3,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 4,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 5,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 6,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 7,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 9,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 31,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 32,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 33,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 34,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 35,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 36,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 37,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 38,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 39,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 40,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 41,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 42,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 43,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 44,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 45,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 46,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 47,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 48,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 49,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 50,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 51,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 52,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 53,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 54,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 55,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 56,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 57,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 58,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 59,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 60,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 61,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 62,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 63,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 64,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 65,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 66,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 67,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 68,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 69,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 70,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 71,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 72,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 73,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 74,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 75,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 76,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 77,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 78,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 79,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 80,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 81,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 82,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 83,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 84,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 85,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 86,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 87,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 88,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 89,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 90,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 101,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 102,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 103,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 104,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 105,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 106,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 107,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 108,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 109,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 110,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 281,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 282,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 283,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 284,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 285,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 286,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 287,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 288,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 289,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 290,
                column: "BusinessType",
                value: "Retail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10003,
                column: "BusinessType",
                value: "Retail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Organizations",
                keyColumn: "BusinessType",
                keyValue: "Retail",
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Organizations",
                keyColumn: "Vertical",
                keyValue: "Retail",
                column: "Vertical",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 3,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 4,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 5,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 6,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 7,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 8,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 102,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 103,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 104,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 105,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 106,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 113,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 116,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 117,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 123,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1002,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1003,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1004,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1005,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1006,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1007,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1008,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1009,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1010,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1011,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1012,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1013,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1014,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1015,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1016,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1017,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1018,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1019,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1020,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1021,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1022,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1023,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1024,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1025,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1026,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1027,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1028,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1029,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1030,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1031,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1032,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1038,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1039,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1091,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1092,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1093,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1127,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1128,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1129,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1130,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1131,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1132,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1138,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 1,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 2,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 3,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 4,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 5,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 6,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 7,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 9,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 31,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 32,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 33,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 34,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 35,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 36,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 37,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 38,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 39,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 40,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 41,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 42,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 43,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 44,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 45,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 46,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 47,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 48,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 49,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 50,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 51,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 52,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 53,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 54,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 55,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 56,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 57,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 58,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 59,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 60,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 61,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 62,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 63,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 64,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 65,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 66,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 67,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 68,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 69,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 70,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 71,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 72,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 73,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 74,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 75,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 76,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 77,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 78,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 79,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 80,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 81,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 82,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 83,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 84,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 85,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 86,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 87,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 88,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 89,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 90,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 101,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 102,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 103,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 104,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 105,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 106,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 107,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 108,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 109,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 110,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 281,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 282,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 283,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 284,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 285,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 286,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 287,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 288,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 289,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 290,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10003,
                column: "BusinessType",
                value: "General");
        }
    }
}
