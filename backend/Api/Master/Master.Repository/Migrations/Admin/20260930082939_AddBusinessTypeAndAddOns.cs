using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AddBusinessTypeAndAddOns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_App_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CustomerId_App_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_CustomerId_App",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000012L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000013L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000014L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000015L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000016L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000017L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000018L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000019L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000020L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000172L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000174L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000175L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000176L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000177L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000178L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000179L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000180L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000182L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000183L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000184L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000185L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000186L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000187L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000188L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000189L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000190L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000193L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000194L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000195L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000196L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000198L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000199L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000200L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000214L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000215L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000216L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000217L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000218L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000219L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000220L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000232L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000233L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000234L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000235L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000236L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000237L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000238L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000239L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000240L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000010001L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000010002L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000193L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000194L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000195L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000196L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000198L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000199L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000200L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2050000231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2060000231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000131L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000132L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000133L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000134L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000135L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000136L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000137L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000138L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000139L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000140L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000151L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000152L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000153L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000154L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000155L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000156L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000157L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000158L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000159L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000160L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000241L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000242L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000243L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000244L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000245L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000246L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000247L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000248L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000249L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000250L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000251L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000252L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000253L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000254L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000255L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000256L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000257L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000258L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000259L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000260L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000261L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000262L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000263L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000264L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000265L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000266L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000267L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000268L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000269L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000270L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000141L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000142L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000143L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000144L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000145L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000146L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000147L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000148L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000149L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000150L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000002);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000004);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000008);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000103);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000106);

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.RenameColumn(
                name: "Apps",
                schema: "mst",
                table: "Permissions",
                newName: "AddOns");

            migrationBuilder.RenameColumn(
                name: "Apps",
                schema: "mst",
                table: "Menus",
                newName: "AddOns");

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "mst",
                table: "Permissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddOns",
                schema: "mst",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "mst",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "mst",
                table: "Menus",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 3,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 4,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 5,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 6,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 7,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 8,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 9,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 10,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 11,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 12,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 13,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 14,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 102,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 103,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 104,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 105,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 106,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 113,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 114,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 115,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 116,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 117,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 118,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 119,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 120,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 121,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 122,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 123,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1002,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1003,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1004,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1005,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1006,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1007,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1008,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1009,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1010,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1011,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1012,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1013,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1014,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1015,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1016,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1017,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1018,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1019,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1020,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1021,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1022,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1023,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1024,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1025,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1026,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1027,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1028,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1029,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1030,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1031,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1032,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1038,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1039,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1079,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1080,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1081,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1082,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1083,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1084,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1085,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1086,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1087,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1088,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1089,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1090,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1091,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1092,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1093,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1106,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1107,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1108,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1109,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1110,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1111,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1112,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1113,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1114,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1115,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1116,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1117,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1118,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1119,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1120,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1121,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1122,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1123,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1124,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1125,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1126,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1127,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1128,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1129,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1130,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1131,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1132,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1133,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 128, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1134,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 128, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1135,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 128, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1136,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 128, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1137,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1138,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1139,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 1,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 2,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 3,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 4,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 5,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 6,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 7,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 9,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 21,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 22,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 23,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 24,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 25,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 26,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 27,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 28,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 29,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 30,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 31,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 32,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 33,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 34,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 35,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 36,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 37,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 38,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 39,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 40,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 41,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 42,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 43,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 44,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 45,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 46,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 47,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 48,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 49,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 50,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 51,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 52,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 53,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 54,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 55,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 56,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 57,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 58,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 59,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 60,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 61,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 62,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 63,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 64,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 65,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 66,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 67,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 68,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 69,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 70,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 71,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 72,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 73,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 74,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 75,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 76,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 77,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 78,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 79,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 80,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 81,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 82,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 83,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 84,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 85,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 86,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 87,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 88,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 89,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 90,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 91,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 92,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 93,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 94,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 95,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 96,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 97,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 98,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 99,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 100,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 101,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 102,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 103,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 104,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 105,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 106,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 107,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 108,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 109,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 110,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 111,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 112,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 113,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 114,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 115,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 116,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 117,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 118,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 119,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 120,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 121,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 122,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 123,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 124,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 125,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 126,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 127,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 128,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 129,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 130,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 3, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 131,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 132,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 133,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 134,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 135,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 136,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 137,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 138,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 139,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 140,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 141,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 142,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 143,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 144,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 145,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 146,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 147,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 148,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 149,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 150,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 151,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 152,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 153,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 154,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 155,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 156,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 157,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 158,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 159,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 160,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 161,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 162,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 163,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 164,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 165,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 166,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 167,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 168,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 169,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 170,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 171,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 172,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 173,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 174,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 175,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 176,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 177,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 178,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 179,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 180,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 181,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 182,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 183,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 184,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 185,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 186,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 187,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 188,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 189,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 190,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 191,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 192,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 193,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 194,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 195,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 196,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 197,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 198,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 199,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 200,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 201,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 202,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 203,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 204,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 205,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 206,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 207,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 208,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 209,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 210,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 211,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 212,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 213,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 214,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 215,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 216,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 217,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 218,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 219,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 220,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 221,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 222,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 223,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 224,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 225,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 226,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 227,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 228,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 229,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 230,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 231,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 232,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 233,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 234,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 235,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 236,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 237,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 238,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 239,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 240,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 241,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 242,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 243,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 244,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 245,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 246,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 247,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 248,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 249,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 250,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 251,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 252,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 253,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 254,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 255,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 256,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 257,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 258,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 259,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 260,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 261,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 262,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 263,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 264,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 265,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 266,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 267,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 268,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 269,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 270,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 271,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 272,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 273,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 274,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 275,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 276,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 277,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 278,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 279,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 280,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10001,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10002,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 0, "School" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10003,
                columns: new[] { "AddOns", "BusinessType" },
                values: new object[] { 64, "General" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000171L,
                column: "RoleId",
                value: 1000104);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 920100121L, null, null, null, null, 121, 1 },
                    { 920100122L, null, null, null, null, 122, 1 },
                    { 920100123L, null, null, null, null, 123, 1 },
                    { 920100124L, null, null, null, null, 124, 1 },
                    { 920100125L, null, null, null, null, 125, 1 },
                    { 920100126L, null, null, null, null, 126, 1 },
                    { 920100127L, null, null, null, null, 127, 1 },
                    { 920100128L, null, null, null, null, 128, 1 },
                    { 920100129L, null, null, null, null, 129, 1 },
                    { 920100130L, null, null, null, null, 130, 1 },
                    { 920100131L, null, null, null, null, 131, 1 },
                    { 920100132L, null, null, null, null, 132, 1 },
                    { 920100133L, null, null, null, null, 133, 1 },
                    { 920100134L, null, null, null, null, 134, 1 },
                    { 920100135L, null, null, null, null, 135, 1 },
                    { 920100136L, null, null, null, null, 136, 1 },
                    { 920100137L, null, null, null, null, 137, 1 },
                    { 920100138L, null, null, null, null, 138, 1 },
                    { 920100139L, null, null, null, null, 139, 1 },
                    { 920100140L, null, null, null, null, 140, 1 },
                    { 920100141L, null, null, null, null, 141, 1 },
                    { 920100142L, null, null, null, null, 142, 1 },
                    { 920100143L, null, null, null, null, 143, 1 },
                    { 920100144L, null, null, null, null, 144, 1 },
                    { 920100145L, null, null, null, null, 145, 1 },
                    { 920100146L, null, null, null, null, 146, 1 },
                    { 920100147L, null, null, null, null, 147, 1 },
                    { 920100148L, null, null, null, null, 148, 1 },
                    { 920100149L, null, null, null, null, 149, 1 },
                    { 920100150L, null, null, null, null, 150, 1 },
                    { 920100151L, null, null, null, null, 151, 1 },
                    { 920100152L, null, null, null, null, 152, 1 },
                    { 920100153L, null, null, null, null, 153, 1 },
                    { 920100154L, null, null, null, null, 154, 1 },
                    { 920100155L, null, null, null, null, 155, 1 },
                    { 920100156L, null, null, null, null, 156, 1 },
                    { 920100157L, null, null, null, null, 157, 1 },
                    { 920100158L, null, null, null, null, 158, 1 },
                    { 920100159L, null, null, null, null, 159, 1 },
                    { 920100160L, null, null, null, null, 160, 1 },
                    { 920100161L, null, null, null, null, 161, 1 },
                    { 920100162L, null, null, null, null, 162, 1 },
                    { 920100163L, null, null, null, null, 163, 1 },
                    { 920100164L, null, null, null, null, 164, 1 },
                    { 920100165L, null, null, null, null, 165, 1 },
                    { 920100166L, null, null, null, null, 166, 1 },
                    { 920100167L, null, null, null, null, 167, 1 },
                    { 920100168L, null, null, null, null, 168, 1 },
                    { 920100169L, null, null, null, null, 169, 1 },
                    { 920100170L, null, null, null, null, 170, 1 },
                    { 920100171L, null, null, null, null, 171, 1 },
                    { 920100172L, null, null, null, null, 172, 1 },
                    { 920100173L, null, null, null, null, 173, 1 },
                    { 920100174L, null, null, null, null, 174, 1 },
                    { 920100175L, null, null, null, null, 175, 1 },
                    { 920100176L, null, null, null, null, 176, 1 },
                    { 920100177L, null, null, null, null, 177, 1 },
                    { 920100178L, null, null, null, null, 178, 1 },
                    { 920100179L, null, null, null, null, 179, 1 },
                    { 920100180L, null, null, null, null, 180, 1 },
                    { 920100181L, null, null, null, null, 181, 1 },
                    { 920100182L, null, null, null, null, 182, 1 },
                    { 920100183L, null, null, null, null, 183, 1 },
                    { 920100184L, null, null, null, null, 184, 1 },
                    { 920100185L, null, null, null, null, 185, 1 },
                    { 920100186L, null, null, null, null, 186, 1 },
                    { 920100187L, null, null, null, null, 187, 1 },
                    { 920100188L, null, null, null, null, 188, 1 },
                    { 920100189L, null, null, null, null, 189, 1 },
                    { 920100190L, null, null, null, null, 190, 1 },
                    { 920100191L, null, null, null, null, 191, 1 },
                    { 920100192L, null, null, null, null, 192, 1 },
                    { 920100193L, null, null, null, null, 193, 1 },
                    { 920100194L, null, null, null, null, 194, 1 },
                    { 920100195L, null, null, null, null, 195, 1 },
                    { 920100196L, null, null, null, null, 196, 1 },
                    { 920100197L, null, null, null, null, 197, 1 },
                    { 920100198L, null, null, null, null, 198, 1 },
                    { 920100199L, null, null, null, null, 199, 1 },
                    { 920100200L, null, null, null, null, 200, 1 },
                    { 920100201L, null, null, null, null, 201, 1 },
                    { 920100202L, null, null, null, null, 202, 1 },
                    { 920100203L, null, null, null, null, 203, 1 },
                    { 920100204L, null, null, null, null, 204, 1 },
                    { 920100205L, null, null, null, null, 205, 1 },
                    { 920100206L, null, null, null, null, 206, 1 },
                    { 920100207L, null, null, null, null, 207, 1 },
                    { 920100208L, null, null, null, null, 208, 1 },
                    { 920100209L, null, null, null, null, 209, 1 },
                    { 920100210L, null, null, null, null, 210, 1 },
                    { 920100211L, null, null, null, null, 211, 1 },
                    { 920100212L, null, null, null, null, 212, 1 },
                    { 920100213L, null, null, null, null, 213, 1 },
                    { 920100214L, null, null, null, null, 214, 1 },
                    { 920100215L, null, null, null, null, 215, 1 },
                    { 920100216L, null, null, null, null, 216, 1 },
                    { 920100217L, null, null, null, null, 217, 1 },
                    { 920100218L, null, null, null, null, 218, 1 },
                    { 920100219L, null, null, null, null, 219, 1 },
                    { 920100220L, null, null, null, null, 220, 1 },
                    { 920100221L, null, null, null, null, 221, 1 },
                    { 920100222L, null, null, null, null, 222, 1 },
                    { 920100223L, null, null, null, null, 223, 1 },
                    { 920100224L, null, null, null, null, 224, 1 },
                    { 920100225L, null, null, null, null, 225, 1 },
                    { 920100226L, null, null, null, null, 226, 1 },
                    { 920100227L, null, null, null, null, 227, 1 },
                    { 920100228L, null, null, null, null, 228, 1 },
                    { 920100229L, null, null, null, null, 229, 1 },
                    { 920100230L, null, null, null, null, 230, 1 },
                    { 920100231L, null, null, null, null, 231, 1 },
                    { 920100232L, null, null, null, null, 232, 1 },
                    { 920100233L, null, null, null, null, 233, 1 },
                    { 920100234L, null, null, null, null, 234, 1 },
                    { 920100235L, null, null, null, null, 235, 1 },
                    { 920100236L, null, null, null, null, 236, 1 },
                    { 920100237L, null, null, null, null, 237, 1 },
                    { 920100238L, null, null, null, null, 238, 1 },
                    { 920100239L, null, null, null, null, 239, 1 },
                    { 920100240L, null, null, null, null, 240, 1 },
                    { 920100241L, null, null, null, null, 241, 1 },
                    { 920100242L, null, null, null, null, 242, 1 },
                    { 920100243L, null, null, null, null, 243, 1 },
                    { 920100244L, null, null, null, null, 244, 1 },
                    { 920100245L, null, null, null, null, 245, 1 },
                    { 920100246L, null, null, null, null, 246, 1 },
                    { 920100247L, null, null, null, null, 247, 1 },
                    { 920100248L, null, null, null, null, 248, 1 },
                    { 920100249L, null, null, null, null, 249, 1 },
                    { 920100250L, null, null, null, null, 250, 1 },
                    { 920100251L, null, null, null, null, 251, 1 },
                    { 920100252L, null, null, null, null, 252, 1 },
                    { 920100253L, null, null, null, null, 253, 1 },
                    { 920100254L, null, null, null, null, 254, 1 },
                    { 920100255L, null, null, null, null, 255, 1 },
                    { 920100256L, null, null, null, null, 256, 1 },
                    { 920100257L, null, null, null, null, 257, 1 },
                    { 920100258L, null, null, null, null, 258, 1 },
                    { 920100259L, null, null, null, null, 259, 1 },
                    { 920100260L, null, null, null, null, 260, 1 },
                    { 920100261L, null, null, null, null, 261, 1 },
                    { 920100262L, null, null, null, null, 262, 1 },
                    { 920100263L, null, null, null, null, 263, 1 },
                    { 920100264L, null, null, null, null, 264, 1 },
                    { 920100265L, null, null, null, null, 265, 1 },
                    { 920100266L, null, null, null, null, 266, 1 },
                    { 920100267L, null, null, null, null, 267, 1 },
                    { 920100268L, null, null, null, null, 268, 1 },
                    { 920100269L, null, null, null, null, 269, 1 },
                    { 920100270L, null, null, null, null, 270, 1 },
                    { 920110001L, null, null, null, null, 10001, 1 },
                    { 920110002L, null, null, null, null, 10002, 1 },
                    { 920200121L, null, null, null, null, 121, 2 },
                    { 920200122L, null, null, null, null, 122, 2 },
                    { 920200123L, null, null, null, null, 123, 2 },
                    { 920200124L, null, null, null, null, 124, 2 },
                    { 920200125L, null, null, null, null, 125, 2 },
                    { 920200126L, null, null, null, null, 126, 2 },
                    { 920200127L, null, null, null, null, 127, 2 },
                    { 920200128L, null, null, null, null, 128, 2 },
                    { 920200129L, null, null, null, null, 129, 2 },
                    { 920200130L, null, null, null, null, 130, 2 },
                    { 920200131L, null, null, null, null, 131, 2 },
                    { 920200132L, null, null, null, null, 132, 2 },
                    { 920200133L, null, null, null, null, 133, 2 },
                    { 920200134L, null, null, null, null, 134, 2 },
                    { 920200135L, null, null, null, null, 135, 2 },
                    { 920200136L, null, null, null, null, 136, 2 },
                    { 920200137L, null, null, null, null, 137, 2 },
                    { 920200138L, null, null, null, null, 138, 2 },
                    { 920200139L, null, null, null, null, 139, 2 },
                    { 920200140L, null, null, null, null, 140, 2 },
                    { 920200141L, null, null, null, null, 141, 2 },
                    { 920200142L, null, null, null, null, 142, 2 },
                    { 920200143L, null, null, null, null, 143, 2 },
                    { 920200144L, null, null, null, null, 144, 2 },
                    { 920200145L, null, null, null, null, 145, 2 },
                    { 920200146L, null, null, null, null, 146, 2 },
                    { 920200147L, null, null, null, null, 147, 2 },
                    { 920200148L, null, null, null, null, 148, 2 },
                    { 920200149L, null, null, null, null, 149, 2 },
                    { 920200150L, null, null, null, null, 150, 2 },
                    { 920200151L, null, null, null, null, 151, 2 },
                    { 920200152L, null, null, null, null, 152, 2 },
                    { 920200153L, null, null, null, null, 153, 2 },
                    { 920200154L, null, null, null, null, 154, 2 },
                    { 920200155L, null, null, null, null, 155, 2 },
                    { 920200156L, null, null, null, null, 156, 2 },
                    { 920200157L, null, null, null, null, 157, 2 },
                    { 920200158L, null, null, null, null, 158, 2 },
                    { 920200159L, null, null, null, null, 159, 2 },
                    { 920200160L, null, null, null, null, 160, 2 },
                    { 920200161L, null, null, null, null, 161, 2 },
                    { 920200162L, null, null, null, null, 162, 2 },
                    { 920200163L, null, null, null, null, 163, 2 },
                    { 920200164L, null, null, null, null, 164, 2 },
                    { 920200165L, null, null, null, null, 165, 2 },
                    { 920200166L, null, null, null, null, 166, 2 },
                    { 920200167L, null, null, null, null, 167, 2 },
                    { 920200168L, null, null, null, null, 168, 2 },
                    { 920200169L, null, null, null, null, 169, 2 },
                    { 920200170L, null, null, null, null, 170, 2 },
                    { 920200171L, null, null, null, null, 171, 2 },
                    { 920200172L, null, null, null, null, 172, 2 },
                    { 920200173L, null, null, null, null, 173, 2 },
                    { 920200174L, null, null, null, null, 174, 2 },
                    { 920200175L, null, null, null, null, 175, 2 },
                    { 920200176L, null, null, null, null, 176, 2 },
                    { 920200177L, null, null, null, null, 177, 2 },
                    { 920200178L, null, null, null, null, 178, 2 },
                    { 920200179L, null, null, null, null, 179, 2 },
                    { 920200180L, null, null, null, null, 180, 2 },
                    { 920200181L, null, null, null, null, 181, 2 },
                    { 920200182L, null, null, null, null, 182, 2 },
                    { 920200183L, null, null, null, null, 183, 2 },
                    { 920200184L, null, null, null, null, 184, 2 },
                    { 920200185L, null, null, null, null, 185, 2 },
                    { 920200186L, null, null, null, null, 186, 2 },
                    { 920200187L, null, null, null, null, 187, 2 },
                    { 920200188L, null, null, null, null, 188, 2 },
                    { 920200189L, null, null, null, null, 189, 2 },
                    { 920200190L, null, null, null, null, 190, 2 },
                    { 920200191L, null, null, null, null, 191, 2 },
                    { 920200192L, null, null, null, null, 192, 2 },
                    { 920200193L, null, null, null, null, 193, 2 },
                    { 920200194L, null, null, null, null, 194, 2 },
                    { 920200195L, null, null, null, null, 195, 2 },
                    { 920200196L, null, null, null, null, 196, 2 },
                    { 920200197L, null, null, null, null, 197, 2 },
                    { 920200198L, null, null, null, null, 198, 2 },
                    { 920200199L, null, null, null, null, 199, 2 },
                    { 920200200L, null, null, null, null, 200, 2 },
                    { 920200201L, null, null, null, null, 201, 2 },
                    { 920200202L, null, null, null, null, 202, 2 },
                    { 920200203L, null, null, null, null, 203, 2 },
                    { 920200204L, null, null, null, null, 204, 2 },
                    { 920200205L, null, null, null, null, 205, 2 },
                    { 920200206L, null, null, null, null, 206, 2 },
                    { 920200207L, null, null, null, null, 207, 2 },
                    { 920200208L, null, null, null, null, 208, 2 },
                    { 920200209L, null, null, null, null, 209, 2 },
                    { 920200210L, null, null, null, null, 210, 2 },
                    { 920200211L, null, null, null, null, 211, 2 },
                    { 920200212L, null, null, null, null, 212, 2 },
                    { 920200213L, null, null, null, null, 213, 2 },
                    { 920200214L, null, null, null, null, 214, 2 },
                    { 920200215L, null, null, null, null, 215, 2 },
                    { 920200216L, null, null, null, null, 216, 2 },
                    { 920200217L, null, null, null, null, 217, 2 },
                    { 920200218L, null, null, null, null, 218, 2 },
                    { 920200219L, null, null, null, null, 219, 2 },
                    { 920200220L, null, null, null, null, 220, 2 },
                    { 920200221L, null, null, null, null, 221, 2 },
                    { 920200222L, null, null, null, null, 222, 2 },
                    { 920200223L, null, null, null, null, 223, 2 },
                    { 920200224L, null, null, null, null, 224, 2 },
                    { 920200225L, null, null, null, null, 225, 2 },
                    { 920200226L, null, null, null, null, 226, 2 },
                    { 920200227L, null, null, null, null, 227, 2 },
                    { 920200228L, null, null, null, null, 228, 2 },
                    { 920200229L, null, null, null, null, 229, 2 },
                    { 920200230L, null, null, null, null, 230, 2 },
                    { 920200231L, null, null, null, null, 231, 2 },
                    { 920200232L, null, null, null, null, 232, 2 },
                    { 920200233L, null, null, null, null, 233, 2 },
                    { 920200234L, null, null, null, null, 234, 2 },
                    { 920200235L, null, null, null, null, 235, 2 },
                    { 920200236L, null, null, null, null, 236, 2 },
                    { 920200237L, null, null, null, null, 237, 2 },
                    { 920200238L, null, null, null, null, 238, 2 },
                    { 920200239L, null, null, null, null, 239, 2 },
                    { 920200240L, null, null, null, null, 240, 2 },
                    { 920200241L, null, null, null, null, 241, 2 },
                    { 920200242L, null, null, null, null, 242, 2 },
                    { 920200243L, null, null, null, null, 243, 2 },
                    { 920200244L, null, null, null, null, 244, 2 },
                    { 920200245L, null, null, null, null, 245, 2 },
                    { 920200246L, null, null, null, null, 246, 2 },
                    { 920200247L, null, null, null, null, 247, 2 },
                    { 920200248L, null, null, null, null, 248, 2 },
                    { 920200249L, null, null, null, null, 249, 2 },
                    { 920200250L, null, null, null, null, 250, 2 },
                    { 920200251L, null, null, null, null, 251, 2 },
                    { 920200252L, null, null, null, null, 252, 2 },
                    { 920200253L, null, null, null, null, 253, 2 },
                    { 920200254L, null, null, null, null, 254, 2 },
                    { 920200255L, null, null, null, null, 255, 2 },
                    { 920200256L, null, null, null, null, 256, 2 },
                    { 920200257L, null, null, null, null, 257, 2 },
                    { 920200258L, null, null, null, null, 258, 2 },
                    { 920200259L, null, null, null, null, 259, 2 },
                    { 920200260L, null, null, null, null, 260, 2 },
                    { 920200261L, null, null, null, null, 261, 2 },
                    { 920200262L, null, null, null, null, 262, 2 },
                    { 920200263L, null, null, null, null, 263, 2 },
                    { 920200264L, null, null, null, null, 264, 2 },
                    { 920200265L, null, null, null, null, 265, 2 },
                    { 920200266L, null, null, null, null, 266, 2 },
                    { 920200267L, null, null, null, null, 267, 2 },
                    { 920200268L, null, null, null, null, 268, 2 },
                    { 920200269L, null, null, null, null, 269, 2 },
                    { 920200270L, null, null, null, null, 270, 2 },
                    { 920210001L, null, null, null, null, 10001, 2 },
                    { 920210002L, null, null, null, null, 10002, 2 },
                    { 2030000161L, null, null, null, null, 161, 1000104 },
                    { 2030000162L, null, null, null, null, 162, 1000104 },
                    { 2030000163L, null, null, null, null, 163, 1000104 },
                    { 2030000173L, null, null, null, null, 173, 1000104 },
                    { 2040000011L, null, null, null, null, 11, 1000105 },
                    { 2040000121L, null, null, null, null, 121, 1000105 },
                    { 2040000201L, null, null, null, null, 201, 1000105 },
                    { 2040000202L, null, null, null, null, 202, 1000105 },
                    { 2040000203L, null, null, null, null, 203, 1000105 },
                    { 2040000204L, null, null, null, null, 204, 1000105 },
                    { 2040000205L, null, null, null, null, 205, 1000105 },
                    { 2040000206L, null, null, null, null, 206, 1000105 },
                    { 2040000207L, null, null, null, null, 207, 1000105 },
                    { 2040000208L, null, null, null, null, 208, 1000105 },
                    { 2040000209L, null, null, null, null, 209, 1000105 },
                    { 2040000210L, null, null, null, null, 210, 1000105 },
                    { 2040000211L, null, null, null, null, 211, 1000105 },
                    { 2040000212L, null, null, null, null, 212, 1000105 },
                    { 2040000213L, null, null, null, null, 213, 1000105 },
                    { 2040000221L, null, null, null, null, 221, 1000105 },
                    { 2040000222L, null, null, null, null, 222, 1000105 },
                    { 2040000223L, null, null, null, null, 223, 1000105 },
                    { 2040000224L, null, null, null, null, 224, 1000105 },
                    { 2040000225L, null, null, null, null, 225, 1000105 },
                    { 2040000226L, null, null, null, null, 226, 1000105 },
                    { 2040000227L, null, null, null, null, 227, 1000105 },
                    { 2040000228L, null, null, null, null, 228, 1000105 },
                    { 2040000229L, null, null, null, null, 229, 1000105 },
                    { 2040000230L, null, null, null, null, 230, 1000105 },
                    { 2040000231L, null, null, null, null, 231, 1000105 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "CustomerId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemName",
                schema: "mst",
                table: "Roles",
                column: "SystemName",
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerId",
                schema: "mst",
                table: "Licenses",
                column: "CustomerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_SystemName",
                schema: "mst",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_CustomerId",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100131L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100132L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100133L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100134L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100135L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100136L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100137L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100138L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100139L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100140L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100141L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100142L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100143L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100144L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100145L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100146L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100147L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100148L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100149L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100150L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100151L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100152L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100153L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100154L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100155L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100156L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100157L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100158L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100159L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100160L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100172L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100174L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100175L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100176L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100177L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100178L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100179L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100180L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100182L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100183L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100184L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100185L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100186L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100187L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100188L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100189L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100190L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100193L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100194L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100195L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100196L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100198L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100199L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100200L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100214L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100215L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100216L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100217L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100218L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100219L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100220L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100232L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100233L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100234L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100235L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100236L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100237L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100238L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100239L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100240L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100241L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100242L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100243L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100244L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100245L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100246L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100247L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100248L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100249L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100250L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100251L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100252L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100253L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100254L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100255L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100256L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100257L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100258L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100259L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100260L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100261L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100262L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100263L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100264L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100265L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100266L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100267L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100268L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100269L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100270L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920110001L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920110002L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200131L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200132L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200133L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200134L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200135L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200136L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200137L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200138L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200139L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200140L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200141L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200142L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200143L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200144L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200145L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200146L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200147L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200148L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200149L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200150L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200151L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200152L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200153L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200154L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200155L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200156L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200157L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200158L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200159L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200160L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200172L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200174L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200175L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200176L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200177L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200178L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200179L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200180L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200182L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200183L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200184L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200185L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200186L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200187L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200188L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200189L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200190L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200193L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200194L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200195L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200196L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200198L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200199L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200200L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200214L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200215L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200216L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200217L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200218L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200219L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200220L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200232L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200233L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200234L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200235L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200236L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200237L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200238L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200239L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200240L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200241L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200242L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200243L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200244L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200245L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200246L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200247L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200248L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200249L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200250L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200251L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200252L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200253L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200254L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200255L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200256L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200257L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200258L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200259L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200260L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200261L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200262L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200263L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200264L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200265L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200266L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200267L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200268L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200269L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200270L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920210001L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920210002L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2040000231L);

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "mst",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "AddOns",
                schema: "mst",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "mst",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "mst",
                table: "Menus");

            migrationBuilder.RenameColumn(
                name: "AddOns",
                schema: "mst",
                table: "Permissions",
                newName: "Apps");

            migrationBuilder.RenameColumn(
                name: "AddOns",
                schema: "mst",
                table: "Menus",
                newName: "Apps");

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "RefreshTokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 3,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 4,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 5,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 6,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 7,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 8,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 9,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 10,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 11,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 12,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 13,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 14,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 106,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 107,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 108,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 109,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 110,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 111,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 112,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 113,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 114,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 115,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 116,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 117,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 118,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 119,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 120,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 121,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 122,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 123,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1002,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1003,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1004,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1005,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1006,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1007,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1008,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1009,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1010,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1011,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1012,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1013,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1014,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1015,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1016,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1017,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1018,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1019,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1020,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1021,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1022,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1023,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1024,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1025,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1026,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1027,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1028,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1029,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1030,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1031,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1032,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1038,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1039,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1079,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1080,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1081,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1082,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1083,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1084,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1085,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1086,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1087,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1088,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1089,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1090,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1091,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1092,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1093,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1106,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1107,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1108,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1109,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1110,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1111,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1112,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1113,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1114,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1115,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1116,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1117,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1118,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1119,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1120,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1121,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1122,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1123,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1124,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1125,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1126,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1127,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1128,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1129,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1130,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1131,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1132,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1133,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1134,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1135,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1136,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1137,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1138,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1139,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 1,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 2,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 3,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 4,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 5,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 6,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 7,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 9,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 21,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 22,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 23,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 24,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 25,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 26,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 27,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 28,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 29,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 30,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 31,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 32,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 33,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 34,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 35,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 36,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 37,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 38,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 39,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 40,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 41,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 42,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 43,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 44,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 45,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 46,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 47,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 48,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 49,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 50,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 51,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 52,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 53,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 54,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 55,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 56,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 57,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 58,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 59,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 60,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 61,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 62,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 63,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 64,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 65,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 66,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 67,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 68,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 69,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 70,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 71,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 72,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 73,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 74,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 75,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 76,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 77,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 78,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 79,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 80,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 81,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 82,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 83,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 84,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 85,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 86,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 87,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 88,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 89,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 90,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 91,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 92,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 93,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 94,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 95,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 96,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 97,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 98,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 99,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 100,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 102,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 103,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 104,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 105,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 106,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 107,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 108,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 109,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 110,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 111,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 112,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 113,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 114,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 115,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 116,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 117,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 118,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 119,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 120,
                column: "Apps",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 121,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 122,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 123,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 124,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 125,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 126,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 127,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 128,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 129,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 130,
                column: "Apps",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 131,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 132,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 133,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 134,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 135,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 136,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 137,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 138,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 139,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 140,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 141,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 142,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 143,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 144,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 145,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 146,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 147,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 148,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 149,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 150,
                column: "Apps",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 151,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 152,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 153,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 154,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 155,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 156,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 157,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 158,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 159,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 160,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 161,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 162,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 163,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 164,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 165,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 166,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 167,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 168,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 169,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 170,
                column: "Apps",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 171,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 172,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 173,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 174,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 175,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 176,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 177,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 178,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 179,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 180,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 181,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 182,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 183,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 184,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 185,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 186,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 187,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 188,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 189,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 190,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 191,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 192,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 193,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 194,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 195,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 196,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 197,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 198,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 199,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 200,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 201,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 202,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 203,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 204,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 205,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 206,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 207,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 208,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 209,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 210,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 211,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 212,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 213,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 214,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 215,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 216,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 217,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 218,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 219,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 220,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 221,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 222,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 223,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 224,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 225,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 226,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 227,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 228,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 229,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 230,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 231,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 232,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 233,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 234,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 235,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 236,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 237,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 238,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 239,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 240,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 241,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 242,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 243,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 244,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 245,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 246,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 247,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 248,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 249,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 250,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 251,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 252,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 253,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 254,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 255,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 256,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 257,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 258,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 259,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 260,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 261,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 262,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 263,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 264,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 265,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 266,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 267,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 268,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 269,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 270,
                column: "Apps",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 271,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 272,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 273,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 274,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 275,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 276,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 277,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 278,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 279,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 280,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10001,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10002,
                column: "Apps",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10003,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000171L,
                column: "RoleId",
                value: 1000103);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 2000000011L, null, null, null, null, 11, 1000002 },
                    { 2000000012L, null, null, null, null, 12, 1000002 },
                    { 2000000013L, null, null, null, null, 13, 1000002 },
                    { 2000000014L, null, null, null, null, 14, 1000002 },
                    { 2000000015L, null, null, null, null, 15, 1000002 },
                    { 2000000016L, null, null, null, null, 16, 1000002 },
                    { 2000000017L, null, null, null, null, 17, 1000002 },
                    { 2000000018L, null, null, null, null, 18, 1000002 },
                    { 2000000019L, null, null, null, null, 19, 1000002 },
                    { 2000000020L, null, null, null, null, 20, 1000002 },
                    { 2000000091L, null, null, null, null, 91, 1000002 },
                    { 2000000092L, null, null, null, null, 92, 1000002 },
                    { 2000000093L, null, null, null, null, 93, 1000002 },
                    { 2000000094L, null, null, null, null, 94, 1000002 },
                    { 2000000095L, null, null, null, null, 95, 1000002 },
                    { 2000000096L, null, null, null, null, 96, 1000002 },
                    { 2000000097L, null, null, null, null, 97, 1000002 },
                    { 2000000098L, null, null, null, null, 98, 1000002 },
                    { 2000000099L, null, null, null, null, 99, 1000002 },
                    { 2000000100L, null, null, null, null, 100, 1000002 },
                    { 2000000121L, null, null, null, null, 121, 1000002 },
                    { 2000000122L, null, null, null, null, 122, 1000002 },
                    { 2000000123L, null, null, null, null, 123, 1000002 },
                    { 2000000124L, null, null, null, null, 124, 1000002 },
                    { 2000000125L, null, null, null, null, 125, 1000002 },
                    { 2000000126L, null, null, null, null, 126, 1000002 },
                    { 2000000127L, null, null, null, null, 127, 1000002 },
                    { 2000000128L, null, null, null, null, 128, 1000002 },
                    { 2000000129L, null, null, null, null, 129, 1000002 },
                    { 2000000130L, null, null, null, null, 130, 1000002 },
                    { 2000000161L, null, null, null, null, 161, 1000002 },
                    { 2000000162L, null, null, null, null, 162, 1000002 },
                    { 2000000163L, null, null, null, null, 163, 1000002 },
                    { 2000000164L, null, null, null, null, 164, 1000002 },
                    { 2000000165L, null, null, null, null, 165, 1000002 },
                    { 2000000166L, null, null, null, null, 166, 1000002 },
                    { 2000000167L, null, null, null, null, 167, 1000002 },
                    { 2000000168L, null, null, null, null, 168, 1000002 },
                    { 2000000169L, null, null, null, null, 169, 1000002 },
                    { 2000000170L, null, null, null, null, 170, 1000002 },
                    { 2000000171L, null, null, null, null, 171, 1000002 },
                    { 2000000172L, null, null, null, null, 172, 1000002 },
                    { 2000000173L, null, null, null, null, 173, 1000002 },
                    { 2000000174L, null, null, null, null, 174, 1000002 },
                    { 2000000175L, null, null, null, null, 175, 1000002 },
                    { 2000000176L, null, null, null, null, 176, 1000002 },
                    { 2000000177L, null, null, null, null, 177, 1000002 },
                    { 2000000178L, null, null, null, null, 178, 1000002 },
                    { 2000000179L, null, null, null, null, 179, 1000002 },
                    { 2000000180L, null, null, null, null, 180, 1000002 },
                    { 2000000181L, null, null, null, null, 181, 1000002 },
                    { 2000000182L, null, null, null, null, 182, 1000002 },
                    { 2000000183L, null, null, null, null, 183, 1000002 },
                    { 2000000184L, null, null, null, null, 184, 1000002 },
                    { 2000000185L, null, null, null, null, 185, 1000002 },
                    { 2000000186L, null, null, null, null, 186, 1000002 },
                    { 2000000187L, null, null, null, null, 187, 1000002 },
                    { 2000000188L, null, null, null, null, 188, 1000002 },
                    { 2000000189L, null, null, null, null, 189, 1000002 },
                    { 2000000190L, null, null, null, null, 190, 1000002 },
                    { 2000000191L, null, null, null, null, 191, 1000002 },
                    { 2000000192L, null, null, null, null, 192, 1000002 },
                    { 2000000193L, null, null, null, null, 193, 1000002 },
                    { 2000000194L, null, null, null, null, 194, 1000002 },
                    { 2000000195L, null, null, null, null, 195, 1000002 },
                    { 2000000196L, null, null, null, null, 196, 1000002 },
                    { 2000000197L, null, null, null, null, 197, 1000002 },
                    { 2000000198L, null, null, null, null, 198, 1000002 },
                    { 2000000199L, null, null, null, null, 199, 1000002 },
                    { 2000000200L, null, null, null, null, 200, 1000002 },
                    { 2000000201L, null, null, null, null, 201, 1000002 },
                    { 2000000202L, null, null, null, null, 202, 1000002 },
                    { 2000000203L, null, null, null, null, 203, 1000002 },
                    { 2000000204L, null, null, null, null, 204, 1000002 },
                    { 2000000205L, null, null, null, null, 205, 1000002 },
                    { 2000000206L, null, null, null, null, 206, 1000002 },
                    { 2000000207L, null, null, null, null, 207, 1000002 },
                    { 2000000208L, null, null, null, null, 208, 1000002 },
                    { 2000000209L, null, null, null, null, 209, 1000002 },
                    { 2000000210L, null, null, null, null, 210, 1000002 },
                    { 2000000211L, null, null, null, null, 211, 1000002 },
                    { 2000000212L, null, null, null, null, 212, 1000002 },
                    { 2000000213L, null, null, null, null, 213, 1000002 },
                    { 2000000214L, null, null, null, null, 214, 1000002 },
                    { 2000000215L, null, null, null, null, 215, 1000002 },
                    { 2000000216L, null, null, null, null, 216, 1000002 },
                    { 2000000217L, null, null, null, null, 217, 1000002 },
                    { 2000000218L, null, null, null, null, 218, 1000002 },
                    { 2000000219L, null, null, null, null, 219, 1000002 },
                    { 2000000220L, null, null, null, null, 220, 1000002 },
                    { 2000000221L, null, null, null, null, 221, 1000002 },
                    { 2000000222L, null, null, null, null, 222, 1000002 },
                    { 2000000223L, null, null, null, null, 223, 1000002 },
                    { 2000000224L, null, null, null, null, 224, 1000002 },
                    { 2000000225L, null, null, null, null, 225, 1000002 },
                    { 2000000226L, null, null, null, null, 226, 1000002 },
                    { 2000000227L, null, null, null, null, 227, 1000002 },
                    { 2000000228L, null, null, null, null, 228, 1000002 },
                    { 2000000229L, null, null, null, null, 229, 1000002 },
                    { 2000000230L, null, null, null, null, 230, 1000002 },
                    { 2000000231L, null, null, null, null, 231, 1000002 },
                    { 2000000232L, null, null, null, null, 232, 1000002 },
                    { 2000000233L, null, null, null, null, 233, 1000002 },
                    { 2000000234L, null, null, null, null, 234, 1000002 },
                    { 2000000235L, null, null, null, null, 235, 1000002 },
                    { 2000000236L, null, null, null, null, 236, 1000002 },
                    { 2000000237L, null, null, null, null, 237, 1000002 },
                    { 2000000238L, null, null, null, null, 238, 1000002 },
                    { 2000000239L, null, null, null, null, 239, 1000002 },
                    { 2000000240L, null, null, null, null, 240, 1000002 },
                    { 2000010001L, null, null, null, null, 10001, 1000002 },
                    { 2000010002L, null, null, null, null, 10002, 1000002 },
                    { 2030000011L, null, null, null, null, 11, 1000103 },
                    { 2030000181L, null, null, null, null, 181, 1000103 },
                    { 2030000191L, null, null, null, null, 191, 1000103 },
                    { 2030000192L, null, null, null, null, 192, 1000103 },
                    { 2030000193L, null, null, null, null, 193, 1000103 },
                    { 2030000194L, null, null, null, null, 194, 1000103 },
                    { 2030000195L, null, null, null, null, 195, 1000103 },
                    { 2030000196L, null, null, null, null, 196, 1000103 },
                    { 2030000197L, null, null, null, null, 197, 1000103 },
                    { 2030000198L, null, null, null, null, 198, 1000103 },
                    { 2030000199L, null, null, null, null, 199, 1000103 },
                    { 2030000200L, null, null, null, null, 200, 1000103 },
                    { 2040000161L, null, null, null, null, 161, 1000104 },
                    { 2040000162L, null, null, null, null, 162, 1000104 },
                    { 2040000163L, null, null, null, null, 163, 1000104 },
                    { 2040000171L, null, null, null, null, 171, 1000104 },
                    { 2040000173L, null, null, null, null, 173, 1000104 },
                    { 2050000011L, null, null, null, null, 11, 1000105 },
                    { 2050000121L, null, null, null, null, 121, 1000105 },
                    { 2050000201L, null, null, null, null, 201, 1000105 },
                    { 2050000202L, null, null, null, null, 202, 1000105 },
                    { 2050000203L, null, null, null, null, 203, 1000105 },
                    { 2050000204L, null, null, null, null, 204, 1000105 },
                    { 2050000205L, null, null, null, null, 205, 1000105 },
                    { 2050000206L, null, null, null, null, 206, 1000105 },
                    { 2050000207L, null, null, null, null, 207, 1000105 },
                    { 2050000208L, null, null, null, null, 208, 1000105 },
                    { 2050000209L, null, null, null, null, 209, 1000105 },
                    { 2050000210L, null, null, null, null, 210, 1000105 },
                    { 2050000211L, null, null, null, null, 211, 1000105 },
                    { 2050000212L, null, null, null, null, 212, 1000105 },
                    { 2050000213L, null, null, null, null, 213, 1000105 },
                    { 2050000221L, null, null, null, null, 221, 1000105 },
                    { 2050000222L, null, null, null, null, 222, 1000105 },
                    { 2050000223L, null, null, null, null, 223, 1000105 },
                    { 2050000224L, null, null, null, null, 224, 1000105 },
                    { 2050000225L, null, null, null, null, 225, 1000105 },
                    { 2050000226L, null, null, null, null, 226, 1000105 },
                    { 2050000227L, null, null, null, null, 227, 1000105 },
                    { 2050000228L, null, null, null, null, 228, 1000105 },
                    { 2050000229L, null, null, null, null, 229, 1000105 },
                    { 2050000230L, null, null, null, null, 230, 1000105 },
                    { 2050000231L, null, null, null, null, 231, 1000105 },
                    { 2060000011L, null, null, null, null, 11, 1000106 },
                    { 2060000091L, null, null, null, null, 91, 1000106 },
                    { 2060000121L, null, null, null, null, 121, 1000106 },
                    { 2060000161L, null, null, null, null, 161, 1000106 },
                    { 2060000171L, null, null, null, null, 171, 1000106 },
                    { 2060000181L, null, null, null, null, 181, 1000106 },
                    { 2060000191L, null, null, null, null, 191, 1000106 },
                    { 2060000201L, null, null, null, null, 201, 1000106 },
                    { 2060000211L, null, null, null, null, 211, 1000106 },
                    { 2060000221L, null, null, null, null, 221, 1000106 },
                    { 2060000231L, null, null, null, null, 231, 1000106 },
                    { 4000000091L, null, null, null, null, 91, 1000004 },
                    { 4000000092L, null, null, null, null, 92, 1000004 },
                    { 4000000093L, null, null, null, null, 93, 1000004 },
                    { 4000000094L, null, null, null, null, 94, 1000004 },
                    { 4000000095L, null, null, null, null, 95, 1000004 },
                    { 4000000096L, null, null, null, null, 96, 1000004 },
                    { 4000000097L, null, null, null, null, 97, 1000004 },
                    { 4000000098L, null, null, null, null, 98, 1000004 },
                    { 4000000099L, null, null, null, null, 99, 1000004 },
                    { 4000000100L, null, null, null, null, 100, 1000004 },
                    { 4000000121L, null, null, null, null, 121, 1000004 },
                    { 4000000122L, null, null, null, null, 122, 1000004 },
                    { 4000000123L, null, null, null, null, 123, 1000004 },
                    { 4000000124L, null, null, null, null, 124, 1000004 },
                    { 4000000125L, null, null, null, null, 125, 1000004 },
                    { 4000000126L, null, null, null, null, 126, 1000004 },
                    { 4000000127L, null, null, null, null, 127, 1000004 },
                    { 4000000128L, null, null, null, null, 128, 1000004 },
                    { 4000000129L, null, null, null, null, 129, 1000004 },
                    { 4000000130L, null, null, null, null, 130, 1000004 },
                    { 4000000131L, null, null, null, null, 131, 1000004 },
                    { 4000000132L, null, null, null, null, 132, 1000004 },
                    { 4000000133L, null, null, null, null, 133, 1000004 },
                    { 4000000134L, null, null, null, null, 134, 1000004 },
                    { 4000000135L, null, null, null, null, 135, 1000004 },
                    { 4000000136L, null, null, null, null, 136, 1000004 },
                    { 4000000137L, null, null, null, null, 137, 1000004 },
                    { 4000000138L, null, null, null, null, 138, 1000004 },
                    { 4000000139L, null, null, null, null, 139, 1000004 },
                    { 4000000140L, null, null, null, null, 140, 1000004 },
                    { 4000000151L, null, null, null, null, 151, 1000004 },
                    { 4000000152L, null, null, null, null, 152, 1000004 },
                    { 4000000153L, null, null, null, null, 153, 1000004 },
                    { 4000000154L, null, null, null, null, 154, 1000004 },
                    { 4000000155L, null, null, null, null, 155, 1000004 },
                    { 4000000156L, null, null, null, null, 156, 1000004 },
                    { 4000000157L, null, null, null, null, 157, 1000004 },
                    { 4000000158L, null, null, null, null, 158, 1000004 },
                    { 4000000159L, null, null, null, null, 159, 1000004 },
                    { 4000000160L, null, null, null, null, 160, 1000004 },
                    { 4000000161L, null, null, null, null, 161, 1000004 },
                    { 4000000162L, null, null, null, null, 162, 1000004 },
                    { 4000000163L, null, null, null, null, 163, 1000004 },
                    { 4000000164L, null, null, null, null, 164, 1000004 },
                    { 4000000165L, null, null, null, null, 165, 1000004 },
                    { 4000000166L, null, null, null, null, 166, 1000004 },
                    { 4000000167L, null, null, null, null, 167, 1000004 },
                    { 4000000168L, null, null, null, null, 168, 1000004 },
                    { 4000000169L, null, null, null, null, 169, 1000004 },
                    { 4000000170L, null, null, null, null, 170, 1000004 },
                    { 4000000241L, null, null, null, null, 241, 1000004 },
                    { 4000000242L, null, null, null, null, 242, 1000004 },
                    { 4000000243L, null, null, null, null, 243, 1000004 },
                    { 4000000244L, null, null, null, null, 244, 1000004 },
                    { 4000000245L, null, null, null, null, 245, 1000004 },
                    { 4000000246L, null, null, null, null, 246, 1000004 },
                    { 4000000247L, null, null, null, null, 247, 1000004 },
                    { 4000000248L, null, null, null, null, 248, 1000004 },
                    { 4000000249L, null, null, null, null, 249, 1000004 },
                    { 4000000250L, null, null, null, null, 250, 1000004 },
                    { 4000000251L, null, null, null, null, 251, 1000004 },
                    { 4000000252L, null, null, null, null, 252, 1000004 },
                    { 4000000253L, null, null, null, null, 253, 1000004 },
                    { 4000000254L, null, null, null, null, 254, 1000004 },
                    { 4000000255L, null, null, null, null, 255, 1000004 },
                    { 4000000256L, null, null, null, null, 256, 1000004 },
                    { 4000000257L, null, null, null, null, 257, 1000004 },
                    { 4000000258L, null, null, null, null, 258, 1000004 },
                    { 4000000259L, null, null, null, null, 259, 1000004 },
                    { 4000000260L, null, null, null, null, 260, 1000004 },
                    { 4000000261L, null, null, null, null, 261, 1000004 },
                    { 4000000262L, null, null, null, null, 262, 1000004 },
                    { 4000000263L, null, null, null, null, 263, 1000004 },
                    { 4000000264L, null, null, null, null, 264, 1000004 },
                    { 4000000265L, null, null, null, null, 265, 1000004 },
                    { 4000000266L, null, null, null, null, 266, 1000004 },
                    { 4000000267L, null, null, null, null, 267, 1000004 },
                    { 4000000268L, null, null, null, null, 268, 1000004 },
                    { 4000000269L, null, null, null, null, 269, 1000004 },
                    { 4000000270L, null, null, null, null, 270, 1000004 },
                    { 8000000091L, null, null, null, null, 91, 1000008 },
                    { 8000000092L, null, null, null, null, 92, 1000008 },
                    { 8000000093L, null, null, null, null, 93, 1000008 },
                    { 8000000094L, null, null, null, null, 94, 1000008 },
                    { 8000000095L, null, null, null, null, 95, 1000008 },
                    { 8000000096L, null, null, null, null, 96, 1000008 },
                    { 8000000097L, null, null, null, null, 97, 1000008 },
                    { 8000000098L, null, null, null, null, 98, 1000008 },
                    { 8000000099L, null, null, null, null, 99, 1000008 },
                    { 8000000100L, null, null, null, null, 100, 1000008 },
                    { 8000000121L, null, null, null, null, 121, 1000008 },
                    { 8000000122L, null, null, null, null, 122, 1000008 },
                    { 8000000123L, null, null, null, null, 123, 1000008 },
                    { 8000000124L, null, null, null, null, 124, 1000008 },
                    { 8000000125L, null, null, null, null, 125, 1000008 },
                    { 8000000126L, null, null, null, null, 126, 1000008 },
                    { 8000000127L, null, null, null, null, 127, 1000008 },
                    { 8000000128L, null, null, null, null, 128, 1000008 },
                    { 8000000129L, null, null, null, null, 129, 1000008 },
                    { 8000000130L, null, null, null, null, 130, 1000008 },
                    { 8000000141L, null, null, null, null, 141, 1000008 },
                    { 8000000142L, null, null, null, null, 142, 1000008 },
                    { 8000000143L, null, null, null, null, 143, 1000008 },
                    { 8000000144L, null, null, null, null, 144, 1000008 },
                    { 8000000145L, null, null, null, null, 145, 1000008 },
                    { 8000000146L, null, null, null, null, 146, 1000008 },
                    { 8000000147L, null, null, null, null, 147, 1000008 },
                    { 8000000148L, null, null, null, null, 148, 1000008 },
                    { 8000000149L, null, null, null, null, 149, 1000008 },
                    { 8000000150L, null, null, null, null, 150, 1000008 }
                });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 4,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 5,
                column: "App",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000101,
                column: "App",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000102,
                column: "App",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000104,
                column: "App",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000105,
                column: "App",
                value: 2);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Roles",
                columns: new[] { "RoleId", "App", "CreatedAt", "CreatedBy", "CustomerId", "Description", "DisplayName", "IsActive", "IsSystemRole", "ModifiedAt", "ModifiedBy", "SystemName" },
                values: new object[,]
                {
                    { 1000002, 2, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 1000004, 4, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 1000008, 8, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 1000103, 2, null, null, null, null, "Accountant", true, true, null, null, "Accountant" },
                    { 1000106, 2, null, null, null, null, "Viewer", true, true, null, null, "Viewer" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_App_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "App", "SystemName" },
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_App_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "CustomerId", "App", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerId_App",
                schema: "mst",
                table: "Licenses",
                columns: new[] { "CustomerId", "App" },
                unique: true);
        }
    }
}
