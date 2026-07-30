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

        MapXminConcurrency(modelBuilder);
        SeedCountries(modelBuilder);
        SeedCurrencies(modelBuilder);
        SeedIndianStates(modelBuilder);
        SeedTransactionTypes(modelBuilder);
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
}
