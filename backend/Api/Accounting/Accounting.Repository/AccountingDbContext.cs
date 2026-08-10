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
    /// How far back the books are closed, per role. Accounting owns it because
    /// closing a period is an accounting act, and every other service reads it
    /// before posting.
    /// </summary>
    public DbSet<PeriodLock> PeriodLocks => Set<PeriodLock>();

    /// <summary>
    /// Where the branch's books begin. One row per branch, and Accounting drives
    /// it because an opening balance touches every subledger at once — the
    /// service that owns the ledger is the only one that can see whether they all
    /// tie.
    /// </summary>
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();

    public DbSet<OpeningBalanceLine> OpeningBalanceLines => Set<OpeningBalanceLine>();

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

            // Two discriminators complete the key, each for a master that needs
            // several sub-accounts under one parent: the component so CGST, SGST
            // and IGST can share a tax parent, and the purpose so a contact's
            // trade balance, prepayment and overpayment can share Accounts
            // Receivable. Without the second, all three of a contact's would key
            // identically and only the first would ever be written.
            b.HasIndex(e => new
            {
                e.AccountId,
                e.ReferenceType,
                e.ReferenceId,
                e.TaxComponent,
                e.Purpose,
            }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.ReferenceType, e.ReferenceId });

            // What a balance sheet reads to split trade balances from the
            // advances held inside the same control account.
            b.HasIndex(e => new { e.OrgId, e.Purpose })
                .HasDatabaseName("IX_SubAccounts_Purpose");

            b.Property(e => e.ReferenceType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(20);

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

        modelBuilder.Entity<PeriodLock>(b =>
        {
            b.HasKey(e => e.PeriodLockId);

            // One date per branch per role, enforced rather than merely intended
            // — two rows for one role would make "how far back is this user
            // locked" a question with two answers.
            b.HasIndex(e => new { e.OrgId, e.RoleId }).IsUnique();
        });

        modelBuilder.Entity<OpeningBalance>(b =>
        {
            b.HasKey(e => e.OpeningBalanceId);

            // One per branch, ever. A branch has one moment it started, and every
            // balance in the product is measured from it — a second row is a
            // second starting position, and nothing downstream could say which
            // one it was measured from.
            b.HasIndex(e => e.OrgId)
                .IsUnique()
                .HasDatabaseName("IX_OpeningBalances_Org");

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);

            b.ToTable(table =>
            {
                // The same pair the journal carries: a draft holding a number has
                // consumed one it may never use, and a finalized document without
                // one is a ledger row nobody can cite.
                table.HasCheckConstraint(
                    "chk_opening_number_on_finalize",
                    "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) "
                        + "OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "chk_opening_finalized_stamp",
                    "(\"Status\" = 'Draft') = (\"FinalizedAt\" IS NULL)");
            });
        });

        modelBuilder.Entity<OpeningBalanceLine>(b =>
        {
            b.HasKey(e => e.OpeningBalanceLineId);

            b.HasIndex(e => new { e.OpeningBalanceId, e.LineNumber }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ContactId });
            b.HasIndex(e => new { e.OrgId, e.ItemId });

            b.Property(e => e.LineType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DebitAmount).HasColumnType("decimal(18,2)");
            b.Property(e => e.CreditAmount).HasColumnType("decimal(18,2)");
            b.Property(e => e.Quantity).HasColumnType("decimal(18,6)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,6)");

            // Cascade: a draft's lines have no meaning without their header, and
            // a finalized document is never deleted at all.
            b.HasOne<OpeningBalance>()
                .WithMany()
                .HasForeignKey(e => e.OpeningBalanceId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // What each kind of line must name, and must not. Enforced here
                // rather than only in C# because the whole point of the line type
                // is that a receivable cannot be filed against Sales Revenue and
                // a stock line cannot carry a hand-keyed value.
                table.HasCheckConstraint(
                    "chk_opening_line_names_its_subject",
                    "(\"LineType\" = 'GlAccount' AND \"AccountId\" IS NOT NULL "
                        + "AND \"ContactId\" IS NULL AND \"ItemId\" IS NULL) "
                        + "OR (\"LineType\" IN ('ContactReceivable', 'ContactPayable') "
                        + "AND \"ContactId\" IS NOT NULL AND \"AccountId\" IS NULL "
                        + "AND \"ItemId\" IS NULL) "
                        + "OR (\"LineType\" = 'Item' AND \"ItemId\" IS NOT NULL "
                        + "AND \"AccountId\" IS NULL AND \"ContactId\" IS NULL)");

                // An item line's value is quantity times cost, computed and
                // posted by Inventory. An amount keyed here as well would be a
                // second figure free to disagree with the stock itself.
                table.HasCheckConstraint(
                    "chk_opening_line_item_shape",
                    "(\"LineType\" = 'Item' AND \"Quantity\" > 0 AND \"UnitCost\" >= 0 "
                        + "AND \"DebitAmount\" = 0 AND \"CreditAmount\" = 0) "
                        + "OR (\"LineType\" <> 'Item' AND \"Quantity\" IS NULL "
                        + "AND \"UnitCost\" IS NULL)");

                // Debit xor credit on everything that carries an amount, which is
                // every kind but Item.
                table.HasCheckConstraint(
                    "chk_opening_line_exclusive",
                    "\"LineType\" = 'Item' "
                        + "OR (\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) "
                        + "OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");

                table.HasCheckConstraint(
                    "chk_opening_line_non_negative",
                    "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0");
            });
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
