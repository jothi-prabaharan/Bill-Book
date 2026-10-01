import os
import glob

migration_files = glob.glob('backend/Api/Inventory/Inventory.Repository/Migrations/Tenant/*_AddRestaurantMenu.cs')
if not migration_files:
    print('No migration found')
    exit(1)
    
fpath = migration_files[0]
with open(fpath, 'r') as f:
    content = f.read()

tables = ['MenuCategories', 'MenuSchedules', 'MenuItems', 'ItemVariants', 'ModifierGroups', 'Modifiers', 'MenuItemPrices']
rls_up = ''
rls_down = ''
for t in tables:
    rls_up += f'            migrationBuilder.Sql("ALTER TABLE inv.\\\"{t}\\\" ENABLE ROW LEVEL SECURITY;");\n'
    rls_up += f'            migrationBuilder.Sql("CREATE POLICY \\\"TenantIsolation\\\" ON inv.\\\"{t}\\\" FOR ALL USING (\\\"OrgId\\\" = current_setting(\'app.current_org_id\')::uuid);");\n'
    rls_down += f'            migrationBuilder.Sql("DROP POLICY \\\"TenantIsolation\\\" ON inv.\\\"{t}\\\";");\n'

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {'
down_marker = 'protected override void Down(MigrationBuilder migrationBuilder)\n        {'

content = content.replace(up_marker, up_marker + '\n' + rls_up)
content = content.replace(down_marker, down_marker + '\n' + rls_down)

with open(fpath, 'w') as f:
    f.write(content)

print('Done')
