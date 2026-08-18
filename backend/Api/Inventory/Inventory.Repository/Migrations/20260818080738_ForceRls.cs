using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Inventory.Repository.Migrations
{
    public partial class ForceRls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ DECLARE row record; BEGIN FOR row IN SELECT tablename FROM pg_tables WHERE schemaname = 'inv' LOOP EXECUTE 'ALTER TABLE inv.\"' || row.tablename || '\" FORCE ROW LEVEL SECURITY'; END LOOP; END; $$;");
        }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
