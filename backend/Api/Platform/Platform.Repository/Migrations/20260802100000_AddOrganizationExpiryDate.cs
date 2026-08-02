using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Repository.Migrations
{
    /// <summary>
    /// A branch gets its own expiry date, set when it is created and checked at
    /// every login alongside the customer's licence. It is a cap: whichever of
    /// the two ends first governs, so a branch can be wound down without
    /// touching the account, and can never outlive the licence paying for it.
    ///
    /// <b>Existing branches are left null on purpose.</b> Null means "no
    /// branch-level limit — the licence decides", which is exactly how they
    /// behave today, so nothing about them changes.
    ///
    /// Backfilling them from the licence was the first instinct and is wrong.
    /// The date is a copy, not a reference: once written it does not follow the
    /// licence, so a backfill would pin every branch in the system to today's
    /// expiry, and the next renewal would extend the licence while leaving every
    /// branch locked out under it. A null branch follows the licence for free.
    /// </summary>
    public partial class AddOrganizationExpiryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiryDate",
                schema: "plt",
                table: "Organizations",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ExpiryDate",
                schema: "plt",
                table: "Organizations",
                column: "ExpiryDate",
                filter: "\"ExpiryDate\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_ExpiryDate",
                schema: "plt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                schema: "plt",
                table: "Organizations");
        }
    }
}
