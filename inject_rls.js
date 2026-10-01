const fs = require('fs');
const glob = require('glob');

const files = glob.sync('backend/Api/Sales/Sales.Repository/Migrations/Tenant/*_AddRestaurantOrder.cs');
if (!files.length) {
    console.error('No files found');
    process.exit(1);
}

const f = files[0];
let content = fs.readFileSync(f, 'utf8');

const upMarker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {';
const downMarker = 'protected override void Down(MigrationBuilder migrationBuilder)\n        {';

const rlsUp = 
            migrationBuilder.Sql("ALTER TABLE sal.\\"RestaurantOrders\\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrders\\" FOR ALL USING (\\"OrgId\\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE sal.\\"RestaurantOrderDetails\\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrderDetails\\" FOR ALL USING (\\"OrgId\\" = current_setting('app.current_org_id')::uuid);");
;

const rlsDown = 
            migrationBuilder.Sql("DROP POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrders\\";");
            migrationBuilder.Sql("DROP POLICY \\"TenantIsolation\\" ON sal.\\"RestaurantOrderDetails\\";");
;

content = content.replace(upMarker, upMarker + rlsUp);
content = content.replace(downMarker, downMarker + rlsDown);

fs.writeFileSync(f, content, 'utf8');
