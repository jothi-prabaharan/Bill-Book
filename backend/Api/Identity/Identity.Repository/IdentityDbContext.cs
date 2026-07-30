using Identity.Entity.Enums;
using Identity.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Entities;

namespace Identity.Repository;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserOrganizationRole> UserOrganizationRoles => Set<UserOrganizationRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("idn");

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(e => e.UserId);
            b.HasIndex(e => e.Email).IsUnique();
            b.Property(e => e.ThemePreference).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.HasKey(e => e.RoleId);
            b.HasIndex(e => new { e.CustomerId, e.SystemName }).IsUnique();
            // Postgres treats nulls as distinct, so system-role names need a partial guard.
            b.HasIndex(e => e.SystemName)
                .IsUnique()
                .HasFilter("\"CustomerId\" IS NULL");
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.HasKey(e => e.PermissionId);
            b.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.HasKey(e => e.RolePermissionId);
            b.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
        });

        modelBuilder.Entity<UserOrganizationRole>(b =>
        {
            b.HasKey(e => e.UserOrganizationRoleId);
            b.HasIndex(e => new { e.UserId, e.OrgId, e.RoleId }).IsUnique();
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.OrgId);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(e => e.RefreshTokenId);
            b.HasIndex(e => e.TokenHash);
            b.HasIndex(e => new { e.UserId, e.ExpiresAt });
        });

        modelBuilder.Entity<LoginHistory>(b =>
        {
            b.HasKey(e => e.LoginHistoryId);
            b.HasIndex(e => new { e.UserId, e.LoginAt });
        });

        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.HasKey(e => e.PasswordResetTokenId);
            b.HasIndex(e => e.TokenHash);
        });

        modelBuilder.Entity<OtpVerification>(b =>
        {
            b.HasKey(e => e.OtpVerificationId);
            b.HasIndex(e => new { e.UserId, e.Purpose, e.ExpiresAt });
            b.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Channel).HasConversion<string>().HasMaxLength(10);
        });

        MapXminConcurrency(modelBuilder);
        SeedRolesAndPermissions(modelBuilder);
    }

    /// <summary>Expose the Postgres xmin system column as the concurrency token on every audited entity.</summary>
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

    private static void SeedRolesAndPermissions(ModelBuilder modelBuilder)
    {
        string[] systemRoles = { "Owner", "Administrator", "Accountant", "Sales", "Viewer" };
        var roles = new List<Role>();
        for (int i = 0; i < systemRoles.Length; i++)
        {
            roles.Add(new Role
            {
                RoleId = i + 1,
                CustomerId = null,
                SystemName = systemRoles[i],
                DisplayName = systemRoles[i],
                IsSystemRole = true,
                IsActive = true,
            });
        }

        modelBuilder.Entity<Role>().HasData(roles);

        string[] modules =
        {
            "dashboard", "contacts", "crm", "inventory", "sales", "purchase",
            "accounting", "banking", "reports", "settings", "support", "platform",
        };
        string[] actions =
        {
            "view", "create", "edit", "approve", "void",
            "delete", "print", "export", "import", "AllUserData",
        };

        var permissions = new List<Permission>();
        int permissionId = 1;
        foreach (string module in modules)
        {
            foreach (string action in actions)
            {
                permissions.Add(new Permission
                {
                    PermissionId = permissionId++,
                    Code = $"{module}.{action}",
                    Module = module,
                });
            }
        }

        modelBuilder.Entity<Permission>().HasData(permissions);
    }
}
