using Microsoft.EntityFrameworkCore;
using SavingsScheme.Entity.TableEntities;
using Shared.Kernel.Tenancy;

namespace SavingsScheme.Repository;

public class SavingsSchemeDbContext : TenantDbContext
{
    public SavingsSchemeDbContext(DbContextOptions<SavingsSchemeDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<SchemeDefinition> SchemeDefinitions => Set<SchemeDefinition>();
    public DbSet<SchemeEnrolment> SchemeEnrolments => Set<SchemeEnrolment>();
    public DbSet<SchemeInstalment> SchemeInstalments => Set<SchemeInstalment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.HasDefaultSchema("sav");
    }
}
