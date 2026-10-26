using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_08_SalesAnalysisReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-228): sal.CreditNotes and sal.CreditNoteDetails are Sales' tables, excluded from
            // migrations. The read models now carry the credit note's reason and a read model for its lines;
            // this migration only moves the snapshot, so the model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
