import glob
import os

files = glob.glob('backend/Api/Sales/Sales.Repository/Migrations/Tenant/*_AddRestaurantOrder.cs')
if not files:
    print('No files found')
    exit(1)

f = files[0]
with open(f, 'r') as file:
    content = file.read()

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {'
down_marker = 'protected override void Down(MigrationBuilder migrationBuilder)\n        {'

rls_up = '''
            migrationBuilder.Sql("ALTER TABLE sal.\\"RestaurantOrders\\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrders\\" FOR ALL USING (\\"OrgId\\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE sal.\\"RestaurantOrderDetails\\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrderDetails\\" FOR ALL USING (\\"OrgId\\" = current_setting('app.current_org_id')::uuid);");
'''

rls_down = '''
            migrationBuilder.Sql("DROP POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrders\\";");
            migrationBuilder.Sql("DROP POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrderDetails\\";");
'''

content = content.replace(up_marker, up_marker + rls_up)
content = content.replace(down_marker, down_marker + rls_down)

with open(f, 'w') as file:
    file.write(content)
