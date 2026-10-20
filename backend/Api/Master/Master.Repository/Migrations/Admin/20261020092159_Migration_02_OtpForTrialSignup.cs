using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class Migration_02_OtpForTrialSignup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TK-196: a trial-signup code is sent before any user exists, so
            // UserId is nullable, and such a code is found by the number it
            // went to (the index below).
            //
            // The six Menus updates are not TK-196's. Commit 21e3e41e reordered
            // the report groups in the seed and the snapshot by hand with no
            // migration, so no database ever got the new order; generating
            // this migration picked them up, and they are kept so the model
            // and the databases agree again.
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "mst",
                table: "OtpVerifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "DisplayOrder",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "DisplayOrder",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "DisplayOrder",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "DisplayOrder",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "DisplayOrder",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "DisplayOrder",
                value: 3);

            migrationBuilder.CreateIndex(
                name: "IX_OtpVerifications_Destination_Purpose_ExpiresAt",
                schema: "mst",
                table: "OtpVerifications",
                columns: new[] { "Destination", "Purpose", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpVerifications_Destination_Purpose_ExpiresAt",
                schema: "mst",
                table: "OtpVerifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "mst",
                table: "OtpVerifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "DisplayOrder",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "DisplayOrder",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "DisplayOrder",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "DisplayOrder",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "DisplayOrder",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "DisplayOrder",
                value: 6);
        }
    }
}
