using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSubAccountPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponent",
                schema: "acc",
                table: "SubAccounts");

            // 'Primary', not the empty string EF scaffolds for a new non-null
            // column. Every sub-account written before this migration is a trade
            // balance — advances are what the column was added for — and an empty
            // purpose would match no enum value, so reading those rows back would
            // throw rather than merely look odd.
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "acc",
                table: "SubAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Primary");

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponen~",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "AccountId", "ReferenceType", "ReferenceId", "TaxComponent", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_Purpose",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "OrgId", "Purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponen~",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.DropIndex(
                name: "IX_SubAccounts_Purpose",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponent",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "AccountId", "ReferenceType", "ReferenceId", "TaxComponent" },
                unique: true);
        }
    }
}
