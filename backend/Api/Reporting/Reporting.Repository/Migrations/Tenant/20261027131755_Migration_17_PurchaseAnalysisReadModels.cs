using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_17_PurchaseAnalysisReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DebitNoteDetailRead is a new read model over pur.DebitNoteDetails and DebitNoteRead gains ReasonCode, which Purchase owns:
            // nothing in the database changes, only the model snapshot (TK-239).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
