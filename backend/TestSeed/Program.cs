using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Inventory.Repository;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

class Program
{
    static async Task Main(string[] args)
    {
        var tenant = new MockTenantContext { OrgId = Guid.NewGuid(), CustomerId = Guid.NewGuid() };
        var clock = TimeProvider.System;
        var user = new MockCurrentUser();
        
        var audit = new AuditSaveChangesInterceptor(user, clock);
        var rls = new RlsConnectionInterceptor(tenant);
        
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=EP_Tenant_Template;Username=postgres;Password=123")
            .AddInterceptors(audit, rls)
            .Options;
            
        using var db = new InventoryDbContext(options, tenant);
        
        try
        {
            var types = Inventory.Repository.SeedData.UomSeed.BuildTypes(tenant.OrgId.Value);
            db.UomTypes.AddRange(types);
            await db.SaveChangesAsync();
            Console.WriteLine("Types saved!");
            
            var typeIds = await db.UomTypes.Where(t => t.UomTypeSystemName != null).ToDictionaryAsync(t => t.UomTypeSystemName!, t => t.UomTypeId);
            var units = Inventory.Repository.SeedData.UomSeed.BuildUnits(tenant.OrgId.Value, typeIds);
            db.UnitOfMeasures.AddRange(units);
            await db.SaveChangesAsync();
            Console.WriteLine("Units saved!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine("DbUpdateException: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }
}

class MockTenantContext : Shared.Kernel.Tenancy.ITenantContext
{
    public Guid? CustomerId { get; set; }
    public Guid? OrgId { get; set; }
    public (Guid CustomerId, Guid OrgId) Require() => (CustomerId ?? Guid.Empty, OrgId ?? Guid.Empty);
}

class MockCurrentUser : Shared.Kernel.Interfaces.ICurrentUser
{
    public Guid? UserId => null;
    public Guid? CustomerId => null;
    public Guid? OrgId => null;
    public int? RoleId => null;
}
