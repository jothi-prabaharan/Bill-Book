using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository;

/// <summary>
/// The acc schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
/// </summary>
public class AccountingDbContext : TenantDbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<SubAccount> SubAccounts => Set<SubAccount>();

    public DbSet<TaxMaster> TaxMasters => Set<TaxMaster>();

    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();

    /// <summary>
    /// The general ledger. Every posting in the product lands here and nowhere
    /// else, whichever service described it.
    /// </summary>
    public DbSet<JournalLedger> JournalLedger => Set<JournalLedger>();

    /// <summary>
    /// Manual journals, and only manual journals. Every other document posts
    /// straight to <see cref="JournalLedger"/> under its own type and id.
    /// </summary>
    public DbSet<Journal> Journals => Set<Journal>();

    public DbSet<JournalDetail> JournalDetails => Set<JournalDetail>();

    /// <summary>
    /// Shared with every service that generates a code. Accounting owns the
    /// migration; the others map the same entity and exclude it from theirs.
    /// </summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("acc");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: true);

        modelBuilder.Entity<Account>(b =>
        {
            b.HasKey(e => e.AccountId);
            b.HasIndex(e => new { e.OrgId, e.AccountCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.AccountTypeId });

            // What makes bank-account provisioning idempotent: a retried call
            // finds the existing row instead of creating a second account. It
            // also enforces that the seeded control names stay unique.
            b.HasIndex(e => new { e.OrgId, e.AccountSystemName })
                .IsUnique()
                .HasFilter("\"AccountSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_Accounts_SystemName");

            // Filtered indexes for the pickers that scan these flags.
            b.HasIndex(["OrgId"], "IX_Accounts_Bank").HasFilter("\"IsBank\" = true");
            b.HasIndex(["OrgId"], "IX_Accounts_Sales").HasFilter("\"IsSales\" = true");
            b.HasIndex(["OrgId"], "IX_Accounts_Purchase").HasFilter("\"IsPurchase\" = true");

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubAccount>(b =>
        {
            b.HasKey(e => e.SubAccountId);

            // The component completes the key, so CGST/SGST/IGST can share a
            // parent account and a tax rate.
            b.HasIndex(e => new { e.AccountId, e.ReferenceType, e.ReferenceId, e.TaxComponent })
                .IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ReferenceType, e.ReferenceId });

            b.Property(e => e.ReferenceType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(10);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaxMaster>(b =>
        {
            b.HasKey(e => e.TaxMasterId);
            b.HasIndex(e => new { e.OrgId, e.TaxGroupId, e.EffectiveFrom });
            b.HasIndex(e => new { e.OrgId, e.EffectiveFrom, e.EffectiveTo });
            b.HasIndex(["OrgId"], "IX_TaxMasters_Sales").HasFilter("\"IsSales\" = true");
            b.HasIndex(["OrgId"], "IX_TaxMasters_Purchase").HasFilter("\"IsPurchase\" = true");

            foreach (string rate in new[] { "TotalRate", "CgstRate", "SgstRate", "IgstRate", "CessRate" })
            {
                b.Property(rate).HasColumnType("decimal(5,2)");
            }

            // The split invariant, enforced in the database so it cannot drift:
            // CGST equals SGST, the two sum to the total, and IGST is the total.
            b.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "chk_tax_split",
                    "\"CgstRate\" = \"SgstRate\" AND \"CgstRate\" + \"SgstRate\" = \"TotalRate\" "
                        + "AND \"IgstRate\" = \"TotalRate\"");

                // A rate usable on neither document is dead data.
                table.HasCheckConstraint(
                    "chk_tax_applicability",
                    "\"IsSales\" = true OR \"IsPurchase\" = true");

                table.HasCheckConstraint(
                    "chk_tax_effective_range",
                    "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
        });

        modelBuilder.Entity<PaymentTerm>(b =>
        {
            b.HasKey(e => e.PaymentTermId);
            b.HasIndex(e => new { e.OrgId, e.TermName }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.TermSystemName })
                .IsUnique()
                .HasFilter("\"TermSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_PaymentTerms_SystemName");

            // At most one default, enforced here rather than by whoever
            // remembers to clear the previous one.
            b.HasIndex(["OrgId"], "IX_PaymentTerms_Default")
                .IsUnique()
                .HasFilter("\"IsDefault\" = true");

            b.HasIndex(["OrgId"], "IX_PaymentTerms_Sales").HasFilter("\"IsSales\" = true");
            b.HasIndex(["OrgId"], "IX_PaymentTerms_Purchase").HasFilter("\"IsPurchase\" = true");
            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.TermName })
                .HasDatabaseName("IX_PaymentTerms_Order");

            b.Property(e => e.TermType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DiscountPercent).HasColumnType("decimal(5,2)");

            b.ToTable(table =>
            {
                // A term usable on neither document is dead data.
                table.HasCheckConstraint(
                    "chk_term_applicability",
                    "\"IsSales\" = true OR \"IsPurchase\" = true");

                // The day of month only means anything for DayOfNextMonth, and
                // that type cannot work without it.
                table.HasCheckConstraint(
                    "chk_term_day_of_month",
                    "(\"TermType\" = 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NOT NULL) "
                        + "OR (\"TermType\" <> 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NULL)");

                table.HasCheckConstraint(
                    "chk_term_due_on_receipt",
                    "\"TermType\" <> 'DueOnReceipt' OR \"DueDays\" = 0");

                // A discount window that outlives the due date can never be earned.
                table.HasCheckConstraint(
                    "chk_term_discount_window",
                    "\"TermType\" <> 'Net' OR \"DiscountDays\" <= \"DueDays\"");

                table.HasCheckConstraint(
                    "chk_term_discount_days",
                    "\"DiscountPercent\" = 0 OR \"DiscountDays\" > 0");
            });
        });

        modelBuilder.Entity<JournalLedger>(b =>
        {
            b.HasKey(e => e.LedgerId);

            // Reporting reads: a period, an account within a period, and the
            // rows behind one document.
            b.HasIndex(e => new { e.OrgId, e.LedgerDate });
            b.HasIndex(e => new { e.OrgId, e.AccountId, e.LedgerDate });

            // The replace key, and the document lookup, in one index. A posting
            // is identified by all four columns — the leftmost three are what
            // "show me this invoice's rows" asks for, so a separate shorter
            // index would duplicate this one's prefix and earn nothing.
            b.HasIndex(e => new
            {
                e.OrgId,
                e.TransactionTypeCode,
                e.TransactionId,
                e.TransactionDetailId,
                e.LedgerTypeId,
            }).HasDatabaseName("IX_JournalLedger_Posting");

            // Tracing a payment back to the document it settles.
            b.HasIndex(e => new
            {
                e.OrgId,
                e.MappingTransactionTypeCode,
                e.MappingTransactionId,
            }).HasDatabaseName("IX_JournalLedger_Mapping");

            b.HasIndex(e => new { e.OrgId, e.ContactId });
            b.HasIndex(e => new { e.OrgId, e.SubAccountId });

            foreach (string amount in new[]
            {
                "DebitAmount", "CreditAmount", "DebitAmountBase", "CreditAmountBase",
            })
            {
                b.Property(amount).HasColumnType("decimal(18,2)");
            }

            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,8)");
            b.Property(e => e.TaxExchangeRate).HasColumnType("decimal(18,8)");

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<SubAccount>()
                .WithMany()
                .HasForeignKey(e => e.SubAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // A leg is a debit xor a credit. Both, or neither, and no column
                // in the ledger can be summed without knowing which convention
                // its writer used.
                table.HasCheckConstraint(
                    "chk_ledger_exclusive",
                    "(\"DebitAmount\" = 0) <> (\"CreditAmount\" = 0)");

                table.HasCheckConstraint(
                    "chk_ledger_base_exclusive",
                    "\"DebitAmountBase\" = 0 OR \"CreditAmountBase\" = 0");

                // Never negative. A reversal is an offsetting entry, not a
                // debit written as a minus.
                table.HasCheckConstraint(
                    "chk_ledger_non_negative",
                    "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 "
                        + "AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
            });
        });

        modelBuilder.Entity<Journal>(b =>
        {
            b.HasKey(e => e.JournalId);

            // Filtered, because a draft has no number yet. The uniqueness that
            // matters is over issued numbers, and a series full of nulls would
            // otherwise allow only one draft per branch.
            b.HasIndex(e => new { e.OrgId, e.JournalNo })
                .IsUnique()
                .HasFilter("\"JournalNo\" IS NOT NULL")
                .HasDatabaseName("IX_Journals_Number");

            b.HasIndex(e => new { e.OrgId, e.JournalDate });
            b.HasIndex(e => new { e.OrgId, e.TransactionTypeCode, e.SourceId });

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,8)");

            // Both sides of a reversal, each pointing at the other. Restrict, so
            // neither half of a pair can be deleted out from under the other.
            b.HasOne<Journal>()
                .WithMany()
                .HasForeignKey(e => e.ReversesJournalId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Journal>()
                .WithMany()
                .HasForeignKey(e => e.ReversedByJournalId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // A number is taken at post. Both halves matter: a draft holding
                // a number has consumed one it may never use, and a posted entry
                // without one is a ledger row nobody can cite.
                table.HasCheckConstraint(
                    "chk_journal_number_on_post",
                    "(\"Status\" = 'Draft' AND \"JournalNo\" IS NULL) "
                        + "OR (\"Status\" <> 'Draft' AND \"JournalNo\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "chk_journal_posted_stamp",
                    "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");

                table.HasCheckConstraint(
                    "chk_journal_rate_positive",
                    "\"ExchangeRate\" > 0");

                // Nothing reverses itself. Without this a single row could be
                // its own reversal and net to zero against nothing.
                table.HasCheckConstraint(
                    "chk_journal_reversal_distinct",
                    "\"ReversesJournalId\" IS NULL OR \"ReversesJournalId\" <> \"JournalId\"");
            });
        });

        modelBuilder.Entity<JournalDetail>(b =>
        {
            b.HasKey(e => e.JournalDetailId);

            b.HasIndex(e => new { e.JournalId, e.LineNumber }).IsUnique();
            b.HasIndex(e => e.ReversesJournalDetailId);

            foreach (string amount in new[]
            {
                "DebitAmount", "CreditAmount", "DebitAmountBase", "CreditAmountBase",
            })
            {
                b.Property(amount).HasColumnType("decimal(18,2)");
            }

            // Cascade: a draft's lines have no meaning without their header, and
            // a posted journal is never deleted at all.
            b.HasOne<Journal>()
                .WithMany()
                .HasForeignKey(e => e.JournalId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<SubAccount>()
                .WithMany()
                .HasForeignKey(e => e.SubAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<JournalDetail>()
                .WithMany()
                .HasForeignKey(e => e.ReversesJournalDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<JournalDetail>()
                .WithMany()
                .HasForeignKey(e => e.ReversedByJournalDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // The same two the ledger carries. A journal line and the ledger
                // row it produces are the same assertion written twice, so they
                // are constrained the same way.
                table.HasCheckConstraint(
                    "chk_journal_detail_exclusive",
                    "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) "
                        + "OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");

                table.HasCheckConstraint(
                    "chk_journal_detail_non_negative",
                    "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 "
                        + "AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
            });
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
