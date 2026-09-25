using Shared.Kernel.Apps;
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

    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();

    public DbSet<Organization> Organizations => Set<Organization>();


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

    public DbSet<MenuPermission> MenuPermissions => Set<MenuPermission>();

    public DbSet<UserOrganizationRole> UserOrganizationRoles => Set<UserOrganizationRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    /// <summary>Exchange rate history, schema <c>rat</c> (TK-24).</summary>
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    /// <summary>Metal rate history, schema <c>rat</c> (TK-24).</summary>
    public DbSet<MetalRate> MetalRates => Set<MetalRate>();

    /// <summary>Each fetch of a rate source, schema <c>rat</c> (TK-26).</summary>
    public DbSet<RateFetchRun> RateFetchRuns => Set<RateFetchRun>();

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

        // rat: global rate history (TK-24). One row per pair or metal and
        // purity, per date, per source, so a manual correction sits beside the
        // scraped figure rather than overwriting it.
        modelBuilder.Entity<ExchangeRate>(b =>
        {
            b.ToTable("ExchangeRates", "rat");
            b.HasKey(e => e.ExchangeRateId);
            b.Property(e => e.Rate).HasPrecision(18, 8);
            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(e => new { e.FromCurrencyCode, e.ToCurrencyCode, e.RateDate, e.Source }).IsUnique();
        });

        modelBuilder.Entity<RateFetchRun>(b =>
        {
            b.ToTable("RateFetchRuns", "rat");
            b.HasKey(e => e.RateFetchRunId);
            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.FollowUpStatus).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.Source, e.RunDate, e.Status });
            b.HasIndex(e => e.FollowUpStatus);
        });

        modelBuilder.Entity<MetalRate>(b =>
        {
            b.ToTable("MetalRates", "rat");
            b.HasKey(e => e.MetalRateId);
            b.Property(e => e.RatePerGram).HasPrecision(18, 4);
            b.Property(e => e.Metal).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(e => new { e.Metal, e.PurityCode, e.RateDate, e.Source }).IsUnique();
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
            // By name, so the values already in the column still read (TK-42).
            b.Property(e => e.PlanTier).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<TenantDatabase>(b =>
        {
            b.Property(e => e.PlanType).HasConversion<string>().HasMaxLength(20);
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

        modelBuilder.Entity<License>(b =>
        {
            b.HasKey(e => e.LicenseId);
            // One licence per app per customer (TK-42).
            b.HasIndex(e => new { e.CustomerId, e.App }).IsUnique();
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
            // A name is unique within an app: Owner of RetailErp and Owner of
            // Payroll are two rows (TK-42).
            b.HasIndex(e => new { e.CustomerId, e.App, e.SystemName }).IsUnique();
            // Postgres treats nulls as distinct, so system-role names need a partial guard.
            b.HasIndex(e => new { e.App, e.SystemName })
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

            // Unique: the hash is how a presented token is found, and two rows
            // sharing one would make "which token is this" ambiguous at exactly
            // the moment it must not be. It also makes a replay of a hash that
            // somehow got re-minted a write failure rather than a silent second
            // session.
            b.HasIndex(e => e.TokenHash).IsUnique();
            b.HasIndex(e => new { e.UserId, e.ExpiresAt });

            // Reuse detection revokes a whole family at once, so the family is
            // the access path.
            b.HasIndex(e => e.FamilyId);
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

        // ---- Menu ----
        // Rail modules, the sections in their panels and the screens in those
        // sections are all Menu rows, told apart by Type and joined by ParentId.
        modelBuilder.Entity<Menu>(b =>
        {
            b.HasKey(e => e.MenuId);
            b.Property(e => e.Type).HasConversion<string>().HasMaxLength(10);

            // Unique among siblings, which is what a code is for. Postgres treats
            // NULLs as distinct in a unique index, so rail modules — whose ParentId
            // is null — need the second, filtered index to be held to the same rule.
            b.HasIndex(e => new { e.ParentId, e.Code }).IsUnique();
            b.HasIndex(e => e.Code).IsUnique().HasFilter("\"ParentId\" IS NULL");

            b.HasIndex(e => new { e.ParentId, e.DisplayOrder });

            // Deleting a section takes its screens with it. Restrict would make a
            // populated module undeletable, which is not the same thing as safe.
            b.HasMany(e => e.Children)
                .WithOne(e => e.Parent!)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.Permissions)
                .WithOne(e => e.Menu)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuPermission>(b =>
        {
            b.HasKey(e => e.MenuPermissionId);
            b.HasIndex(e => new { e.MenuId, e.PermissionCode }).IsUnique();
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
        // The seed sets scalar properties only — HasData cannot carry a populated
        // navigation — so both lists go in as they are built.
        modelBuilder.Entity<Menu>().HasData(MenuSeed.Build());
        modelBuilder.Entity<MenuPermission>().HasData(MenuSeed.BuildPermissions());
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
            },

            // The only display format the product did not already own.
            //
            // Currency and number formatting were never missing: mst.Currency
            // carries Symbol, SymbolPosition, DecimalPlaces and Format — the
            // grouping mask that distinguishes Indian ##,##,##0.00 from Western
            // ###,###,##0.00 — and unitPrice.decimals and quantity.decimals
            // above cover the non-money cases. Duplicating any of that into a
            // config key would give two places to change a currency's rendering
            // and no rule about which one wins. A date pattern belongs to the
            // branch rather than to a currency, so it has nowhere else to live.
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000005"),
                OrgId = null,
                Code = "format.date",
                Name = "Date Format",
                Description = "Display pattern for dates, e.g. dd/MM/yyyy",
                DataType = ConfigDataType.Text,
                Value = "dd/MM/yyyy",
                Category = "Formatting",
                IsSystem = true,
            });
    }

    /// <summary>
    /// Every module the permission catalogue is seeded for — the whole set of
    /// values a <c>RequireModulePermission</c> may name.
    ///
    /// <b>Public because a controller naming a module that is not here is a
    /// locked door rather than a refused request</b>, and nothing else in the
    /// product reads the two sides against each other. Leads and Tickets
    /// shipped demanding <c>customer</c>, which was never seeded, so every
    /// request to either was refused for every role including Owner. See
    /// <c>Customer.Api.Tests.PermissionModuleTests</c>, which asserts against
    /// this array rather than a copy of it.
    /// </summary>
    public static readonly string[] PermissionModules =
    {
        "dashboard", "contacts", "crm", "inventory", "sales", "purchase",
        "accounting", "banking", "reports", "settings", "support", "platform",

        // H1 (TK-48). Appended, so every existing permission keeps its id.
        "employee", "hrm",

        // H4 (TK-51).
        "payroll",

        // H2, H3 (TK-49, TK-50).
        "leave", "attendance",

        // H9 (TK-56).
        "claims",

        // School (S0, TK-60). attendance is already above and is shared: HRMS
        // uses it for staff attendance, School for student attendance.
        "sis", "admission", "fee", "facility", "workorder", "preventive", "amc",
    };

    /// <summary>
    /// Permissions outside the module × action grid (TK-60): a verb only one
    /// module has. Ids from 10,001, so a module appended to the grid later never
    /// meets one.
    /// </summary>
    public static readonly IReadOnlyList<(int PermissionId, string Module, string Action, App Apps)> ExtraPermissions =
    [
        // Editing a locked day's register (S3). School's student register only,
        // though the attendance module is shared with HRMS.
        (10_001, "attendance", "unlock", App.School),

        // Closing a work order, which is terminal (S6).
        (10_002, "workorder", "close", App.School),
    ];

    /// <summary>
    /// School's roles beside its Owner (TK-60), with fixed ids in the reserved
    /// range, and the modules each holds. A module listed with actions grants
    /// only those; one listed alone grants all of it.
    /// </summary>
    public static readonly IReadOnlyList<(int RoleId, string Name, string[] Grants)> SchoolRoles =
    [
        (1_000_101, "Principal", ["contacts", "sis", "admission", "attendance", "fee", "facility", "workorder", "preventive", "amc", "employee.view", "attendance.unlock", "workorder.close"]),
        (1_000_102, "Office Admin", ["contacts", "sis", "admission", "attendance", "fee.view", "fee.create", "fee.print", "employee.view", "attendance.unlock"]),
        (1_000_103, "Accountant", ["fee", "contacts.view", "sis.view", "admission.view"]),
        (1_000_104, "Teacher", ["sis.view", "sis.edit", "attendance.view", "attendance.create", "attendance.edit"]),
        (1_000_105, "Maintenance", ["facility", "contacts.view", "workorder.view", "workorder.create", "workorder.edit", "preventive", "amc.view", "employee.view"]),
        (1_000_106, "Viewer", ["*.view"]),
    ];

    /// <summary>
    /// Which apps' roles may hold a module's permissions (TK-42).
    ///
    /// <c>settings</c> covers users, roles, branches, organization settings,
    /// currencies, configuration, SMTP, API keys and numbering, which every app
    /// shares. <c>platform</c> is operator-only and granted by a flag on the user,
    /// never by a role, so it belongs to no one app. Everything else is RetailErp's
    /// until another app's module is seeded.
    /// </summary>
    /// <summary>
    /// The seeded Owner role of each app (TK-45). RetailErp's is the original
    /// Owner, role 1.
    ///
    /// <b>The others sit in a reserved range, 1,000,000 plus the app's flag</b>,
    /// not at 6, 7 and 8. Customer-made roles take ids from the same identity
    /// column, which the seed moves past the highest seeded id, so on a
    /// database already in use 6 to 8 belong to somebody's roles.
    /// </summary>
    public static readonly IReadOnlyList<(App App, int RoleId)> AppOwnerRoles =
    [
        (App.RetailErp, 1),
        (App.School, 1_000_000 + (int)App.School),
        (App.Hrms, 1_000_000 + (int)App.Hrms),
        (App.Payroll, 1_000_000 + (int)App.Payroll),
    ];

    /// <summary>The seeded Owner role of <paramref name="app"/>.</summary>
    public static int OwnerRoleOf(App app) =>
        AppOwnerRoles.First(o => o.App == app).RoleId;

    public static App AppsOfModule(string module) => module switch
    {
        "settings" or "platform" => App.All,

        // The employee master and organisation setup are shared by the apps
        // that employ people on the books: HRMS, Payroll and School (TK-48).
        "employee" => App.Hrms | App.Payroll | App.School,

        // Lifecycle, letters, assets, announcements, leave and claims are HRMS's own.
        "hrm" or "leave" or "claims" => App.Hrms,

        // Staff attendance in HRMS, student attendance in School (TK-60). A
        // role belongs to one app and a token carries one app, so the shared
        // module never lets one app's role into the other's screens.
        "attendance" => App.Hrms | App.School,

        // Guardians and maintenance vendors are contacts, so School shares the
        // contact master with RetailErp (TK-60).
        "contacts" => App.RetailErp | App.School,

        // School's own (TK-60).
        "sis" or "admission" or "fee" or "facility" or "workorder" or "preventive" or "amc" => App.School,

        // Payroll core, statutory, tax, runs (TK-51).
        "payroll" => App.Payroll,
        _ => App.RetailErp,
    };

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
                App = App.RetailErp,
                IsSystemRole = true,
                IsActive = true,
            });
        }

        // One Owner per other app (TK-45): signing up for an app, or starting
        // its trial, makes the signer that app's Owner. Appended with fixed
        // ids so the RetailErp roles keep theirs.
        foreach ((App app, int roleId) in AppOwnerRoles.Where(o => o.App != App.RetailErp))
        {
            roles.Add(new Role
            {
                RoleId = roleId,
                CustomerId = null,
                SystemName = "Owner",
                DisplayName = "Owner",
                App = app,
                IsSystemRole = true,
                IsActive = true,
            });
        }

        foreach ((int roleId, string name, _) in SchoolRoles)
        {
            roles.Add(new Role
            {
                RoleId = roleId,
                CustomerId = null,
                SystemName = name,
                DisplayName = name,
                App = App.School,
                IsSystemRole = true,
                IsActive = true,
            });
        }

        modelBuilder.Entity<Role>().HasData(roles);

        string[] modules = PermissionModules;
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
                    Apps = AppsOfModule(module),
                });
            }
        }

        foreach ((int extraId, string module, string action, App apps) in ExtraPermissions)
        {
            permissions.Add(new Permission
            {
                PermissionId = extraId,
                Code = $"{module}.{action}",
                Module = module,
                Apps = apps,
            });
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

        // The five system roles are RetailErp's, so they hold RetailErp's
        // permissions only (the grant rule, TK-42). Today that is every one; a
        // module another app adds later is not RetailErp's and stays out.
        List<Permission> retail = nonPlatform.Where(p => p.Apps.HasFlag(App.RetailErp)).ToList();

        Grant(owner, retail);
        Grant(administrator, retail);
        Grant(accountant, retail.Where(p => accountantModules.Contains(p.Module)));
        Grant(sales, retail.Where(p => salesModules.Contains(p.Module)));

        // Viewer sees everything and changes nothing. "dashboard.view" style only.
        Grant(viewer, retail.Where(p => p.Code.EndsWith(".view", StringComparison.Ordinal)));

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

        Grant(accountant, retail.Where(p =>
            accountantAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        Grant(sales, retail.Where(p =>
            salesAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        // Each other app's Owner holds every permission its app may hold
        // (TK-45). Each grant's id is fixed by its app and its permission,
        // 1,000,000,000 × the app's flag plus the permission id, so a module
        // added to one app later adds rows without moving any other id, and no
        // id meets a customer role's grants from the identity column.
        foreach ((App app, int roleId) in AppOwnerRoles.Where(o => o.App != App.RetailErp))
        {
            foreach (Permission permission in nonPlatform.Where(p => p.Apps.HasFlag(app)))
            {
                grants.Add(new RolePermission
                {
                    RolePermissionId = 1_000_000_000L * (int)app + permission.PermissionId,
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }

        // School's other roles (TK-60): 1,000,000,000 × School's flag, plus
        // 10,000,000 × the role's place in the list, plus the permission id.
        List<Permission> school = nonPlatform.Where(p => p.Apps.HasFlag(App.School)).ToList();
        for (int k = 0; k < SchoolRoles.Count; k++)
        {
            (int roleId, _, string[] granted) = SchoolRoles[k];
            foreach (Permission permission in school.Where(p => SchoolRoleHolds(granted, p)))
            {
                grants.Add(new RolePermission
                {
                    RolePermissionId = 1_000_000_000L * (int)App.School + 10_000_000L * (k + 1) + permission.PermissionId,
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }

        modelBuilder.Entity<RolePermission>().HasData(grants);
    }

    /// <summary>
    /// Whether a School role's grant list covers a permission: a bare module
    /// covers its whole grid (never the extra verbs, which are named alone),
    /// <c>module.action</c> covers one, and <c>*.view</c> covers every view.
    /// Public for tests.
    /// </summary>
    public static bool SchoolRoleHolds(IEnumerable<string> grants, Permission permission) =>
        grants.Any(g => g == permission.Code
            || (g == "*.view" && permission.Code.EndsWith(".view", StringComparison.Ordinal))
            || (g == permission.Module && permission.PermissionId < ExtraPermissions[0].PermissionId));
}
