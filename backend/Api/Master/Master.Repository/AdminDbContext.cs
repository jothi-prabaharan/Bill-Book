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

    /// <summary>Statutory compliance calendar rules, schema <c>mst</c> (TK-119).</summary>
    public DbSet<ComplianceDueDate> ComplianceDueDates => Set<ComplianceDueDate>();

    /// <summary>Per-branch statutory filing applicability and tracking, schema <c>mst</c> (TK-119).</summary>
    public DbSet<OrgComplianceSetting> OrgComplianceSettings => Set<OrgComplianceSetting>();

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
            b.Property(e => e.BusinessType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AddOns).HasConversion<int>();
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
            // One licence per customer (D-36, TK-127).
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
            // Roles span business types and add-ons (D-36, TK-127).
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
            b.Property(e => e.BusinessType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AddOns).HasConversion<int>();
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
            b.Property(e => e.BusinessType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AddOns).HasConversion<int>();

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

            // School fees (TK-64).
            ("FDM", "Fee Demand", true),
            ("FRC", "Fee Receipt", true),

            // School work orders: parts issued post Dr COGS / Cr Inventory under it (TK-66).
            ("WRK", "Work Order", true),

            // A restaurant order (TK-146): a running tab that posts nothing; its bill is an invoice.
            ("RST", "Restaurant Order", false),

            // A kitchen ticket (TK-147): a station's share of an order, posting nothing.
            ("KOT", "Kitchen Ticket", false),
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

            // An invoice moving delivered goods' cost from Goods Delivered Not
            // Invoiced into cost of sales (TK-90). Its own type because COGS (4)
            // is the key the costing worker replaces per line: filed there, the
            // worker's settled posting for the same line would erase it.
            (7, "GDNI", "Goods delivered not invoiced clearing"),
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
            },

            // How long a portal link works before the contact needs a new one
            // (TK-94). A branch can shorten or lengthen it; revoking ends it at
            // once whatever it says.
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000006"),
                OrgId = null,
                Code = "portal.linkDays",
                Name = "Portal Link Validity (days)",
                Description = "Days a contact's portal link keeps working, unless it is revoked first",
                DataType = ConfigDataType.Number,
                Value = "90",
                Category = "Portal",
                IsSystem = true,
            },

            // The most a sales line may be discounted, as a percentage of its
            // gross value (D-29, TK-102). A contact's own MaxDiscountPercent
            // wins when set; 100 is no limit. A save past it may request an
            // approved override.
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000007"),
                OrgId = null,
                Code = "sales.maxLineDiscountPercent",
                Name = "Maximum Line Discount (%)",
                Description = "The most a sales line may be discounted without an approved override. A contact's own limit wins; 100 is no limit",
                DataType = ConfigDataType.Number,
                Value = "100",
                Category = "Sales",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000008"),
                OrgId = null,
                Code = "restaurant.tables.enabled",
                Name = "Tables",
                Description = "Floors and tables: seat a dine-in order at a table.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000009"),
                OrgId = null,
                Code = "restaurant.kitchen.enabled",
                Name = "Kitchen (KOT)",
                Description = "Send orders to the kitchen as tickets, by station.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000a"),
                OrgId = null,
                Code = "restaurant.recipes.enabled",
                Name = "Recipes",
                Description = "Deduct each dish's ingredients from stock when its bill posts.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000b"),
                OrgId = null,
                Code = "restaurant.table.required",
                Name = "Table required",
                Description = "When a bill must name a table: Off, DineInOnly or Always. Needs tables.",
                DataType = ConfigDataType.Text,
                Value = "Off",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000c"),
                OrgId = null,
                Code = "restaurant.kot.required",
                Name = "KOT required",
                Description = "A bill is refused while any of its lines was never sent to the kitchen. Needs the kitchen.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000d"),
                OrgId = null,
                Code = "restaurant.customer.required",
                Name = "Customer required",
                Description = "When a bill must name a customer: Off, DineInOnly or Always.",
                DataType = ConfigDataType.Text,
                Value = "Off",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000e"),
                OrgId = null,
                Code = "restaurant.waiter.required",
                Name = "Waiter required",
                Description = "A bill must name the waiter who served it.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-00000000000f"),
                OrgId = null,
                Code = "restaurant.serviceCharge.percent",
                Name = "Service charge %",
                Description = "Added to every bill as a line of its own. 0 for none.",
                DataType = ConfigDataType.Number,
                Value = "0",
                Category = "Restaurant",
                IsSystem = true,
            },
            new Configuration
            {
                ConfigId = Guid.Parse("a0000000-0000-0000-0000-000000000010"),
                OrgId = null,
                Code = "restaurant.tips.enabled",
                Name = "Tips",
                Description = "Take a tip on the bill, held for staff rather than earned.",
                DataType = ConfigDataType.Boolean,
                Value = "false",
                Category = "Restaurant",
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

        // School (S0, TK-60). attendance is already above and is shared: HRMS
        // uses it for staff attendance, School for student attendance.
        "sis", "admission", "fee", "facility", "workorder", "preventive", "amc",

        // H9 (TK-56). Appended after School rather than beside leave, so the
        // School permissions TK-60 migrated keep their ids.
        "claims",

        // H10, H11 (TK-57, TK-58).
        "recruitment", "performance", "restaurant",

        // Project accounting (TK-104). RetailErp's, but granted with fixed ids
        // (see FixedGrantModules) so no existing grant is renumbered.
        "projects",
    };

    /// <summary>
    /// RetailErp modules appended after the system roles' grants were first
    /// numbered (TK-104). Their grants take fixed ids — 910,000,000 plus
    /// 100,000 × the role plus the permission id — because appended to the
    /// sequential grants they would renumber every grant after them, and a
    /// migration of that many updates collided on the unique role-permission
    /// index before (TK-70).
    /// </summary>
    public static readonly IReadOnlySet<string> FixedGrantModules = new HashSet<string> { "projects" };

    /// <summary>
    /// Permissions outside the module × action grid (TK-60): a verb only one
    /// module has. Ids from 10,001, so a module appended to the grid later never
    /// meets one.
    /// </summary>
    public static readonly IReadOnlyList<(int PermissionId, string Module, string Action, BusinessType? BusinessType, AddOn AddOns)> ExtraPermissions =
    [
        // Editing a locked day's register (S3). School's student register only,
        // though the attendance module is shared with HRMS.
        (10_001, "attendance", "unlock", BusinessType.School, AddOn.None),

        // Closing a work order, which is terminal (S6).
        (10_002, "workorder", "close", BusinessType.School, AddOn.None),

        // The manual e-invoice actions: retrying a registration, and generating
        // or cancelling an e-way bill (TK-92). Posting still needs
        // sales.approve and cancelling an IRN rides on sales.void.
        (10_003, "sales", "einvoice", BusinessType.General, AddOn.EInvoicing),
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
        (1_000_104, "Teacher", ["sis.view", "sis.edit", "attendance.view", "attendance.create", "attendance.edit"]),
        (1_000_105, "Maintenance", ["facility", "contacts.view", "workorder.view", "workorder.create", "workorder.edit", "preventive", "amc.view", "employee.view"]),
    ];

    public static readonly IReadOnlyList<(App App, int RoleId)> AppOwnerRoles =
    [
        (App.RetailErp, 1),
    ];

    /// <summary>The seeded Owner role (D-36: roles span types, single Owner role 1).</summary>
    public static int OwnerRoleOf(App app) => 1;

    public static (BusinessType? BusinessType, AddOn AddOns) ScopeOfModule(string module) => module switch
    {
        "settings" or "platform" => (null, AddOn.None),
        "employee" => (BusinessType.School, AddOn.Hrms | AddOn.Payroll),
        "hrm" or "leave" or "claims" or "recruitment" or "performance" => (null, AddOn.Hrms),
        "attendance" => (BusinessType.School, AddOn.Hrms),
        "payroll" => (null, AddOn.Payroll),
        "contacts" => (BusinessType.General, AddOn.Crm),
        "sis" or "admission" or "fee" or "facility" or "workorder" or "preventive" or "amc" => (BusinessType.School, AddOn.None),
        "crm" => (null, AddOn.Crm),
        "restaurant" => (BusinessType.Restaurant, AddOn.None),
        _ => (BusinessType.General, AddOn.None),
    };

    public static App AppsOfModule(string module) => module switch
    {
        "settings" or "platform" => App.All,
        "employee" => App.Hrms | App.Payroll | App.School,
        "hrm" or "leave" or "claims" or "recruitment" or "performance" => App.Hrms,
        "attendance" => App.Hrms | App.School,
        "contacts" => App.RetailErp | App.School,
        "restaurant" => App.RetailErp,
        "sis" or "admission" or "fee" or "facility" or "workorder" or "preventive" or "amc" => App.School,
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
            (BusinessType? bType, AddOn addOns) = ScopeOfModule(module);
            foreach (string action in actions)
            {
                permissions.Add(new Permission
                {
                    PermissionId = permissionId++,
                    Code = $"{module}.{action}",
                    Module = module,
                    BusinessType = bType,
                    AddOns = addOns,
                });
            }
        }

        foreach ((int extraId, string module, string action, BusinessType? bType, AddOn addOns) in ExtraPermissions)
        {
            permissions.Add(new Permission
            {
                PermissionId = extraId,
                Code = $"{module}.{action}",
                Module = module,
                BusinessType = bType,
                AddOns = addOns,
            });
        }

        modelBuilder.Entity<Permission>().HasData(permissions);
        SeedRolePermissions(modelBuilder, permissions);
    }

    /// <summary>
    /// Links system roles to their permissions. Module-level grants: a role
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
        // permissions only (the grant rule, TK-42).
        //
        // The extras (ids from 10,000) are left out of these sequential grants
        // and granted below with fixed ids: appended here they would renumber
        // every grant after them, and a migration of that many updates collided
        // on the unique role-permission index before (TK-70).
        string[] retailModules = { "dashboard", "contacts", "crm", "inventory", "sales", "purchase", "accounting", "banking", "reports", "settings", "support" };
        List<Permission> retail = nonPlatform
            .Where(p => retailModules.Contains(p.Module) && p.PermissionId < 10_000)
            .ToList();

        Grant(owner, retail);
        Grant(administrator, retail);
        Grant(accountant, retail.Where(p => accountantModules.Contains(p.Module)));
        Grant(sales, retail.Where(p => salesModules.Contains(p.Module)));

        // Viewer sees everything and changes nothing. "dashboard.view" style only.
        Grant(viewer, retail.Where(p => p.Code.EndsWith(".view", StringComparison.Ordinal)));

        // Read-only grants outside a role's own modules, for things the role has
        // to look at to do its own job.
        string[] accountantAlsoReads = { "contacts", "inventory" };
        string[] salesAlsoReads = { "inventory" };

        Grant(accountant, retail.Where(p =>
            accountantAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        Grant(sales, retail.Where(p =>
            salesAlsoReads.Contains(p.Module)
            && p.Code.EndsWith(".view", StringComparison.Ordinal)));

        // RetailErp's extra permissions, to the roles that hold their module:
        // 900,000,000 + 100,000 × the role + the permission id, clear of the
        // sequential ids above and of the per-app ranges below.
        foreach (Permission extra in nonPlatform.Where(p => retailModules.Contains(p.Module) && p.PermissionId >= 10_000))
        {
            foreach (int roleId in new[] { owner, administrator, accountant, sales })
            {
                bool holds = roleId is owner or administrator
                    || (roleId == accountant && accountantModules.Contains(extra.Module))
                    || (roleId == sales && salesModules.Contains(extra.Module));
                if (holds)
                {
                    grants.Add(new RolePermission
                    {
                        RolePermissionId = 900_000_000L + (100_000L * roleId) + extra.PermissionId,
                        RoleId = roleId,
                        PermissionId = extra.PermissionId,
                    });
                }
            }
        }

        // Projects (TK-104): Owner, Administrator and Accountant run them; Sales
        // and Viewer read them. Fixed ids, so nothing above moves.
        foreach (Permission permission in nonPlatform.Where(p => FixedGrantModules.Contains(p.Module) && p.PermissionId < 10_000))
        {
            bool isView = permission.Code.EndsWith(".view", StringComparison.Ordinal);
            foreach (int roleId in new[] { owner, administrator, accountant, sales, viewer })
            {
                if (roleId is sales or viewer && !isView)
                {
                    continue;
                }

                grants.Add(new RolePermission
                {
                    RolePermissionId = 910_000_000L + (100_000L * roleId) + permission.PermissionId,
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }

        // Owner and Administrator hold all non-platform permissions across all business types
        // and add-ons (D-36: roles span types). Non-retail permissions granted to Owner and
        // Administrator take fixed ids: 920,000,000 + 100,000 × the role + the permission id.
        List<Permission> nonRetail = nonPlatform
            .Where(p => !retailModules.Contains(p.Module) && !FixedGrantModules.Contains(p.Module))
            .ToList();

        foreach (Permission permission in nonRetail)
        {
            foreach (int roleId in new[] { owner, administrator })
            {
                grants.Add(new RolePermission
                {
                    RolePermissionId = 920_000_000L + (100_000L * roleId) + permission.PermissionId,
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }

        // School's other roles (TK-60): 1,000,000,000 × School's flag, plus
        // 10,000,000 × the role's place in the list, plus the permission id.
        List<Permission> school = nonPlatform.Where(p =>
            p.BusinessType == BusinessType.School
            || p.Module == "contacts"
            || p.Module == "attendance"
            || p.Module == "employee").ToList();

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

        modelBuilder.Entity<ComplianceDueDate>(b =>
        {
            b.HasKey(e => e.ComplianceDueDateId);
            b.Property(e => e.ComplianceDueDateId).ValueGeneratedNever();
            b.Property(e => e.Frequency).HasConversion<int>();
            b.HasIndex(e => new { e.Category, e.FormName });
            b.HasData(
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 1,
                    Category = "GST",
                    FormName = "GSTR-1",
                    Description = "Outward supplies monthly return",
                    Frequency = ComplianceFrequency.Monthly,
                    DueDay = 11,
                    DueMonthOffset = 1,
                    RuleDescription = "11th of the following month",
                    ApplicabilityCondition = "GSTIN",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 2,
                    Category = "GST",
                    FormName = "GSTR-3B",
                    Description = "Monthly summary return and tax payment",
                    Frequency = ComplianceFrequency.Monthly,
                    DueDay = 20,
                    DueMonthOffset = 1,
                    RuleDescription = "20th of the following month",
                    ApplicabilityCondition = "GSTIN",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 3,
                    Category = "TDS",
                    FormName = "TDS Deposit",
                    Description = "Monthly tax deducted at source deposit (Challan 281)",
                    Frequency = ComplianceFrequency.Monthly,
                    DueDay = 7,
                    DueMonthOffset = 1,
                    RuleDescription = "7th of the following month (30th April for March)",
                    ApplicabilityCondition = "TAN",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 4,
                    Category = "TDS",
                    FormName = "Form 26Q",
                    Description = "Quarterly TDS return for payments other than salary",
                    Frequency = ComplianceFrequency.Quarterly,
                    DueDay = 31,
                    DueMonthOffset = 1,
                    RuleDescription = "31st of the month following quarter (31st May for Q4)",
                    ApplicabilityCondition = "TAN",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 5,
                    Category = "TDS",
                    FormName = "Form 27Q",
                    Description = "Quarterly TDS return for payments to non-residents",
                    Frequency = ComplianceFrequency.Quarterly,
                    DueDay = 31,
                    DueMonthOffset = 1,
                    RuleDescription = "31st of the month following quarter (31st May for Q4)",
                    ApplicabilityCondition = "TAN",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 6,
                    Category = "IncomeTax",
                    FormName = "Advance Tax",
                    Description = "Quarterly advance income tax installment",
                    Frequency = ComplianceFrequency.Quarterly,
                    DueDay = 15,
                    DueMonthOffset = 0,
                    RuleDescription = "15th of June, September, December, and March",
                    ApplicabilityCondition = "All",
                    IsActive = true,
                },
                new ComplianceDueDate
                {
                    ComplianceDueDateId = 7,
                    Category = "MSME",
                    FormName = "MSME Form 1",
                    Description = "Half-yearly return to MCA for outstanding payments to MSME vendors",
                    Frequency = ComplianceFrequency.HalfYearly,
                    DueDay = 31,
                    DueMonthOffset = 1,
                    RuleDescription = "31st October (for Apr–Sep) and 30th April (for Oct–Mar)",
                    ApplicabilityCondition = "CIN",
                    IsActive = true,
                }
            );
        });

        modelBuilder.Entity<OrgComplianceSetting>(b =>
        {
            b.HasKey(e => e.OrgComplianceSettingId);
            b.HasIndex(e => new { e.OrgId, e.ComplianceDueDateId }).IsUnique();
        });

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


