using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AppOnRolesLicencesAndMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TK-42. Every existing role, licence, refresh token, permission and
            // menu row is RetailErp's (1); the seed updates below then widen the
            // shared settings rows to every app (15). The default is written by
            // hand: EF's would be 0, which is no app at all.
            migrationBuilder.DropIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_CustomerId",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "RefreshTokens",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Apps",
                schema: "mst",
                table: "Permissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Apps",
                schema: "mst",
                table: "Menus",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 3,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 4,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 5,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 6,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 7,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 8,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 9,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 106,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 113,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 114,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 115,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 116,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 117,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1002,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1003,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1004,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1005,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1006,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1007,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1008,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1009,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1010,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1011,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1012,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1013,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1014,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1015,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1016,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1017,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1018,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1019,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1020,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1021,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1022,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1023,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1024,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1025,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1026,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1027,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1028,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1029,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1030,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1031,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1032,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1038,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1039,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1079,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1080,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1081,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1082,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1083,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1084,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1085,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1086,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1087,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1088,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1089,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1090,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1091,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1092,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1093,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1106,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 1,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 2,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 3,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 4,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 5,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 6,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 7,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 9,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 21,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 22,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 23,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 24,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 25,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 26,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 27,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 28,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 29,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 30,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 31,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 32,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 33,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 34,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 35,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 36,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 37,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 38,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 39,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 40,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 41,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 42,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 43,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 44,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 45,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 46,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 47,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 48,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 49,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 50,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 51,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 52,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 53,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 54,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 55,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 56,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 57,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 58,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 59,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 60,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 61,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 62,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 63,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 64,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 65,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 66,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 67,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 68,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 69,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 70,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 71,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 72,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 73,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 74,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 75,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 76,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 77,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 78,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 79,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 80,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 81,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 82,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 83,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 84,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 85,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 86,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 87,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 88,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 89,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 90,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 91,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 92,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 93,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 94,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 95,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 96,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 97,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 98,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 99,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 100,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 106,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 107,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 108,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 109,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 110,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 111,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 112,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 113,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 114,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 115,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 116,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 117,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 118,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 119,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 120,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 4,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 5,
                column: "App",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_App_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "App", "SystemName" },
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_App_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "CustomerId", "App", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerId_App",
                schema: "mst",
                table: "Licenses",
                columns: new[] { "CustomerId", "App" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_App_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CustomerId_App_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_CustomerId_App",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Apps",
                schema: "mst",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Apps",
                schema: "mst",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "CustomerId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemName",
                schema: "mst",
                table: "Roles",
                column: "SystemName",
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerId",
                schema: "mst",
                table: "Licenses",
                column: "CustomerId",
                unique: true);
        }
    }
}
