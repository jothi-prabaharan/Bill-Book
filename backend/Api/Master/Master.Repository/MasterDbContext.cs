using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Entities;

namespace Master.Repository;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options)
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

        MapXminConcurrency(modelBuilder);
        SeedCountries(modelBuilder);
        SeedCurrencies(modelBuilder);
        SeedIndianStates(modelBuilder);
        SeedTransactionTypes(modelBuilder);
        SeedLedgerTypes(modelBuilder);
        SeedLedgerSources(modelBuilder);
        SeedAccountTypes(modelBuilder);
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

    private static void SeedCountries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>().HasData(
            new Country { CountryId = 1, CountryCode = "IN", CountryName = "India", CurrencyCode = "INR", PhoneCode = "+91" },
            new Country { CountryId = 2, CountryCode = "US", CountryName = "United States", CurrencyCode = "USD", PhoneCode = "+1" },
            new Country { CountryId = 3, CountryCode = "GB", CountryName = "United Kingdom", CurrencyCode = "GBP", PhoneCode = "+44" },
            new Country { CountryId = 4, CountryCode = "AE", CountryName = "United Arab Emirates", CurrencyCode = "AED", PhoneCode = "+971" },
            new Country { CountryId = 5, CountryCode = "SG", CountryName = "Singapore", CurrencyCode = "SGD", PhoneCode = "+65" });
    }

    private static void SeedCurrencies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>().HasData(
            new Currency { CurrencyId = 1, Code = "INR", Name = "Indian Rupee", Symbol = "₹", Format = "##,##,##0.00", DecimalPlaces = 2 },
            new Currency { CurrencyId = 2, Code = "USD", Name = "US Dollar", Symbol = "$", Format = "###,###,##0.00", DecimalPlaces = 2 },
            new Currency { CurrencyId = 3, Code = "GBP", Name = "Pound Sterling", Symbol = "£", Format = "###,###,##0.00", DecimalPlaces = 2 },
            new Currency { CurrencyId = 4, Code = "AED", Name = "UAE Dirham", Symbol = "د.إ", Format = "###,###,##0.00", DecimalPlaces = 2 },
            new Currency { CurrencyId = 5, Code = "SGD", Name = "Singapore Dollar", Symbol = "S$", Format = "###,###,##0.00", DecimalPlaces = 2 });
    }

    private static void SeedIndianStates(ModelBuilder modelBuilder)
    {
        // (GST state code, name) — India (CountryId 1). Codes 25 and 28 are unused historically.
        (string Code, string Name)[] states =
        {
            ("01", "Jammu and Kashmir"), ("02", "Himachal Pradesh"), ("03", "Punjab"),
            ("04", "Chandigarh"), ("05", "Uttarakhand"), ("06", "Haryana"), ("07", "Delhi"),
            ("08", "Rajasthan"), ("09", "Uttar Pradesh"), ("10", "Bihar"), ("11", "Sikkim"),
            ("12", "Arunachal Pradesh"), ("13", "Nagaland"), ("14", "Manipur"), ("15", "Mizoram"),
            ("16", "Tripura"), ("17", "Meghalaya"), ("18", "Assam"), ("19", "West Bengal"),
            ("20", "Jharkhand"), ("21", "Odisha"), ("22", "Chhattisgarh"), ("23", "Madhya Pradesh"),
            ("24", "Gujarat"), ("26", "Dadra and Nagar Haveli and Daman and Diu"), ("27", "Maharashtra"),
            ("29", "Karnataka"), ("30", "Goa"), ("31", "Lakshadweep"), ("32", "Kerala"),
            ("33", "Tamil Nadu"), ("34", "Puducherry"), ("35", "Andaman and Nicobar Islands"),
            ("36", "Telangana"), ("37", "Andhra Pradesh"), ("38", "Ladakh"), ("97", "Other Territory"),
        };

        var rows = new List<State>();
        for (int i = 0; i < states.Length; i++)
        {
            rows.Add(new State
            {
                StateId = i + 1,
                CountryId = 1,
                StateCode = states[i].Code,
                StateName = states[i].Name,
                IsActive = true,
            });
        }

        modelBuilder.Entity<State>().HasData(rows);
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
}
