using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <summary>
    /// A numbering series no longer names a branch. It is already scoped to one
    /// <c>OrgId</c>, and an <c>OrgId</c> is a branch — so the series table is
    /// per-branch by construction, and each branch numbers from its own.
    ///
    /// <c>BranchCode</c> stays. It is the branch's short code copied onto the
    /// series so a generated number can name where it came from, and copying is
    /// what stops a branch rename restyling numbers already issued.
    /// </summary>
    public partial class DropNumberingBranchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both indexes led with BranchId, so both are rebuilt without it.
            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_Default", schema: "acc", table: "NumberingSeries");

            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_Lookup", schema: "acc", table: "NumberingSeries");

            migrationBuilder.DropColumn(
                name: "BranchId", schema: "acc", table: "NumberingSeries");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Lookup",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Default",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode" },
                unique: true,
                filter: "\"IsDefault\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_Default", schema: "acc", table: "NumberingSeries");

            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_Lookup", schema: "acc", table: "NumberingSeries");

            migrationBuilder.AddColumn<long>(
                name: "BranchId", schema: "acc", table: "NumberingSeries",
                type: "bigint", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Lookup",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Default",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode", "BranchId" },
                unique: true,
                filter: "\"IsDefault\" = true");
        }
    }
}
