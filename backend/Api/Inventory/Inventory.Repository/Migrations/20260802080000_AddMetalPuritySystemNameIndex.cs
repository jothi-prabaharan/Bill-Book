using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// Metal purities were the one seeded master whose system name was not
    /// unique. Re-seeding matches on it — the display name is renamable, so it
    /// cannot be the identity — and a match that is not unique is not a match:
    /// two concurrent seed calls could each find the row absent and each insert
    /// it.
    ///
    /// Filtered on NOT NULL, like every other system-name index here, so purities
    /// a customer types in themselves carry no system name and are not forced
    /// into a namespace that is not theirs.
    /// </summary>
    public partial class AddMetalPuritySystemNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_SystemName",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "PuritySystemName" },
                unique: true,
                filter: "\"PuritySystemName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetalPurities_SystemName",
                schema: "inv",
                table: "MetalPurities");
        }
    }
}
