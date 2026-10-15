using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printing.Repository.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPaperSizeAndSampleFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE prt."PrintTemplates"
                SET "PaperSizeName" = 'A4', "IsSample" = TRUE
                WHERE "PaperSizeName" = '' OR "PaperSizeName" IS NULL;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
