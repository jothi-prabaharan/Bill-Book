using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class SchoolRolesAndMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                keyValue: 101,
                column: "Apps",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "Apps",
                value: 3);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 11, 2, false, "students", null, null, 11, "graduation-cap", false, false, null, null, "sis", "Students", null, null, null, "Rail" },
                    { 12, 2, false, "fees", null, null, 12, "wallet", false, false, null, null, "fee", "Fees", null, null, null, "Rail" },
                    { 13, 2, false, "maintenance", null, null, 13, "wrench", false, false, null, null, "facility", "Maintenance", null, null, null, "Rail" }
                });

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

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "Apps", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[,]
                {
                    { 141, 8, "payroll.view", null, null, null, null, null, "payroll" },
                    { 142, 8, "payroll.create", null, null, null, null, null, "payroll" },
                    { 143, 8, "payroll.edit", null, null, null, null, null, "payroll" },
                    { 144, 8, "payroll.approve", null, null, null, null, null, "payroll" },
                    { 145, 8, "payroll.void", null, null, null, null, null, "payroll" },
                    { 146, 8, "payroll.delete", null, null, null, null, null, "payroll" },
                    { 147, 8, "payroll.print", null, null, null, null, null, "payroll" },
                    { 148, 8, "payroll.export", null, null, null, null, null, "payroll" },
                    { 149, 8, "payroll.import", null, null, null, null, null, "payroll" },
                    { 150, 8, "payroll.AllUserData", null, null, null, null, null, "payroll" },
                    { 151, 4, "leave.view", null, null, null, null, null, "leave" },
                    { 152, 4, "leave.create", null, null, null, null, null, "leave" },
                    { 153, 4, "leave.edit", null, null, null, null, null, "leave" },
                    { 154, 4, "leave.approve", null, null, null, null, null, "leave" },
                    { 155, 4, "leave.void", null, null, null, null, null, "leave" },
                    { 156, 4, "leave.delete", null, null, null, null, null, "leave" },
                    { 157, 4, "leave.print", null, null, null, null, null, "leave" },
                    { 158, 4, "leave.export", null, null, null, null, null, "leave" },
                    { 159, 4, "leave.import", null, null, null, null, null, "leave" },
                    { 160, 4, "leave.AllUserData", null, null, null, null, null, "leave" },
                    { 161, 6, "attendance.view", null, null, null, null, null, "attendance" },
                    { 162, 6, "attendance.create", null, null, null, null, null, "attendance" },
                    { 163, 6, "attendance.edit", null, null, null, null, null, "attendance" },
                    { 164, 6, "attendance.approve", null, null, null, null, null, "attendance" },
                    { 165, 6, "attendance.void", null, null, null, null, null, "attendance" },
                    { 166, 6, "attendance.delete", null, null, null, null, null, "attendance" },
                    { 167, 6, "attendance.print", null, null, null, null, null, "attendance" },
                    { 168, 6, "attendance.export", null, null, null, null, null, "attendance" },
                    { 169, 6, "attendance.import", null, null, null, null, null, "attendance" },
                    { 170, 6, "attendance.AllUserData", null, null, null, null, null, "attendance" },
                    { 171, 2, "sis.view", null, null, null, null, null, "sis" },
                    { 172, 2, "sis.create", null, null, null, null, null, "sis" },
                    { 173, 2, "sis.edit", null, null, null, null, null, "sis" },
                    { 174, 2, "sis.approve", null, null, null, null, null, "sis" },
                    { 175, 2, "sis.void", null, null, null, null, null, "sis" },
                    { 176, 2, "sis.delete", null, null, null, null, null, "sis" },
                    { 177, 2, "sis.print", null, null, null, null, null, "sis" },
                    { 178, 2, "sis.export", null, null, null, null, null, "sis" },
                    { 179, 2, "sis.import", null, null, null, null, null, "sis" },
                    { 180, 2, "sis.AllUserData", null, null, null, null, null, "sis" },
                    { 181, 2, "admission.view", null, null, null, null, null, "admission" },
                    { 182, 2, "admission.create", null, null, null, null, null, "admission" },
                    { 183, 2, "admission.edit", null, null, null, null, null, "admission" },
                    { 184, 2, "admission.approve", null, null, null, null, null, "admission" },
                    { 185, 2, "admission.void", null, null, null, null, null, "admission" },
                    { 186, 2, "admission.delete", null, null, null, null, null, "admission" },
                    { 187, 2, "admission.print", null, null, null, null, null, "admission" },
                    { 188, 2, "admission.export", null, null, null, null, null, "admission" },
                    { 189, 2, "admission.import", null, null, null, null, null, "admission" },
                    { 190, 2, "admission.AllUserData", null, null, null, null, null, "admission" },
                    { 191, 2, "fee.view", null, null, null, null, null, "fee" },
                    { 192, 2, "fee.create", null, null, null, null, null, "fee" },
                    { 193, 2, "fee.edit", null, null, null, null, null, "fee" },
                    { 194, 2, "fee.approve", null, null, null, null, null, "fee" },
                    { 195, 2, "fee.void", null, null, null, null, null, "fee" },
                    { 196, 2, "fee.delete", null, null, null, null, null, "fee" },
                    { 197, 2, "fee.print", null, null, null, null, null, "fee" },
                    { 198, 2, "fee.export", null, null, null, null, null, "fee" },
                    { 199, 2, "fee.import", null, null, null, null, null, "fee" },
                    { 200, 2, "fee.AllUserData", null, null, null, null, null, "fee" },
                    { 201, 2, "facility.view", null, null, null, null, null, "facility" },
                    { 202, 2, "facility.create", null, null, null, null, null, "facility" },
                    { 203, 2, "facility.edit", null, null, null, null, null, "facility" },
                    { 204, 2, "facility.approve", null, null, null, null, null, "facility" },
                    { 205, 2, "facility.void", null, null, null, null, null, "facility" },
                    { 206, 2, "facility.delete", null, null, null, null, null, "facility" },
                    { 207, 2, "facility.print", null, null, null, null, null, "facility" },
                    { 208, 2, "facility.export", null, null, null, null, null, "facility" },
                    { 209, 2, "facility.import", null, null, null, null, null, "facility" },
                    { 210, 2, "facility.AllUserData", null, null, null, null, null, "facility" },
                    { 211, 2, "workorder.view", null, null, null, null, null, "workorder" },
                    { 212, 2, "workorder.create", null, null, null, null, null, "workorder" },
                    { 213, 2, "workorder.edit", null, null, null, null, null, "workorder" },
                    { 214, 2, "workorder.approve", null, null, null, null, null, "workorder" },
                    { 215, 2, "workorder.void", null, null, null, null, null, "workorder" },
                    { 216, 2, "workorder.delete", null, null, null, null, null, "workorder" },
                    { 217, 2, "workorder.print", null, null, null, null, null, "workorder" },
                    { 218, 2, "workorder.export", null, null, null, null, null, "workorder" },
                    { 219, 2, "workorder.import", null, null, null, null, null, "workorder" },
                    { 220, 2, "workorder.AllUserData", null, null, null, null, null, "workorder" },
                    { 221, 2, "preventive.view", null, null, null, null, null, "preventive" },
                    { 222, 2, "preventive.create", null, null, null, null, null, "preventive" },
                    { 223, 2, "preventive.edit", null, null, null, null, null, "preventive" },
                    { 224, 2, "preventive.approve", null, null, null, null, null, "preventive" },
                    { 225, 2, "preventive.void", null, null, null, null, null, "preventive" },
                    { 226, 2, "preventive.delete", null, null, null, null, null, "preventive" },
                    { 227, 2, "preventive.print", null, null, null, null, null, "preventive" },
                    { 228, 2, "preventive.export", null, null, null, null, null, "preventive" },
                    { 229, 2, "preventive.import", null, null, null, null, null, "preventive" },
                    { 230, 2, "preventive.AllUserData", null, null, null, null, null, "preventive" },
                    { 231, 2, "amc.view", null, null, null, null, null, "amc" },
                    { 232, 2, "amc.create", null, null, null, null, null, "amc" },
                    { 233, 2, "amc.edit", null, null, null, null, null, "amc" },
                    { 234, 2, "amc.approve", null, null, null, null, null, "amc" },
                    { 235, 2, "amc.void", null, null, null, null, null, "amc" },
                    { 236, 2, "amc.delete", null, null, null, null, null, "amc" },
                    { 237, 2, "amc.print", null, null, null, null, null, "amc" },
                    { 238, 2, "amc.export", null, null, null, null, null, "amc" },
                    { 239, 2, "amc.import", null, null, null, null, null, "amc" },
                    { 240, 2, "amc.AllUserData", null, null, null, null, null, "amc" },
                    { 10001, 2, "attendance.unlock", null, null, null, null, null, "attendance" },
                    { 10002, 2, "workorder.close", null, null, null, null, null, "workorder" }
                });

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
                    { 2010000011L, null, null, null, null, 11, 1000101 },
                    { 2010000012L, null, null, null, null, 12, 1000101 },
                    { 2010000013L, null, null, null, null, 13, 1000101 },
                    { 2010000014L, null, null, null, null, 14, 1000101 },
                    { 2010000015L, null, null, null, null, 15, 1000101 },
                    { 2010000016L, null, null, null, null, 16, 1000101 },
                    { 2010000017L, null, null, null, null, 17, 1000101 },
                    { 2010000018L, null, null, null, null, 18, 1000101 },
                    { 2010000019L, null, null, null, null, 19, 1000101 },
                    { 2010000020L, null, null, null, null, 20, 1000101 },
                    { 2010000121L, null, null, null, null, 121, 1000101 },
                    { 2010000161L, null, null, null, null, 161, 1000101 },
                    { 2010000162L, null, null, null, null, 162, 1000101 },
                    { 2010000163L, null, null, null, null, 163, 1000101 },
                    { 2010000164L, null, null, null, null, 164, 1000101 },
                    { 2010000165L, null, null, null, null, 165, 1000101 },
                    { 2010000166L, null, null, null, null, 166, 1000101 },
                    { 2010000167L, null, null, null, null, 167, 1000101 },
                    { 2010000168L, null, null, null, null, 168, 1000101 },
                    { 2010000169L, null, null, null, null, 169, 1000101 },
                    { 2010000170L, null, null, null, null, 170, 1000101 },
                    { 2010000171L, null, null, null, null, 171, 1000101 },
                    { 2010000172L, null, null, null, null, 172, 1000101 },
                    { 2010000173L, null, null, null, null, 173, 1000101 },
                    { 2010000174L, null, null, null, null, 174, 1000101 },
                    { 2010000175L, null, null, null, null, 175, 1000101 },
                    { 2010000176L, null, null, null, null, 176, 1000101 },
                    { 2010000177L, null, null, null, null, 177, 1000101 },
                    { 2010000178L, null, null, null, null, 178, 1000101 },
                    { 2010000179L, null, null, null, null, 179, 1000101 },
                    { 2010000180L, null, null, null, null, 180, 1000101 },
                    { 2010000181L, null, null, null, null, 181, 1000101 },
                    { 2010000182L, null, null, null, null, 182, 1000101 },
                    { 2010000183L, null, null, null, null, 183, 1000101 },
                    { 2010000184L, null, null, null, null, 184, 1000101 },
                    { 2010000185L, null, null, null, null, 185, 1000101 },
                    { 2010000186L, null, null, null, null, 186, 1000101 },
                    { 2010000187L, null, null, null, null, 187, 1000101 },
                    { 2010000188L, null, null, null, null, 188, 1000101 },
                    { 2010000189L, null, null, null, null, 189, 1000101 },
                    { 2010000190L, null, null, null, null, 190, 1000101 },
                    { 2010000191L, null, null, null, null, 191, 1000101 },
                    { 2010000192L, null, null, null, null, 192, 1000101 },
                    { 2010000193L, null, null, null, null, 193, 1000101 },
                    { 2010000194L, null, null, null, null, 194, 1000101 },
                    { 2010000195L, null, null, null, null, 195, 1000101 },
                    { 2010000196L, null, null, null, null, 196, 1000101 },
                    { 2010000197L, null, null, null, null, 197, 1000101 },
                    { 2010000198L, null, null, null, null, 198, 1000101 },
                    { 2010000199L, null, null, null, null, 199, 1000101 },
                    { 2010000200L, null, null, null, null, 200, 1000101 },
                    { 2010000201L, null, null, null, null, 201, 1000101 },
                    { 2010000202L, null, null, null, null, 202, 1000101 },
                    { 2010000203L, null, null, null, null, 203, 1000101 },
                    { 2010000204L, null, null, null, null, 204, 1000101 },
                    { 2010000205L, null, null, null, null, 205, 1000101 },
                    { 2010000206L, null, null, null, null, 206, 1000101 },
                    { 2010000207L, null, null, null, null, 207, 1000101 },
                    { 2010000208L, null, null, null, null, 208, 1000101 },
                    { 2010000209L, null, null, null, null, 209, 1000101 },
                    { 2010000210L, null, null, null, null, 210, 1000101 },
                    { 2010000211L, null, null, null, null, 211, 1000101 },
                    { 2010000212L, null, null, null, null, 212, 1000101 },
                    { 2010000213L, null, null, null, null, 213, 1000101 },
                    { 2010000214L, null, null, null, null, 214, 1000101 },
                    { 2010000215L, null, null, null, null, 215, 1000101 },
                    { 2010000216L, null, null, null, null, 216, 1000101 },
                    { 2010000217L, null, null, null, null, 217, 1000101 },
                    { 2010000218L, null, null, null, null, 218, 1000101 },
                    { 2010000219L, null, null, null, null, 219, 1000101 },
                    { 2010000220L, null, null, null, null, 220, 1000101 },
                    { 2010000221L, null, null, null, null, 221, 1000101 },
                    { 2010000222L, null, null, null, null, 222, 1000101 },
                    { 2010000223L, null, null, null, null, 223, 1000101 },
                    { 2010000224L, null, null, null, null, 224, 1000101 },
                    { 2010000225L, null, null, null, null, 225, 1000101 },
                    { 2010000226L, null, null, null, null, 226, 1000101 },
                    { 2010000227L, null, null, null, null, 227, 1000101 },
                    { 2010000228L, null, null, null, null, 228, 1000101 },
                    { 2010000229L, null, null, null, null, 229, 1000101 },
                    { 2010000230L, null, null, null, null, 230, 1000101 },
                    { 2010000231L, null, null, null, null, 231, 1000101 },
                    { 2010000232L, null, null, null, null, 232, 1000101 },
                    { 2010000233L, null, null, null, null, 233, 1000101 },
                    { 2010000234L, null, null, null, null, 234, 1000101 },
                    { 2010000235L, null, null, null, null, 235, 1000101 },
                    { 2010000236L, null, null, null, null, 236, 1000101 },
                    { 2010000237L, null, null, null, null, 237, 1000101 },
                    { 2010000238L, null, null, null, null, 238, 1000101 },
                    { 2010000239L, null, null, null, null, 239, 1000101 },
                    { 2010000240L, null, null, null, null, 240, 1000101 },
                    { 2010010001L, null, null, null, null, 10001, 1000101 },
                    { 2010010002L, null, null, null, null, 10002, 1000101 },
                    { 2020000011L, null, null, null, null, 11, 1000102 },
                    { 2020000012L, null, null, null, null, 12, 1000102 },
                    { 2020000013L, null, null, null, null, 13, 1000102 },
                    { 2020000014L, null, null, null, null, 14, 1000102 },
                    { 2020000015L, null, null, null, null, 15, 1000102 },
                    { 2020000016L, null, null, null, null, 16, 1000102 },
                    { 2020000017L, null, null, null, null, 17, 1000102 },
                    { 2020000018L, null, null, null, null, 18, 1000102 },
                    { 2020000019L, null, null, null, null, 19, 1000102 },
                    { 2020000020L, null, null, null, null, 20, 1000102 },
                    { 2020000121L, null, null, null, null, 121, 1000102 },
                    { 2020000161L, null, null, null, null, 161, 1000102 },
                    { 2020000162L, null, null, null, null, 162, 1000102 },
                    { 2020000163L, null, null, null, null, 163, 1000102 },
                    { 2020000164L, null, null, null, null, 164, 1000102 },
                    { 2020000165L, null, null, null, null, 165, 1000102 },
                    { 2020000166L, null, null, null, null, 166, 1000102 },
                    { 2020000167L, null, null, null, null, 167, 1000102 },
                    { 2020000168L, null, null, null, null, 168, 1000102 },
                    { 2020000169L, null, null, null, null, 169, 1000102 },
                    { 2020000170L, null, null, null, null, 170, 1000102 },
                    { 2020000171L, null, null, null, null, 171, 1000102 },
                    { 2020000172L, null, null, null, null, 172, 1000102 },
                    { 2020000173L, null, null, null, null, 173, 1000102 },
                    { 2020000174L, null, null, null, null, 174, 1000102 },
                    { 2020000175L, null, null, null, null, 175, 1000102 },
                    { 2020000176L, null, null, null, null, 176, 1000102 },
                    { 2020000177L, null, null, null, null, 177, 1000102 },
                    { 2020000178L, null, null, null, null, 178, 1000102 },
                    { 2020000179L, null, null, null, null, 179, 1000102 },
                    { 2020000180L, null, null, null, null, 180, 1000102 },
                    { 2020000181L, null, null, null, null, 181, 1000102 },
                    { 2020000182L, null, null, null, null, 182, 1000102 },
                    { 2020000183L, null, null, null, null, 183, 1000102 },
                    { 2020000184L, null, null, null, null, 184, 1000102 },
                    { 2020000185L, null, null, null, null, 185, 1000102 },
                    { 2020000186L, null, null, null, null, 186, 1000102 },
                    { 2020000187L, null, null, null, null, 187, 1000102 },
                    { 2020000188L, null, null, null, null, 188, 1000102 },
                    { 2020000189L, null, null, null, null, 189, 1000102 },
                    { 2020000190L, null, null, null, null, 190, 1000102 },
                    { 2020000191L, null, null, null, null, 191, 1000102 },
                    { 2020000192L, null, null, null, null, 192, 1000102 },
                    { 2020000197L, null, null, null, null, 197, 1000102 },
                    { 2020010001L, null, null, null, null, 10001, 1000102 },
                    { 2030000011L, null, null, null, null, 11, 1000103 },
                    { 2030000171L, null, null, null, null, 171, 1000103 },
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

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Roles",
                columns: new[] { "RoleId", "App", "CreatedAt", "CreatedBy", "CustomerId", "Description", "DisplayName", "IsActive", "IsSystemRole", "ModifiedAt", "ModifiedBy", "SystemName" },
                values: new object[,]
                {
                    { 1000101, 2, null, null, null, null, "Principal", true, true, null, null, "Principal" },
                    { 1000102, 2, null, null, null, null, "Office Admin", true, true, null, null, "Office Admin" },
                    { 1000103, 2, null, null, null, null, "Accountant", true, true, null, null, "Accountant" },
                    { 1000104, 2, null, null, null, null, "Teacher", true, true, null, null, "Teacher" },
                    { 1000105, 2, null, null, null, null, "Maintenance", true, true, null, null, "Maintenance" },
                    { 1000106, 2, null, null, null, null, "Viewer", true, true, null, null, "Viewer" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 119, 2, false, "students-g1", null, null, 1, null, false, false, null, null, null, null, 11, null, null, "Group" },
                    { 120, 2, false, "fees-g1", null, null, 1, null, false, false, null, null, null, null, 12, null, null, "Group" },
                    { 121, 2, false, "maintenance-g1", null, null, 1, null, false, false, null, null, null, null, 13, null, null, "Group" },
                    { 1111, 2, true, "stu", null, null, 1, "users-round", false, false, null, null, "sis", "Students", 119, "/sis/students", "Student", "Item" },
                    { 1112, 2, false, "acs", null, null, 2, "layers", false, false, null, null, "sis", "Academic setup", 119, "/sis/setup", null, "Item" },
                    { 1113, 2, true, "exm", null, null, 3, "clipboard-check", false, false, null, null, "sis", "Exams and marks", 119, "/sis/exams", "Exam", "Item" },
                    { 1114, 2, true, "enq", null, null, 4, "message-circle", false, false, null, null, "admission", "Enquiries", 119, "/admission/enquiries", "Enquiry", "Item" },
                    { 1115, 2, true, "apl", null, null, 5, "file-text", false, false, null, null, "admission", "Applications", 119, "/admission/applications", "Application", "Item" },
                    { 1116, 2, false, "sat", null, null, 6, "calendar-check", false, false, null, null, "attendance", "Attendance register", 119, "/attendance/register", null, "Item" },
                    { 1117, 2, false, "fst", null, null, 1, "settings", false, false, null, null, "fee", "Fee setup", 120, "/fee/setup", null, "Item" },
                    { 1118, 2, true, "fdm", null, null, 2, "file-text", false, false, null, null, "fee", "Fee demands", 120, "/fee/demands", "Fee demand", "Item" },
                    { 1119, 2, true, "frc", null, null, 3, "receipt", false, false, null, null, "fee", "Fee receipts", 120, "/fee/receipts", "Fee receipt", "Item" },
                    { 1120, 2, true, "spc", null, null, 1, "building", false, false, null, null, "facility", "Buildings and spaces", 121, "/facility/spaces", "Building", "Item" },
                    { 1121, 2, true, "fas", null, null, 2, "package", false, false, null, null, "facility", "Facility assets", 121, "/facility/assets", "Asset", "Item" },
                    { 1122, 2, true, "wko", null, null, 3, "wrench", false, false, null, null, "workorder", "Work orders", 121, "/work-orders", "Work order", "Item" },
                    { 1123, 2, true, "ppm", null, null, 4, "calendar-clock", false, false, null, null, "preventive", "Preventive plans", 121, "/preventive/plans", "Plan", "Item" },
                    { 1124, 2, true, "amc", null, null, 5, "file-signature", false, false, null, null, "amc", "AMC contracts", 121, "/amc/contracts", "AMC contract", "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 395, "view", null, null, 1111, null, null, "sis", "sis.view" },
                    { 396, "create", null, null, 1111, null, null, "sis", "sis.create" },
                    { 397, "edit", null, null, 1111, null, null, "sis", "sis.edit" },
                    { 398, "view", null, null, 1112, null, null, "sis", "sis.view" },
                    { 399, "create", null, null, 1112, null, null, "sis", "sis.create" },
                    { 400, "edit", null, null, 1112, null, null, "sis", "sis.edit" },
                    { 401, "view", null, null, 1113, null, null, "sis", "sis.view" },
                    { 402, "create", null, null, 1113, null, null, "sis", "sis.create" },
                    { 403, "edit", null, null, 1113, null, null, "sis", "sis.edit" },
                    { 404, "view", null, null, 1114, null, null, "admission", "admission.view" },
                    { 405, "create", null, null, 1114, null, null, "admission", "admission.create" },
                    { 406, "edit", null, null, 1114, null, null, "admission", "admission.edit" },
                    { 407, "view", null, null, 1115, null, null, "admission", "admission.view" },
                    { 408, "create", null, null, 1115, null, null, "admission", "admission.create" },
                    { 409, "edit", null, null, 1115, null, null, "admission", "admission.edit" },
                    { 410, "view", null, null, 1116, null, null, "attendance", "attendance.view" },
                    { 411, "create", null, null, 1116, null, null, "attendance", "attendance.create" },
                    { 412, "edit", null, null, 1116, null, null, "attendance", "attendance.edit" },
                    { 413, "view", null, null, 1117, null, null, "fee", "fee.view" },
                    { 414, "create", null, null, 1117, null, null, "fee", "fee.create" },
                    { 415, "edit", null, null, 1117, null, null, "fee", "fee.edit" },
                    { 416, "view", null, null, 1118, null, null, "fee", "fee.view" },
                    { 417, "create", null, null, 1118, null, null, "fee", "fee.create" },
                    { 418, "edit", null, null, 1118, null, null, "fee", "fee.edit" },
                    { 419, "void", null, null, 1118, null, null, "fee", "fee.void" },
                    { 420, "print", null, null, 1118, null, null, "fee", "fee.print" },
                    { 421, "view", null, null, 1119, null, null, "fee", "fee.view" },
                    { 422, "create", null, null, 1119, null, null, "fee", "fee.create" },
                    { 423, "edit", null, null, 1119, null, null, "fee", "fee.edit" },
                    { 424, "void", null, null, 1119, null, null, "fee", "fee.void" },
                    { 425, "print", null, null, 1119, null, null, "fee", "fee.print" },
                    { 426, "view", null, null, 1120, null, null, "facility", "facility.view" },
                    { 427, "create", null, null, 1120, null, null, "facility", "facility.create" },
                    { 428, "edit", null, null, 1120, null, null, "facility", "facility.edit" },
                    { 429, "view", null, null, 1121, null, null, "facility", "facility.view" },
                    { 430, "create", null, null, 1121, null, null, "facility", "facility.create" },
                    { 431, "edit", null, null, 1121, null, null, "facility", "facility.edit" },
                    { 432, "view", null, null, 1122, null, null, "workorder", "workorder.view" },
                    { 433, "create", null, null, 1122, null, null, "workorder", "workorder.create" },
                    { 434, "edit", null, null, 1122, null, null, "workorder", "workorder.edit" },
                    { 435, "delete", null, null, 1122, null, null, "workorder", "workorder.delete" },
                    { 436, "view", null, null, 1123, null, null, "preventive", "preventive.view" },
                    { 437, "create", null, null, 1123, null, null, "preventive", "preventive.create" },
                    { 438, "edit", null, null, 1123, null, null, "preventive", "preventive.edit" },
                    { 439, "view", null, null, 1124, null, null, "amc", "amc.view" },
                    { 440, "create", null, null, 1124, null, null, "amc", "amc.create" },
                    { 441, "edit", null, null, 1124, null, null, "amc", "amc.edit" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 395);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 396);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 397);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 398);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 399);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 400);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 401);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 402);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 403);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 404);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 405);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 406);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 407);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 408);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 409);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 410);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 411);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 412);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 413);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 414);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 415);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 416);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 417);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 418);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 419);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 420);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 421);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 422);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 423);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 424);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 425);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 426);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 427);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 428);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 429);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 430);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 431);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 432);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 433);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 434);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 435);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 436);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 437);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 438);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 439);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 440);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 441);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 141);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 142);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 143);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 144);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 145);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 146);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 147);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 148);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 149);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 150);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 151);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 152);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 153);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 154);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 155);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 156);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 157);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 158);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 159);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 160);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 161);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 162);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 163);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 164);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 165);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 166);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 167);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 168);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 169);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 170);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 171);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 172);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 173);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 174);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 175);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 176);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 177);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 178);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 179);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 180);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 181);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 182);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 183);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 184);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 185);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 186);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 187);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 188);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 189);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 190);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 191);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 192);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 193);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 194);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 195);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 196);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 197);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 198);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 199);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 200);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 201);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 202);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 203);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 204);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 205);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 206);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 207);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 208);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 209);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 210);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 211);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 212);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 213);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 214);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 215);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 216);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 217);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 218);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 219);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 220);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 221);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 222);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 223);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 224);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 225);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 226);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 227);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 228);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 229);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 230);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 231);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 232);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 233);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 234);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 235);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 236);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 237);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 238);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 239);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 240);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10001);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10002);

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
                keyValue: 2010000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000012L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000013L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000014L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000015L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000016L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000017L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000018L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000019L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000020L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000172L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000174L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000175L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000176L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000177L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000178L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000179L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000180L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000182L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000183L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000184L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000185L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000186L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000187L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000188L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000189L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000190L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000193L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000194L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000195L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000196L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000198L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000199L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000200L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000201L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000202L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000203L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000204L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000205L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000206L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000207L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000208L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000209L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000210L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000211L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000212L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000213L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000214L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000215L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000216L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000217L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000218L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000219L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000220L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000221L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000222L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000223L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000224L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000225L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000226L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000227L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000228L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000229L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000230L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000231L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000232L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000233L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000234L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000235L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000236L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000237L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000238L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000239L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010000240L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010010001L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2010010002L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000012L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000013L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000014L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000015L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000016L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000017L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000018L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000019L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000020L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000161L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000162L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000163L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000164L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000165L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000166L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000167L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000168L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000169L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000170L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000171L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000172L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000173L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000174L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000175L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000176L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000177L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000178L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000179L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000180L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000181L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000182L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000183L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000184L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000185L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000186L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000187L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000188L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000189L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000190L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000191L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000192L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020000197L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2020010001L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000011L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2030000171L);

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
                keyValue: 1000101);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000102);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000103);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000104);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000105);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000106);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1111);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1112);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1113);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1114);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1115);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1116);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1117);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1118);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1119);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1120);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1121);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1122);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1123);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1124);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 119);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 120);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 121);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 13);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 2,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 101,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1001,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 11,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 12,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 13,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 14,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 15,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 16,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 17,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 18,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 19,
                column: "Apps",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 20,
                column: "Apps",
                value: 1);
        }
    }
}
