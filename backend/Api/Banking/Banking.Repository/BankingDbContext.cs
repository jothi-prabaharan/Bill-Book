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

        base.OnModelCreating(modelBuilder);
    }
}
