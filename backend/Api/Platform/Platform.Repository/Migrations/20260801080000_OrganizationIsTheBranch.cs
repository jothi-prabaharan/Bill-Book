using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Repository.Migrations
{
    /// <summary>
    /// Collapses the three-level Customer → Organization → Branch model back to
    /// two: the Customer is the head office, and an Organization <b>is</b> a
    /// branch.
    ///
    /// <c>plt.Branches</c> duplicated Organization almost column for column —
    /// GSTIN, both address lines, city, state, postal code, country, phone,
    /// mobile, email — while <c>OrgId</c> was already the isolation boundary.
    /// Two tables described one thing, and only one of them scoped any data.
    /// </summary>
    public partial class OrganizationIsTheBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Branches", schema: "plt");

            // The short code the branch is known by. Existing rows are the first
            // organization on their account, so they are the head office.
            migrationBuilder.AddColumn<string>(
                name: "OrgCode",
                schema: "plt",
                table: "Organizations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "HO");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CustomerId_OrgCode",
                schema: "plt",
                table: "Organizations",
                columns: new[] { "CustomerId", "OrgCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_CustomerId_OrgCode",
                schema: "plt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OrgCode",
                schema: "plt",
                table: "Organizations");

            // Branches is not recreated. Down puts the schema back to a shape
            // that works; it does not resurrect a table this migration exists to
            // remove, and no data survived to restore into it.
        }
    }
}
