using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sales.Repository;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

class Program
{
    static async Task Main(string[] args)
    {
        var tenant = new MockTenantContext { OrgId = Guid.Parse("bb6e5001-6042-48bc-961b-dcbd3811330a"), CustomerId = Guid.NewGuid() };
        var clock = TimeProvider.System;
        var user = new MockCurrentUser();
        
        var audit = new AuditSaveChangesInterceptor(user, clock);
        var rls = new RlsConnectionInterceptor(tenant);
        
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=IN0000000001;Username=postgres;Password=123")
            .AddInterceptors(audit, rls)
            .Options;
            
        using var db = new SalesDbContext(options, tenant);
        
        try
        {
            var seed = new Sales.Repository.SeedData.SalesSeed(db);
            await seed.SeedForOrganizationAsync(tenant.OrgId.Value, default);
            Console.WriteLine("Sales saved!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine("DbUpdateException in Sales: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception in Sales: " + ex.Message);
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
