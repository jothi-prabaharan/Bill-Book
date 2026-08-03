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
    /// Spend, receive and transfer money — three transaction types in one table,
    /// discriminated by <c>TransactionTypeCode</c>.
    /// </summary>
    public DbSet<MoneyTransaction> MoneyTransactions => Set<MoneyTransaction>();

    public DbSet<MoneyTransactionDetail> MoneyTransactionDetails =>
        Set<MoneyTransactionDetail>();

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

        modelBuilder.Entity<MoneyTransaction>(b =>
        {
            b.HasKey(e => e.MoneyTransactionId);

            // Filtered, because a draft has no number yet. The uniqueness that
            // matters is over issued numbers, and the type is in the key because
            // each of the three has its own series — two series left with no
            // prefix would otherwise collide at 00001.
            b.HasIndex(e => new { e.OrgId, e.TransactionTypeCode, e.TransactionNo })
                .IsUnique()
                .HasFilter("\"TransactionNo\" IS NOT NULL")
                .HasDatabaseName("IX_MoneyTransactions_Number");

            b.HasIndex(e => new { e.OrgId, e.TransactionDate });

            // Reconciliation: one account over a period.
            b.HasIndex(e => new { e.OrgId, e.BankAccountId, e.TransactionDate })
                .HasDatabaseName("IX_MoneyTransactions_Account");

            b.HasIndex(e => new { e.OrgId, e.ContactId });

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,8)");

            // Restrict, both sides: a bank account with money moved through it
            // cannot be deleted out from under its own history.
            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.ToBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // The number is taken at post. Both halves matter: a draft
                // holding one has consumed a number it may never use, and a
                // posted document without one is a ledger row nobody can cite.
                table.HasCheckConstraint(
                    "chk_money_number_on_post",
                    "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) "
                        + "OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "chk_money_posted_stamp",
                    "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");

                table.HasCheckConstraint(
                    "chk_money_void_stamp",
                    "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");

                // Zero moves no money, and a negative amount would mean the
                // direction lives on the number rather than on the document type.
                table.HasCheckConstraint("chk_money_amount_positive", "\"Amount\" > 0");

                table.HasCheckConstraint("chk_money_rate_positive", "\"ExchangeRate\" > 0");

                // A transfer is the one money document with no counterparty:
                // both sides are the organization's own accounts. Everything else
                // has a counterparty and no destination account.
                table.HasCheckConstraint(
                    "chk_money_transfer_shape",
                    "(\"TransactionTypeCode\" = 'TRM' "
                        + "AND \"ToBankAccountId\" IS NOT NULL AND \"ContactId\" IS NULL) "
                        + "OR (\"TransactionTypeCode\" <> 'TRM' AND \"ToBankAccountId\" IS NULL)");

                // Moving money to the account it came from posts two legs that
                // cancel, and reconciles as a transaction that never happened.
                table.HasCheckConstraint(
                    "chk_money_transfer_distinct",
                    "\"ToBankAccountId\" IS NULL OR \"ToBankAccountId\" <> \"BankAccountId\"");

                // Only the three this table is for. A fourth code here would be a
                // document with no posting path behind it.
                table.HasCheckConstraint(
                    "chk_money_transaction_type",
                    "\"TransactionTypeCode\" IN ('SPM', 'RCM', 'TRM')");
            });
        });

        modelBuilder.Entity<MoneyTransactionDetail>(b =>
        {
            b.HasKey(e => e.MoneyTransactionDetailId);

            b.HasIndex(e => new { e.MoneyTransactionId, e.LineNumber }).IsUnique();

            // "What has been paid against this bill?" — the read that turns a
            // pile of payments back into an outstanding balance.
            b.HasIndex(e => new
            {
                e.OrgId,
                e.MappingTransactionTypeCode,
                e.MappingTransactionId,
            }).HasDatabaseName("IX_MoneyTransactionDetails_Mapping");

            b.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            b.Property(e => e.AmountBase).HasColumnType("decimal(18,2)");

            // Cascade: a draft's lines have no meaning without their header, and
            // a posted document is voided rather than deleted.
            b.HasOne<MoneyTransaction>()
                .WithMany()
                .HasForeignKey(e => e.MoneyTransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "chk_money_detail_amount_positive",
                    "\"Amount\" > 0 AND \"AmountBase\" > 0");

                // Half a mapping traces to nothing. Either the line settles a
                // named document or it settles none — an id with no type cannot
                // be resolved, and a type with no id names every document at once.
                table.HasCheckConstraint(
                    "chk_money_detail_mapping_paired",
                    "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
            });
        });

        base.OnModelCreating(modelBuilder);
    }
}
