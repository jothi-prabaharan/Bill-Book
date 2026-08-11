using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Entity.TableEntities;
using Shared.Kernel.Entities;

namespace Platform.Repository;

public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<CustomerDatabase> CustomerDatabases => Set<CustomerDatabase>();

    public DbSet<License> Licenses => Set<License>();

    public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

    public DbSet<OrgCurrency> OrgCurrencies => Set<OrgCurrency>();

    public DbSet<Configuration> Configurations => Set<Configuration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("plt");

        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(e => e.CustomerId);
            b.HasIndex(e => e.CustomerCode).IsUnique();
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Organization>(b =>
        {
            b.HasKey(e => e.OrgId);
            b.HasIndex(e => new { e.CustomerId, e.Name }).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.OrgCode }).IsUnique();
            b.HasIndex(e => e.CustomerId);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DiscountLevel).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.AllowFreeTextLines).HasDefaultValue(true);
            b.Property(e => e.DiscountBeforeTax).HasDefaultValue(true);

            // Read on every login, alongside the customer's licence. Filtered,
            // because most branches have no date of their own and an index over
            // mostly-null rows is bigger than the answer it gives.
            b.HasIndex(e => e.ExpiryDate)
                .HasFilter("\"ExpiryDate\" IS NOT NULL")
                .HasDatabaseName("IX_Organizations_ExpiryDate");
        });

        modelBuilder.Entity<CustomerDatabase>(b =>
        {
            b.HasKey(e => e.CustomerId);
            b.HasIndex(e => e.DatabaseName).IsUnique();
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<License>(b =>
        {
            b.HasKey(e => e.LicenseId);
            b.HasIndex(e => e.CustomerId).IsUnique();
            b.Property(e => e.LicenseType).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<SmtpSettings>(b =>
        {
            b.HasKey(e => e.SmtpSettingsId);
            // One row per customer; the system default row has a null CustomerId.
            b.HasIndex(e => e.CustomerId).IsUnique().HasFilter("\"CustomerId\" IS NOT NULL");
        });

        modelBuilder.Entity<OrgCurrency>(b =>
        {
            b.HasKey(e => e.OrgCurrencyId);
            b.HasIndex(e => new { e.OrgId, e.CurrencyId }).IsUnique();
            b.HasIndex(e => e.OrgId).IsUnique().HasFilter("\"IsBaseCurrency\" = true");
        });

        modelBuilder.Entity<Configuration>(b =>
        {
            b.HasKey(e => e.ConfigId);
            b.HasIndex(e => new { e.OrgId, e.Code }).IsUnique();
            b.HasIndex(e => e.Code).IsUnique().HasFilter("\"OrgId\" IS NULL");
            b.Property(e => e.DataType).HasConversion<string>().HasMaxLength(10);
        });

        MapXminConcurrency(modelBuilder);
        SeedConfigurations(modelBuilder);
    }

    private static void MapXminConcurrency(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(AuditableEntity.Version))
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }

    private static void SeedConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Configuration>().HasData(
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                OrgId = null,
                Code = "unitPrice.decimals",
                Name = "Unit Price Decimals",
                Description = "Decimal places for unit price inputs",
                DataType = ConfigDataType.Number,
                Value = "2",
                Category = "Formatting",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
                OrgId = null,
                Code = "quantity.decimals",
                Name = "Quantity Decimals",
                Description = "Decimal places for quantity inputs",
                DataType = ConfigDataType.Number,
                Value = "2",
                Category = "Formatting",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
                OrgId = null,
                Code = "sales.dueDays",
                Name = "Sales Due Days",
                Description = "Default payment terms on invoices",
                DataType = ConfigDataType.Number,
                Value = "30",
                Category = "Documents",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000004"),
                OrgId = null,
                Code = "purchase.dueDays",
                Name = "Purchase Due Days",
                Description = "Default payment terms on bills",
                DataType = ConfigDataType.Number,
                Value = "30",
                Category = "Documents",
                IsSystem = true,
            });
    }
}
