using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contacts.Repository.Migrations
{
    /// <summary>
    /// Records whether a contact's receivable and payable sub-accounts exist.
    ///
    /// They are written by Accounting over HTTP, which cannot join the local
    /// transaction — so a contact can save while its sub-ledger does not get
    /// created. Until now the result of that call was discarded and the failure
    /// was invisible: the contact looked complete and could not be invoiced.
    ///
    /// Held on the contact rather than asked of Accounting per row, because the
    /// contact list is the classic N+1 and one HTTP call per row is not a list.
    /// </summary>
    public partial class AddSubLedgerProvisioned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added WITH a default so Postgres backfills existing rows in the
            // same statement, then the default is dropped so new contacts start
            // null and must earn the timestamp.
            //
            // Backfilling asserts that contacts created before this were
            // provisioned. That is an assumption, and a deliberate one: the old
            // code did make the call, and marking every existing contact as
            // broken would be a false alarm on the common case — which is how a
            // warning badge stops being read. Where the assumption is wrong,
            // Link sub-ledger fixes it and is safe to run either way.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubLedgerProvisionedAt",
                schema: "con",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SubLedgerProvisionedAt",
                schema: "con",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            // Finding the contacts that need retrying, without scanning a table
            // where nearly every row is provisioned.
            migrationBuilder.CreateIndex(
                name: "IX_Contacts_SubLedgerPending",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "ContactId" },
                filter: "\"SubLedgerProvisionedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_SubLedgerPending", schema: "con", table: "Contacts");

            migrationBuilder.DropColumn(
                name: "SubLedgerProvisionedAt", schema: "con", table: "Contacts");
        }
    }
}
