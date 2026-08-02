using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Repository.Migrations
{
    /// <summary>
    /// Three read-only grants the built-in roles turned out to need, once
    /// permissions were enforced for the first time.
    ///
    /// Identity has minted a permission claim for every seeded permission since
    /// the beginning and no service ever read one, so the role matrix had never
    /// met a real screen. Enforcing it exposed two roles that could not do their
    /// own job:
    ///
    /// <list type="bullet">
    /// <item><b>Sales could not open the item master or stock.</b> Selling what
    /// you cannot look up is not a role, and this is a retail product.</item>
    /// <item><b>Accountant could not open contacts or inventory.</b> Receivables
    /// are held per contact and stock has to be valued, so both are things an
    /// accountant reads in order to do accounting.</item>
    /// </list>
    ///
    /// All three are <c>.view</c>. Nothing here lets a role write outside its own
    /// modules — a salesperson still cannot edit an item, an accountant still
    /// cannot edit a contact. Read access is what the job needs; write access
    /// would be a separate decision.
    ///
    /// The grants are appended after the existing ones rather than slotted in
    /// beside their role, so every <c>RolePermissionId</c> already issued keeps
    /// its value. Inserting them in role order would have renumbered the Viewer
    /// grants and turned a three-row addition into a rewrite of thirty.
    /// </summary>
    public partial class AddCrossModuleViewGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PermissionId 11 = contacts.view, 31 = inventory.view.
            // RoleId 3 = Accountant, 4 = Sales.
            migrationBuilder.InsertData(
                schema: "idn",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 302L, null, null, null, null, 11, 3 },
                    { 303L, null, null, null, null, 31, 3 },
                    { 304L, null, null, null, null, 31, 4 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "idn",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValues: new object[] { 302L, 303L, 304L });
        }
    }
}
