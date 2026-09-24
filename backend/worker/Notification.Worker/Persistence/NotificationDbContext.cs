using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Kernel.Entities;

namespace Notification.Worker.Persistence;

/// <summary>
/// The worker's own schema, <c>ntf</c> (TK-19). It holds no customer's data —
/// only which message ids have been sent — so it is not a
/// <c>TenantDbContext</c>: there is no tenant to filter by, and the table is the
/// schema's named exemption from row-level security (see the migration).
/// </summary>
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ntf");

        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.HasKey(e => e.MessageId);
            b.HasIndex(e => e.ClaimedAt);

            b.Property(e => e.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>For <c>dotnet ef</c> only: the worker's host needs a broker and a database to start.</summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=IN000001;Username=postgres;Password=123",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ntf"))
            .Options);
}
