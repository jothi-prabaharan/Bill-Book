using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Repository.Migrations
{
    /// <summary>
    /// A branch created beyond the licence is no longer refused — it is created
    /// on its own 30-day trial. This column is what says so.
    ///
    /// The expiry date alone cannot. A branch deliberately wound down early also
    /// ends before the licence does, and the two need opposite words on screen:
    /// one is "buy a licence for this branch", the other is "this branch was
    /// closed". Guessing from the date would tell some customers to pay for a
    /// branch they had themselves closed.
    ///
    /// <b>Existing branches default to false</b>, which is correct rather than
    /// merely convenient: every branch that exists today was created under the
    /// old rule, which refused anything the licence did not cover. So none of
    /// them is on a trial, and the default states a fact.
    /// </summary>
    public partial class AddOrganizationTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                schema: "plt",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrial",
                schema: "plt",
                table: "Organizations");
        }
    }
}
