using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Reporting.Repository.Migrations
{
    public partial class ForceRlsAllSchemas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] schemas = ["acc", "con", "inv", "sal", "pur", "rpt"];
            foreach (var schema in schemas)
            {
                migrationBuilder.Sql($"""
                    DO $$ DECLARE row record;
                    BEGIN
                        FOR row IN SELECT tablename FROM pg_tables WHERE schemaname = '{schema}'
                        LOOP
                            EXECUTE 'ALTER TABLE {schema}."' || row.tablename || '" FORCE ROW LEVEL SECURITY';
                        END LOOP;
                    END; $$;
                    """);
            }
        }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}