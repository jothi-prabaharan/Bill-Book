using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Master.Repository;

/// <summary>
/// Design-time factory for <see cref="AdminDbContext"/> migrations.
/// </summary>
public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdminDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=billbook_master;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "mst"));

        return new AdminDbContext(optionsBuilder.Options);
    }
}
