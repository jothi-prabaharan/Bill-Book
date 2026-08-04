using Banking.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Banking.Repository;

/// <summary>
/// The bnk schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency.
/// </summary>
public class BankingDbContext : TenantDbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    /// <summary>
    /// Money out. Its own table rather than a row in a shared one, so a payment
    /// carries a payee and no destination account.
    /// </summary>
    public DbSet<SpendMoney> SpendMoney => Set<SpendMoney>();

    public DbSet<SpendMoneyDetail> SpendMoneyDetails => Set<SpendMoneyDetail>();

    /// <summary>Money in. The mirror of <see cref="SpendMoney"/>.</summary>
    public DbSet<ReceiveMoney> ReceiveMoney => Set<ReceiveMoney>();

    public DbSet<ReceiveMoneyDetail> ReceiveMoneyDetails => Set<ReceiveMoneyDetail>();

    /// <summary>
    /// Money between the organization's own accounts. No contact, and no detail
    /// table — a transfer allocates to nothing.
    /// </summary>
    public DbSet<TransferMoney> TransferMoney => Set<TransferMoney>();

    /// <summary>Mapped, not migrated — Accounting owns the table.</summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bnk");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        modelBuilder.Entity<Bank>(b =>
        {
            b.HasKey(e => e.BankId);
            b.HasIndex(e => new { e.OrgId, e.BankCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.BankName })
                .HasDatabaseName("IX_Banks_Order");
        });

        modelBuilder.Entity<BankAccount>(b =>
        {
            b.HasKey(e => e.BankAccountId);

            // One account number per bank. The same number at two banks is
            // legitimate; the same number twice at one bank is a duplicate.
            b.HasIndex(e => new { e.OrgId, e.BankId, e.AccountNumber }).IsUnique();

            // One GL account per bank account, never shared — reconciliation
            // could not tell two accounts apart otherwise.
            b.HasIndex(e => new { e.OrgId, e.LedgerAccountId })
                .IsUnique()
                .HasFilter("\"LedgerAccountId\" IS NOT NULL")
                .HasDatabaseName("IX_BankAccounts_Ledger");

            b.HasIndex(e => e.OrgId)
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_BankAccounts_Default");

            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.AccountName })
                .HasDatabaseName("IX_BankAccounts_Order");

            b.Property(e => e.AccountType).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.OdLimit).HasColumnType("decimal(18,2)");

            b.HasOne<Bank>()
                .WithMany()
                .HasForeignKey(e => e.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // Cash in hand and wallets have no institution; everything else
                // must name one, or the account cannot be reconciled.
                table.HasCheckConstraint(
                    "chk_bank_account_institution",
                    "\"AccountType\" IN ('Cash', 'Wallet') OR \"BankId\" IS NOT NULL");

                // A limit on a kind that cannot be overdrawn is meaningless data.
                table.HasCheckConstraint(
                    "chk_bank_account_od_limit",
                    "\"OdLimit\" IS NULL "
                        + "OR \"AccountType\" IN ('OverDraft', 'CashCredit', 'CreditCard')");
            });
        });

        // Spend and receive share a shape, so they share a configuration. Three
        // tables rather than one does not mean three copies of the same twenty
        // lines — what differs between them is the table, not the rules.
        ConfigureMoneyDocument<SpendMoney>(modelBuilder, "SpendMoney", e => e.SpendMoneyId);
        ConfigureMoneyDocument<ReceiveMoney>(modelBuilder, "ReceiveMoney", e => e.ReceiveMoneyId);

        modelBuilder.Entity<SpendMoney>(b =>
        {
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReceiveMoney>(b =>
        {
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureMoneyDetail<SpendMoneyDetail>(modelBuilder, e => e.SpendMoneyDetailId);
        ConfigureMoneyDetail<ReceiveMoneyDetail>(modelBuilder, e => e.ReceiveMoneyDetailId);

        modelBuilder.Entity<SpendMoneyDetail>(b =>
        {
            b.HasIndex(e => new { e.SpendMoneyId, e.LineNumber }).IsUnique();

            // Cascade: a draft's lines have no meaning without their header, and
            // a posted document is voided rather than deleted.
            b.HasOne<SpendMoney>()
                .WithMany()
                .HasForeignKey(e => e.SpendMoneyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceiveMoneyDetail>(b =>
        {
            b.HasIndex(e => new { e.ReceiveMoneyId, e.LineNumber }).IsUnique();

            b.HasOne<ReceiveMoney>()
                .WithMany()
                .HasForeignKey(e => e.ReceiveMoneyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransferMoney>(b =>
        {
            b.HasKey(e => e.TransferMoneyId);

            b.HasIndex(e => new { e.OrgId, e.TransactionNo })
                .IsUnique()
                .HasFilter("\"TransactionNo\" IS NOT NULL")
                .HasDatabaseName("IX_TransferMoney_Number");

            b.HasIndex(e => new { e.OrgId, e.TransactionDate });

            // Reconciliation reads both ends: a transfer appears on the source
            // account's statement and on the destination's.
            b.HasIndex(e => new { e.OrgId, e.FromBankAccountId, e.TransactionDate })
                .HasDatabaseName("IX_TransferMoney_From");

            b.HasIndex(e => new { e.OrgId, e.ToBankAccountId, e.TransactionDate })
                .HasDatabaseName("IX_TransferMoney_To");

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,8)");

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.FromBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.ToBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "chk_transfer_number_on_post",
                    "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) "
                        + "OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "chk_transfer_posted_stamp",
                    "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");

                table.HasCheckConstraint(
                    "chk_transfer_void_stamp",
                    "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");

                table.HasCheckConstraint("chk_transfer_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("chk_transfer_rate_positive", "\"ExchangeRate\" > 0");

                // Moving money to the account it came from posts two legs that
                // cancel, and reconciles as a transaction that never happened.
                table.HasCheckConstraint(
                    "chk_transfer_distinct_accounts",
                    "\"ToBankAccountId\" <> \"FromBankAccountId\"");
            });
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Everything a spend and a receive share: numbering, dates, money columns,
    /// the lifecycle stamps and the constraints that police them.
    /// </summary>
    private static void ConfigureMoneyDocument<T>(
        ModelBuilder modelBuilder,
        string table,
        System.Linq.Expressions.Expression<Func<T, object?>> key)
        where T : class
    {
        modelBuilder.Entity<T>(b =>
        {
            b.HasKey(key);

            // Filtered, because a draft has no number yet. The uniqueness that
            // matters is over issued numbers.
            b.HasIndex(["OrgId", "TransactionNo"])
                .IsUnique()
                .HasFilter("\"TransactionNo\" IS NOT NULL")
                .HasDatabaseName($"IX_{table}_Number");

            b.HasIndex(["OrgId", "TransactionDate"]);

            // Reconciliation: one account over a period.
            b.HasIndex(["OrgId", "BankAccountId", "TransactionDate"])
                .HasDatabaseName($"IX_{table}_Account");

            // "Show me the payments against this bill" without opening the lines.
            b.HasIndex(["OrgId", "MappingTransactionTypeCode", "MappingTransactionId"])
                .HasDatabaseName($"IX_{table}_Mapping");

            b.Property("Status").HasConversion<string>().HasMaxLength(10);
            b.Property("PaymentMethod").HasConversion<string>().HasMaxLength(20);
            b.Property("Amount").HasColumnType("decimal(18,2)");
            b.Property("ExchangeRate").HasColumnType("decimal(18,8)");

            b.ToTable(t =>
            {
                // The number is taken at post. Both halves matter: a draft
                // holding one has consumed a number it may never use, and a
                // posted document without one is a ledger row nobody can cite.
                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_number_on_post",
                    "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) "
                        + "OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");

                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_posted_stamp",
                    "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");

                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_void_stamp",
                    "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");

                // Zero moves no money, and a negative amount would put the
                // direction on the number rather than on the document type.
                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_amount_positive", "\"Amount\" > 0");

                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_rate_positive", "\"ExchangeRate\" > 0");

                // Half a mapping traces to nothing, on the header exactly as on
                // the line.
                t.HasCheckConstraint(
                    $"chk_{table.ToLowerInvariant()}_mapping_paired",
                    "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
            });
        });
    }

    /// <summary>The allocation line, identical on both sides.</summary>
    private static void ConfigureMoneyDetail<T>(
        ModelBuilder modelBuilder,
        System.Linq.Expressions.Expression<Func<T, object?>> key)
        where T : class
    {
        modelBuilder.Entity<T>(b =>
        {
            b.HasKey(key);

            // "What has been paid against this bill?" — the read that turns a
            // pile of payments back into an outstanding balance.
            b.HasIndex(["OrgId", "MappingTransactionTypeCode", "MappingTransactionId"])
                .HasDatabaseName($"IX_{typeof(T).Name}_Mapping");

            b.Property("Amount").HasColumnType("decimal(18,2)");
            b.Property("AmountBase").HasColumnType("decimal(18,2)");

            b.ToTable(t =>
            {
                t.HasCheckConstraint(
                    $"chk_{typeof(T).Name.ToLowerInvariant()}_amount_positive",
                    "\"Amount\" > 0 AND \"AmountBase\" > 0");

                // Half a mapping traces to nothing. An id with no type cannot be
                // resolved; a type with no id names every document at once.
                t.HasCheckConstraint(
                    $"chk_{typeof(T).Name.ToLowerInvariant()}_mapping_paired",
                    "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
            });
        });
    }
}
