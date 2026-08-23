using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Entities;

namespace Master.Repository;

/// <summary>
/// The mst schema, in the master database — the one database every customer
/// shares. Three schemas were folded into it: the reference data that was always
/// mst, the tenant directory that was mst, and the users, roles and tokens that
/// were mst.
///
/// They belong together because they are asked the same questions in the same
/// breath. Signing in reads a user, the organizations they can reach, and the
/// licence on the customer behind them — three tables that were in three schemas
/// behind two HTTP calls, and are now one query. Nothing here is per-branch, so
/// there is no OrgId filter and no RLS: this is a plain DbContext, and the
/// isolation that matters happens in the per-customer databases.
///
/// <b>Contacts did not come with them.</b> A contact is a branch's own record and
/// lives in that customer's own database, so it kept its own context and its own
/// schema — see <see cref="ContactsDbContext"/>. One API host, two databases.
/// </summary>
public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<State> States => Set<State>();

    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();

    public DbSet<LedgerType> LedgerTypes => Set<LedgerType>();

    public DbSet<LedgerSource> LedgerSources => Set<LedgerSource>();

    public DbSet<AccountType> AccountTypes => Set<AccountType>();

    public DbSet<HsnSacCode> HsnSacCodes => Set<HsnSacCode>();

    // ---- mst: the tenant directory. ----

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<CustomerDatabase> CustomerDatabases => Set<CustomerDatabase>();

    public DbSet<License> Licenses => Set<License>();

    public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

    public DbSet<OrgCurrency> OrgCurrencies => Set<OrgCurrency>();

    public DbSet<Configuration> Configurations => Set<Configuration>();

    // ---- mst: users, roles and tokens. ----

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<SubMenu> SubMenus => Set<SubMenu>();

    public DbSet<SubMenuPermission> SubMenuPermissions => Set<SubMenuPermission>();

    public DbSet<UserOrganizationRole> UserOrganizationRoles => Set<UserOrganizationRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mst");

        modelBuilder.Entity<Country>(b =>
        {
            b.HasKey(e => e.CountryId);
            b.Property(e => e.CountryId).ValueGeneratedNever();
            b.HasIndex(e => e.CountryCode).IsUnique();
            b.HasMany(e => e.States).WithOne(e => e.Country!).HasForeignKey(e => e.CountryId);
        });

        modelBuilder.Entity<State>(b =>
        {
            b.HasKey(e => e.StateId);
            b.Property(e => e.StateId).ValueGeneratedNever();
            b.HasIndex(e => new { e.CountryId, e.StateCode }).IsUnique();
        });

        modelBuilder.Entity<Currency>(b =>
        {
            b.HasKey(e => e.CurrencyId);
            b.Property(e => e.CurrencyId).ValueGeneratedNever();
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.SymbolPosition).HasConversion<string>().HasMaxLength(6);
        });

        modelBuilder.Entity<TransactionType>(b =>
        {
            b.HasKey(e => e.Code);
            b.Property(e => e.Code).HasMaxLength(3).IsFixedLength();
            b.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<LedgerType>(b =>
        {
            b.HasKey(e => e.LedgerTypeId);
            b.Property(e => e.LedgerTypeId).ValueGeneratedNever();
            b.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<LedgerSource>(b =>
        {
            b.HasKey(e => e.LedgerSourceId);
            b.Property(e => e.LedgerSourceId).ValueGeneratedNever();
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.Direction).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<AccountType>(b =>
        {
            b.HasKey(e => e.AccountTypeId);
            b.Property(e => e.AccountTypeId).ValueGeneratedNever();
            b.HasIndex(e => e.SystemName).IsUnique();
            b.Property(e => e.NormalBalance).HasConversion<string>().HasMaxLength(6);
            b.Property(e => e.ReportSection).HasConversion<string>().HasMaxLength(15);
        });

        modelBuilder.Entity<HsnSacCode>(b =>
        {
            b.HasKey(e => e.HsnSacCodeId);
            b.Property(e => e.HsnSacCodeId).ValueGeneratedNever();
            b.HasIndex(e => e.Code).IsUnique();
            b.HasIndex(e => new { e.CodeType, e.ChapterCode });
            b.Property(e => e.CodeType).HasConversion<string>().HasMaxLength(3);
            b.Property(e => e.DefaultGstRate).HasColumnType("decimal(5,2)");
        });

        // ---- mst ----
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
            b.Property(e => e.Vertical).HasConversion<string>().HasMaxLength(20);
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

        // ---- mst ----
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

        // ---- Menu & SubMenu ----
        modelBuilder.Entity<Menu>(b =>
        {
            b.HasKey(e => e.MenuId);
            b.HasIndex(e => e.Code).IsUnique();
            b.HasMany(e => e.SubMenus).WithOne(e => e.Menu).HasForeignKey(e => e.MenuId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubMenu>(b =>
        {
            b.HasKey(e => e.SubMenuId);
            b.HasIndex(e => new { e.MenuId, e.Code }).IsUnique();
            b.HasMany(e => e.Permissions).WithOne(e => e.SubMenu).HasForeignKey(e => e.SubMenuId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubMenuPermission>(b =>
        {
            b.HasKey(e => e.SubMenuPermissionId);
            b.HasIndex(e => new { e.SubMenuId, e.PermissionCode }).IsUnique();
        });

        MapXminConcurrency(modelBuilder);
        SeedCountries(modelBuilder);
        SeedCurrencies(modelBuilder);
        SeedTransactionTypes(modelBuilder);
        SeedLedgerTypes(modelBuilder);
        SeedLedgerSources(modelBuilder);
        SeedAccountTypes(modelBuilder);
        modelBuilder.Entity<HsnSacCode>().HasData(SeedData.HsnSacSeed.Build());
        SeedConfigurations(modelBuilder);
        SeedRolesAndPermissions(modelBuilder);
        modelBuilder.Entity<Menu>().HasData(MenuSeed.Build().SelectMany(m => new[] { m }));
        modelBuilder.Entity<SubMenu>().HasData(MenuSeed.Build().SelectMany(m => m.SubMenus));
        modelBuilder.Entity<SubMenuPermission>().HasData(MenuSeed.Build().SelectMany(m => m.SubMenus.SelectMany(sm => sm.Permissions)));
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

    private static void SeedCountries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>().HasData(SeedData.GeographySeed.GetCountries());
    }

    private static void SeedCurrencies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>().HasData(SeedData.GeographySeed.GetCurrencies());
    }


    private static void SeedTransactionTypes(ModelBuilder modelBuilder)
    {
        // (code, name, posts to the ledger). Quotes and orders are commercial
        // documents only — nothing hits the GL until they become an invoice/bill.
        (string Code, string Name, bool Posts)[] types =
        {
            ("QTE", "Quote", false),
            ("BIL", "Bill", true),
            ("POR", "Purchase Order", false),
            ("GRN", "Goods Receipt", true),
            ("SOR", "Sales Order", false),
            ("DLC", "Delivery Challan", true),
            ("INV", "Invoice", true),
            ("CRN", "Credit Note", true),
            ("DBN", "Debit Note", true),
            ("JRN", "Journal", true),
            ("SPM", "Spend Money", true),
            ("RCM", "Receive Money", true),
            ("TRM", "Transfer Money", true),
            ("OPB", "Opening Balance", true),
            ("DEP", "Depreciation", true),
            ("STA", "Stock Adjustment", true),
            ("POS", "POS Sale", true),
        };

        modelBuilder.Entity<TransactionType>().HasData(
            types.Select(t => new TransactionType
            {
                Code = t.Code,
                Name = t.Name,
                IsLedgerPosting = t.Posts,
                IsActive = true,
            }));
    }

    private static void SeedLedgerTypes(ModelBuilder modelBuilder)
    {
        (int Id, string Code, string Name)[] types =
        {
            (1, "ITEM", "Line item"),
            (2, "TAX", "Tax"),
            (3, "CONTROL", "AP / AR / bank / cash control leg"),
            (4, "COGS", "Cost of goods sold"),
            (5, "FX", "Realized exchange gain or loss"),
            (6, "ROUNDOFF", "Rounding"),
        };

        modelBuilder.Entity<LedgerType>().HasData(
            types.Select(t => new LedgerType
            {
                LedgerTypeId = t.Id,
                Code = t.Code,
                Name = t.Name,
                IsActive = true,
            }));
    }

    private static void SeedLedgerSources(ModelBuilder modelBuilder)
    {
        // Payment and refund are paired in opposite directions so each pair
        // reconciles against the same document.
        (int Id, string Code, string Name, LedgerDirection Direction)[] sources =
        {
            (1, "TRANSACTION", "Document posting", LedgerDirection.Both),
            (2, "BILLPAYMENT", "Bill payment", LedgerDirection.Out),
            (3, "INVOICEPAYMENT", "Invoice payment", LedgerDirection.In),
            (4, "BILLREFUND", "Bill refund received", LedgerDirection.In),
            (5, "INVOICEREFUND", "Invoice refund paid", LedgerDirection.Out),
            (6, "CREDITNOTEREFUND", "Credit note refund paid", LedgerDirection.Out),
            (7, "DEBITNOTEREFUND", "Debit note refund received", LedgerDirection.In),
            (8, "VENDORPREPAYMENT", "Advance paid to vendor", LedgerDirection.Out),
            (9, "CUSTOMERPREPAYMENT", "Advance received from customer", LedgerDirection.In),
            (10, "ALLOCATION", "Credit note, debit note or prepayment allocation", LedgerDirection.Both),
            (11, "MONEYTRANSFER", "Bank or cash transfer", LedgerDirection.Both),
            (12, "JOURNAL", "Manual journal", LedgerDirection.Both),
            (13, "OPENINGBALANCE", "Opening balance", LedgerDirection.Both),
            (14, "DEPRECIATION", "Depreciation", LedgerDirection.Out),
            (15, "STOCKADJUSTMENT", "Stock adjustment", LedgerDirection.Both),

            // Overpayment is not a document type of its own — it is a payment
            // that ran past what was owed, and the excess is an advance. The two
            // halves land on one document carrying different sources, which is
            // why the source sits on the ledger leg rather than on the posting.
            //
            // The excess is marked as an overpayment rather than as an ordinary
            // advance, and that is the whole reason these two exist. Refunding an
            // overpayment and refunding a deliberate advance clear different
            // balances — the excess and the deposit are held apart, so the two
            // refund sources below have nothing to tell them apart without the
            // distinction here.
            (16, "VENDOROVERPAYMENT", "Overpayment to vendor", LedgerDirection.Out),
            (17, "CUSTOMEROVERPAYMENT", "Overpayment from customer", LedgerDirection.In),

            // Money held for a customer, given back. 18 clears the overpayment
            // balance and 19 the prepayment balance; they differ in how the
            // credit arose, which is exactly what a ledger source is for.
            (18, "CUSTOMEROVERPAYMENTREFUND", "Customer overpayment refunded", LedgerDirection.Out),
            (19, "CUSTOMERPREPAYMENTREFUND", "Customer advance refunded", LedgerDirection.Out),
        };

        modelBuilder.Entity<LedgerSource>().HasData(
            sources.Select(s => new LedgerSource
            {
                LedgerSourceId = s.Id,
                Code = s.Code,
                Name = s.Name,
                Direction = s.Direction,
                IsActive = true,
            }));
    }

    private static void SeedAccountTypes(ModelBuilder modelBuilder)
    {
        // Ids 1-5 are contractual. Income (4) and Expense (5) stay separate —
        // gross profit only exists because they are distinct types.
        (int Id, string Name, NormalBalance Balance, ReportSection Section)[] types =
        {
            (1, "Asset", NormalBalance.Debit, ReportSection.BalanceSheet),
            (2, "Liability", NormalBalance.Credit, ReportSection.BalanceSheet),
            (3, "Equity", NormalBalance.Credit, ReportSection.BalanceSheet),
            (4, "Income", NormalBalance.Credit, ReportSection.ProfitAndLoss),
            (5, "Expense", NormalBalance.Debit, ReportSection.ProfitAndLoss),
        };

        modelBuilder.Entity<AccountType>().HasData(
            types.Select(t => new AccountType
            {
                AccountTypeId = t.Id,
                SystemName = t.Name,
                DisplayName = t.Name,
                NormalBalance = t.Balance,
                ReportSection = t.Section,
                SortOrder = (short)t.Id,
                IsActive = true,
            }));
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
        SeedRolePermissions(modelBuilder, permissions);
    }

    /// <summary>
    /// Links the 5 system roles to their permissions. Module-level grants: a role
    /// that owns a module gets all 10 actions in it, including approve, void and
    /// AllUserData.
    /// </summary>
    private static void SeedRolePermissions(ModelBuilder modelBuilder, List<Permission> permissions)
    {
        const int owner = 1;
        const int administrator = 2;
        const int accountant = 3;
        const int sales = 4;
        const int viewer = 5;

        string[] accountantModules = { "accounting", "banking", "reports", "purchase" };
        string[] salesModules = { "sales", "contacts", "crm" };

        var grants = new List<RolePermission>();
        long id = 1;

        void Grant(int roleId, IEnumerable<Permission> matched)
        {
            foreach (Permission permission in matched)
            {
                grants.Add(new RolePermission
                {
                    RolePermissionId = id++,
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }

        // platform.* is operator-only and never granted to a tenant role.
        List<Permission> nonPlatform = permissions.Where(p => p.Module != "platform").ToList();

        Grant(owner, nonPlatform);
        Grant(administrator, nonPlatform);
        Grant(accountant, nonPlatform.Where(p => accountantModules.Contains(p.Module)));
        Grant(sales, nonPlatform.Where(p => salesModules.Contains(p.Module)));

        // Viewer sees everything and changes nothing. "dashboard.view" style only.
        Grant(viewer, nonPlatform.Where(p => p.Code.EndsWith(".view", StringComparison.Ordinal)));

        // Read-only grants outside a role's own modules, for things the role has
        // to look at to do its own job. These were invisible until permissions
        // were actually enforced: Identity minted the claims from the beginning
        // and no service read one, so the matrix had never met a real screen.
        //
        // Sales cannot sell what it cannot look up, and Accountant values stock
        // and chases receivables that are held per contact. Both are reads —
        // nothing here lets a salesperson edit an item or an accountant edit a
        // contact, which would be a different decision.
        string[] accountantAlsoReads = { "contacts", "inventory" };
        string[] salesAlsoReads = { "inventory" };

        Grant(accountant, nonPlatform.Where(p =>
            accountantAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        Grant(sales, nonPlatform.Where(p =>
            salesAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        modelBuilder.Entity<RolePermission>().HasData(grants);
    }
}
