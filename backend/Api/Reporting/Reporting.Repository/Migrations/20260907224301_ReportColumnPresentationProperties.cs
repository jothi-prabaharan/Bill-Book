using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations
{
    /// <summary>
    /// Gives rpt.ReportColumns the presentation properties reports.json now
    /// carries: DataType as an enum rather than free text, Alignment, Width,
    /// IsFilterable, IsSortable, IsGroupable and IsPivotable, replacing IsGroup,
    /// IsSort, IsFilter and FilterType.
    ///
    /// The old columns are dropped and the new ones added rather than renamed,
    /// which is a correction to what was scaffolded. EF matched them by position:
    /// IsGroup would have become IsPivotable and IsFilter IsGroupable, carrying
    /// each old value under a name meaning something else, and DataType would have
    /// been cast from text to integer in place — which Postgres refuses on the
    /// seeded 'Text' values, so the migration would have failed on any database
    /// that had run the initial one. Every row's values are set by the UpdateData
    /// calls that follow, this table holding nothing but the seeded specification.
    /// </summary>
    public partial class ReportColumnPresentationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterType",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsFilter",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsGroup",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsSort",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "DataType",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.AddColumn<int>(
                name: "DataType",
                schema: "rpt",
                table: "ReportColumns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Alignment",
                schema: "rpt",
                table: "ReportColumns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                schema: "rpt",
                table: "ReportColumns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFilterable",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSortable",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupable",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPivotable",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -776L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -775L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -774L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -773L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -772L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -771L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -770L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -769L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -768L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -767L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -766L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -765L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -764L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -763L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -762L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -761L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -760L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -759L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -758L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -757L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -756L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -755L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -754L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -753L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 250 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -752L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 280 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -751L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 270 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -750L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -749L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -748L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -747L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -746L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -745L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -744L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -743L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -742L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -741L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -740L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 270 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -739L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -738L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -737L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -736L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -735L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -734L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -733L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -732L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -731L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 100 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -730L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -729L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -728L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -727L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -726L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -725L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -724L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -723L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -722L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -721L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -720L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -719L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -718L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -717L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -716L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -715L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -714L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -713L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -712L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -711L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -710L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -709L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -708L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -707L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -706L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -705L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -704L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -703L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -702L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 3, 9, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -701L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -700L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -699L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -698L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -697L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -696L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -695L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -694L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -693L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -692L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -691L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -690L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -689L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -688L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -687L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 260 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -686L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -685L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 250 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -684L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -683L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -682L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -681L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -680L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -679L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -678L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -677L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -676L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -675L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -674L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -673L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -672L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -671L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -670L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -669L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 220 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -668L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -667L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 3, 9, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -666L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 3, 9, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -665L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -664L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -663L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -662L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -661L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -660L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -659L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -658L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 130 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -657L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -656L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -655L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -654L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -653L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -652L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -651L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -650L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -649L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -648L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -647L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -646L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -645L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -644L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -643L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -642L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -641L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -640L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -639L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -638L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -637L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 130 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -636L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -635L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -634L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -633L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -632L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -631L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -630L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -629L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -628L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -627L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -626L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -625L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -624L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -623L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -622L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -621L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -620L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -619L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -618L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -617L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -616L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -615L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -614L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -613L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -612L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -611L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -610L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -609L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -608L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -607L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -606L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -605L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -604L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -603L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -602L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -601L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -600L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 220 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -599L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -598L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -597L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -596L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -595L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -594L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -593L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -592L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -591L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -590L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -589L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -588L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -587L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -586L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -585L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -584L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -583L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -582L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -581L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -580L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -579L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -578L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -577L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -576L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -575L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -574L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -573L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -572L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -571L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -570L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -569L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -568L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -567L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -566L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -565L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -564L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -563L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -562L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -561L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -560L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -559L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -558L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -557L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -556L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -555L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -554L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -553L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -552L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -551L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -550L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 3, 9, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -549L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -548L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -547L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -546L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -545L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -544L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -543L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -542L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -541L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -540L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -539L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -538L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -537L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -536L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -535L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -534L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -533L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -532L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -531L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -530L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -529L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -528L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -527L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -526L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -525L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -524L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -523L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -522L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -521L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -520L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -519L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -518L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -517L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -516L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -515L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -514L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -513L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -512L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -511L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -510L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -509L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -508L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -507L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -506L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -505L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 260 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -504L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -503L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -502L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -501L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -500L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -499L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -498L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -497L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -496L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -495L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -494L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -493L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -492L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -491L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -490L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -489L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -488L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -487L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -486L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -485L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -484L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -483L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -482L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -481L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -480L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -479L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -478L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -477L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -476L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -475L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -474L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -473L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -472L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -471L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 260 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -470L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -469L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 250 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -468L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -467L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -466L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -465L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -464L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -463L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -462L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -461L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -460L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -459L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -458L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -457L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -456L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -455L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -454L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -453L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -452L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 220 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -451L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -450L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -449L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -448L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -447L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -446L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -445L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -444L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 130 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -443L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -442L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -441L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -440L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -439L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -438L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -437L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -436L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -435L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -434L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -433L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -432L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -431L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -430L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -429L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -428L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -427L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -426L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -425L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -424L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -423L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -422L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -421L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -420L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -419L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -418L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -417L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -416L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -415L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -414L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -413L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -412L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -411L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -410L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -409L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -408L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -407L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -406L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -405L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -404L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 220 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -403L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -402L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -401L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -400L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -399L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -398L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -397L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -396L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -395L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -394L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -393L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -392L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -391L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -390L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -389L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -388L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -387L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -386L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -385L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -384L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -383L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -382L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -381L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -380L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -379L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -378L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -377L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -376L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -375L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -374L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -373L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -372L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -371L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -370L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -369L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -368L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -367L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -366L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -365L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -364L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -363L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -362L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -361L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -360L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -359L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -358L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -357L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -356L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -355L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -354L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -353L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 3, 9, true, true, 110 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -352L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -351L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -350L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -349L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -348L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -347L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -346L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -345L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -344L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -343L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -342L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -341L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -340L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -339L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -338L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -337L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -336L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -335L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -334L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -333L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -332L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -331L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -330L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -329L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -328L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -327L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -326L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -325L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -324L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -323L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -322L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -321L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -320L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -319L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -318L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -317L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -316L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -315L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -314L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -313L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -312L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -311L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -310L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -309L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -308L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -307L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -306L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -305L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -304L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -303L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -302L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -301L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -300L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -299L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -298L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -297L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -296L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -295L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -294L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -293L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -292L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -291L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -290L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -289L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -288L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -287L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -286L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -285L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -284L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -283L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -282L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -281L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -280L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -279L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -278L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -277L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -276L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -275L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -274L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -273L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -272L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -271L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -270L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -269L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -268L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -267L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -266L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -265L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -264L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -263L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -262L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -261L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -260L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -259L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -258L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -257L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -256L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -255L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -254L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -253L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -252L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -251L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -250L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -249L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -248L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -247L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -246L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -245L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -244L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -243L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -242L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -241L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -240L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -239L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -238L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -237L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -236L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -235L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -234L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -233L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -232L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -231L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -230L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -229L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -228L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -227L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -226L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -225L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -224L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -223L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 4, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -222L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -221L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -220L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -219L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -218L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -217L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -216L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -215L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -214L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -213L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -212L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -211L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -210L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 100 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -209L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -208L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -207L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -206L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -205L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -204L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -203L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -202L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -201L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 2, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -200L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -199L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -198L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -197L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -196L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -195L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -194L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -193L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -192L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -191L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -190L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -189L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -188L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -187L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -186L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -185L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -184L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -183L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -182L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -181L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -180L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -179L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -178L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -177L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -176L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -175L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -174L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -173L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -172L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -171L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -170L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -169L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -168L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 100 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -167L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -166L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -165L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 2, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -164L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -163L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -162L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -161L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -160L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -159L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -158L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -157L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -156L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -155L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -154L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -153L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -152L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -151L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -150L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -149L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -148L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -147L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 100 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -146L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -145L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -144L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -143L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -142L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -141L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -140L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -139L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -138L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 2, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -137L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -136L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -135L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -134L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -133L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -132L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -131L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -130L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -129L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -128L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -127L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -126L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -125L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -124L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -123L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -122L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -121L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -120L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -119L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -118L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -117L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -116L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -115L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -114L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -113L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -112L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -111L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -110L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -109L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -108L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -107L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 2, false, 90 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -106L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -105L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -104L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -103L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 2, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -102L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -101L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -100L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -99L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -98L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -97L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -96L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -95L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -94L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -93L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -92L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -91L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -90L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -89L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -88L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -87L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -86L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -85L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 8, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -84L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -83L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -82L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -81L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -80L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -79L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -78L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -77L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -76L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -75L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -74L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -73L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 280 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -72L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -71L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -70L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -69L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -68L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -67L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -66L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -65L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -64L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -63L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 270 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -62L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -61L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -60L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -59L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -58L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -57L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -56L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -55L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -54L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -53L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -52L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 280 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -51L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 210 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -50L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -49L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -48L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -47L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 270 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -46L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 200 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -45L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -44L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 230 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -43L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -42L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -41L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -40L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -39L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -38L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -37L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -36L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -35L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -34L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -33L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -32L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -31L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -30L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 11, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -29L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -28L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -27L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -26L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 5, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -25L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 10, true, true, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -24L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "Width" },
                values: new object[] { 2, 3, false, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -23L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -22L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 170 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -21L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -20L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -19L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -18L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -17L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -16L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -15L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 220 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -14L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 6, true, true, 150 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -13L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 140 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -12L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -11L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, 240 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -10L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -9L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 180 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -8L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 1, 7, true, true, 120 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -7L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -6L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -5L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsSortable", "Width" },
                values: new object[] { 2, 3, true, true, 190 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -4L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -3L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -2L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -1L,
                columns: new[] { "Alignment", "DataType", "IsFilterable", "IsGroupable", "IsPivotable", "IsSortable", "Width" },
                values: new object[] { 1, 1, true, true, true, true, 160 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alignment",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsFilterable",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsGroupable",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsPivotable",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "IsSortable",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "Width",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.DropColumn(
                name: "DataType",
                schema: "rpt",
                table: "ReportColumns");

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                schema: "rpt",
                table: "ReportColumns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterType",
                schema: "rpt",
                table: "ReportColumns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFilter",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGroup",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSort",
                schema: "rpt",
                table: "ReportColumns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -776L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -775L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -774L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -773L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -772L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -771L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -770L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -769L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -768L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -767L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -766L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -765L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -764L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -763L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -762L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -761L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -760L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -759L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -758L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -757L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -756L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -755L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -754L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -753L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -752L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -751L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -750L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -749L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -748L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -747L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -746L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -745L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -744L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -743L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -742L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -741L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -740L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -739L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -738L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -737L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -736L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -735L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -734L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -733L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -732L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -731L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -730L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -729L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -728L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -727L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -726L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -725L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -724L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -723L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -722L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -721L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -720L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -719L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -718L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -717L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -716L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -715L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -714L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -713L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -712L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -711L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -710L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -709L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -708L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -707L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -706L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -705L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -704L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -703L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -702L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -701L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -700L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -699L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -698L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -697L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -696L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -695L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -694L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -693L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -692L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -691L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -690L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -689L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -688L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -687L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -686L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -685L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -684L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -683L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -682L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -681L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -680L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -679L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -678L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -677L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -676L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -675L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -674L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -673L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -672L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -671L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -670L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -669L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -668L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -667L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -666L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -665L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -664L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -663L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -662L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -661L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -660L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -659L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -658L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -657L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -656L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -655L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -654L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -653L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -652L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -651L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -650L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -649L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -648L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -647L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -646L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -645L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -644L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -643L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -642L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -641L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -640L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -639L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -638L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -637L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -636L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -635L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -634L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -633L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -632L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -631L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -630L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -629L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -628L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -627L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -626L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -625L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -624L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -623L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -622L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -621L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -620L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -619L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -618L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -617L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -616L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -615L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -614L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -613L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -612L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -611L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -610L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -609L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -608L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -607L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -606L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -605L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -604L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -603L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -602L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -601L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -600L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -599L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -598L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -597L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -596L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -595L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -594L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -593L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -592L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -591L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -590L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -589L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -588L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -587L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -586L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -585L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -584L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -583L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -582L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -581L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -580L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -579L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -578L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -577L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -576L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -575L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -574L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -573L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -572L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -571L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -570L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -569L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -568L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -567L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -566L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -565L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -564L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -563L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -562L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -561L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -560L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -559L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -558L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -557L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -556L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -555L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -554L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -553L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -552L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -551L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -550L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -549L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -548L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -547L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -546L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -545L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -544L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -543L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -542L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -541L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -540L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -539L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -538L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -537L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -536L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -535L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -534L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -533L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -532L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -531L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -530L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -529L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -528L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -527L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -526L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -525L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -524L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -523L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -522L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -521L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -520L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -519L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -518L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -517L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -516L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -515L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -514L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -513L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -512L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -511L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -510L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -509L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -508L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -507L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -506L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -505L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -504L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -503L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -502L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -501L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -500L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -499L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -498L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -497L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -496L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -495L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -494L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -493L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -492L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -491L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -490L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -489L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -488L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -487L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -486L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -485L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -484L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -483L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -482L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -481L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -480L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -479L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -478L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -477L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -476L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -475L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -474L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -473L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -472L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -471L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -470L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -469L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -468L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -467L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -466L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -465L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -464L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -463L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -462L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -461L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -460L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -459L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -458L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -457L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -456L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -455L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -454L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -453L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -452L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -451L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -450L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -449L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -448L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -447L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -446L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -445L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -444L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -443L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -442L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -441L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -440L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -439L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -438L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -437L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -436L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -435L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -434L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -433L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -432L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -431L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -430L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -429L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -428L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -427L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -426L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -425L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -424L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -423L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -422L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -421L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -420L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -419L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -418L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -417L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -416L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -415L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -414L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -413L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -412L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -411L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -410L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -409L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -408L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -407L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -406L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -405L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -404L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -403L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -402L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -401L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -400L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -399L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -398L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -397L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -396L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -395L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -394L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -393L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -392L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -391L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -390L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -389L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -388L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -387L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -386L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -385L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -384L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -383L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -382L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -381L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -380L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -379L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -378L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -377L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -376L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -375L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -374L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -373L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -372L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -371L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -370L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -369L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -368L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -367L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -366L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -365L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -364L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -363L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -362L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -361L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -360L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -359L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -358L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -357L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -356L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -355L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -354L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -353L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -352L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -351L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -350L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -349L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -348L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -347L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -346L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -345L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -344L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -343L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -342L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -341L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -340L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -339L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -338L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -337L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -336L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -335L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -334L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -333L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -332L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -331L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -330L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -329L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -328L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -327L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -326L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -325L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -324L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -323L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -322L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -321L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -320L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -319L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -318L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -317L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -316L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -315L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -314L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -313L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -312L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -311L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -310L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -309L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -308L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -307L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -306L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -305L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -304L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -303L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -302L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -301L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -300L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -299L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -298L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -297L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -296L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -295L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -294L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -293L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -292L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -291L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -290L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -289L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -288L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -287L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -286L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -285L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -284L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -283L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -282L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -281L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -280L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -279L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -278L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -277L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -276L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -275L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -274L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -273L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -272L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -271L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -270L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -269L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -268L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -267L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -266L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -265L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -264L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -263L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -262L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -261L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -260L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -259L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -258L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -257L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -256L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -255L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -254L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -253L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -252L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -251L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -250L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -249L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -248L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -247L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -246L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -245L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -244L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -243L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -242L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -241L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -240L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -239L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -238L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -237L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -236L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -235L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -234L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -233L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -232L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -231L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -230L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -229L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -228L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -227L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -226L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -225L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -224L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -223L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -222L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -221L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -220L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -219L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -218L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -217L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -216L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -215L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -214L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -213L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -212L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -211L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -210L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -209L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -208L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -207L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -206L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -205L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -204L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -203L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -202L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -201L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -200L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -199L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -198L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -197L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -196L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -195L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -194L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -193L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -192L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -191L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -190L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -189L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -188L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -187L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -186L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -185L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -184L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -183L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -182L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -181L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -180L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -179L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -178L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -177L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -176L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -175L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -174L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -173L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -172L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -171L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -170L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -169L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -168L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -167L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -166L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -165L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -164L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -163L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -162L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -161L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -160L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -159L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -158L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -157L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -156L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -155L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -154L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -153L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -152L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -151L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -150L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -149L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -148L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -147L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -146L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -145L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -144L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -143L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -142L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -141L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -140L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -139L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -138L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -137L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -136L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -135L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -134L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -133L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -132L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -131L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -130L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -129L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -128L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -127L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -126L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -125L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -124L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -123L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -122L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -121L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -120L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -119L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -118L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -117L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -116L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -115L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -114L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -113L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -112L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -111L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -110L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -109L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -108L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -107L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -106L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -105L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -104L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -103L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -102L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -101L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -100L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -99L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -98L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -97L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -96L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -95L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -94L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -93L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -92L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -91L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -90L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -89L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -88L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -87L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -86L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -85L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -84L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -83L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -82L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -81L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -80L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -79L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -78L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -77L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -76L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -75L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -74L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -73L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -72L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -71L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -70L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -69L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -68L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -67L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -66L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -65L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -64L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -63L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -62L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -61L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -60L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -59L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -58L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -57L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -56L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -55L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -54L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -53L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -52L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -51L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -50L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -49L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -48L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -47L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -46L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -45L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -44L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -43L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -42L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -41L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -40L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -39L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -38L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -37L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -36L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -35L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -34L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -33L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -32L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -31L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -30L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -29L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -28L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -27L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -26L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -25L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -24L,
                columns: new[] { "DataType", "FilterType" },
                values: new object[] { "Text", null });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -23L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -22L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -21L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -20L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -19L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -18L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -17L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -16L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -15L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -14L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -13L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -12L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -11L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -10L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -9L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -8L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -7L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -6L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -5L,
                columns: new[] { "DataType", "FilterType", "IsSort" },
                values: new object[] { "Text", null, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -4L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -3L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsSort" },
                values: new object[] { "Text", null, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -2L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });

            migrationBuilder.UpdateData(
                schema: "rpt",
                table: "ReportColumns",
                keyColumn: "Id",
                keyValue: -1L,
                columns: new[] { "DataType", "FilterType", "IsFilter", "IsGroup", "IsSort" },
                values: new object[] { "Text", null, false, false, false });
        }
    }
}
