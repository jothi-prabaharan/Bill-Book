using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository;

/// <summary>
/// The acc schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
///
/// Banking's tables live here too — banks, bank accounts, the three money
/// documents and the imported statements, all formerly the bnk schema. A money
/// document exists to move a balance in the ledger, and it could not be written
/// and posted in one transaction while the two sat in different contexts: the
/// bank account's GL account was provisioned over HTTP, and a payment that saved
/// but failed to post left the two halves disagreeing with nothing to roll back.
/// One context is what makes that a single transaction.
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

    public DbSet<FixedAssetCategory> FixedAssetCategories => Set<FixedAssetCategory>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<DepreciationSchedule> DepreciationSchedules => Set<DepreciationSchedule>();
    public DbSet<AssetTransaction> AssetTransactions => Set<AssetTransaction>();

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

    /// <summary>
    /// Statements as the bank produced them. Nothing here posts — a statement is
    /// the bank's account of movements already recorded, and reconciliation is
    /// the comparison of the two rather than a second set of entries.
    /// </summary>
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();

    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    /// <summary>How to read the file each account's bank produces. No two agree.</summary>
    public DbSet<StatementImportProfile> StatementImportProfiles => Set<StatementImportProfile>();

    public DbSet<TransactionRatio> TransactionRatios => Set<TransactionRatio>();

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

        modelBuilder.Entity<FixedAssetCategory>(b =>
        {
            b.HasKey(e => e.FixedAssetCategoryId);
            b.HasIndex(e => new { e.OrgId, e.CategoryName }).IsUnique();

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AssetAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccumulatedDepreciationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.DepreciationExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FixedAsset>(b =>
        {
            b.HasKey(e => e.FixedAssetId);
            b.HasIndex(e => new { e.OrgId, e.AssetCode }).IsUnique();

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");

            b.HasOne<FixedAssetCategory>()
                .WithMany()
                .HasForeignKey(e => e.FixedAssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DepreciationSchedule>(b =>
        {
            b.HasKey(e => e.DepreciationScheduleId);
            b.HasIndex(e => new { e.OrgId, e.FixedAssetId, e.ScheduleType }).IsUnique();

            b.Property(e => e.ScheduleType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DepreciationMethod).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Rate).HasColumnType("decimal(5,2)");
            b.Property(e => e.SalvageValue).HasColumnType("decimal(18,2)");

            b.HasOne<FixedAsset>()
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetTransaction>(b =>
        {
            b.HasKey(e => e.AssetTransactionId);
            b.HasIndex(e => new { e.OrgId, e.FixedAssetId, e.TransactionDate });

            b.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            b.HasOne<FixedAsset>()
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<DepreciationSchedule>()
                .WithMany()
                .HasForeignKey(e => e.DepreciationScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Journal>()
                .WithMany()
                .HasForeignKey(e => e.JournalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JournalLedger>(b =>

        {
            b.HasKey(e => e.LedgerId);

            // Reporting reads: a period, an account within a period, and the
            // rows behind one document.
            b.HasIndex(e => new { e.OrgId, e.LedgerDate });
            b.HasIndex(e => new { e.OrgId, e.AccountId, e.LedgerDate });
            b.HasIndex(e => new { e.OrgId, e.SubAccountId, e.LedgerDate });

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

        modelBuilder.Entity<BankStatement>(b =>
        {
            b.HasKey(e => e.BankStatementId);

            b.HasIndex(e => new { e.OrgId, e.BankAccountId, e.FromDate });

            b.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");
            b.Property(e => e.ClosingBalance).HasColumnType("decimal(18,2)");

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_statement_period", "\"ToDate\" >= \"FromDate\""));
        });

        modelBuilder.Entity<BankStatementLine>(b =>
        {
            b.HasKey(e => e.BankStatementLineId);

            // What makes re-importing an overlapping period add only what is new.
            // Per account rather than per statement, because the whole point is
            // that the same movement arrives in two different files.
            b.HasIndex(e => new { e.OrgId, e.BankAccountId, e.RowHash })
                .IsUnique()
                .HasDatabaseName("IX_BankStatementLines_Row");

            b.HasIndex(e => new { e.BankStatementId, e.LineNumber }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.BankAccountId, e.Status, e.TransactionDate });

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
            b.Property(e => e.WithdrawalAmount).HasColumnType("decimal(18,2)");
            b.Property(e => e.DepositAmount).HasColumnType("decimal(18,2)");
            b.Property(e => e.RunningBalance).HasColumnType("decimal(18,2)");

            b.HasOne<BankStatement>()
                .WithMany()
                .HasForeignKey(e => e.BankStatementId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table =>
            {
                // In xor out, the same rule the ledger holds. A line that is
                // both, or neither, is a row the import failed to understand and
                // should have refused rather than stored.
                table.HasCheckConstraint(
                    "chk_statement_line_exclusive",
                    "(\"WithdrawalAmount\" > 0 AND \"DepositAmount\" = 0) "
                        + "OR (\"DepositAmount\" > 0 AND \"WithdrawalAmount\" = 0)");

                table.HasCheckConstraint(
                    "chk_statement_line_non_negative",
                    "\"WithdrawalAmount\" >= 0 AND \"DepositAmount\" >= 0");

                // A match is a whole match or none of one. Half of it — a status
                // with no document, or a document with no status — is a line that
                // reads as reconciled against nothing.
                table.HasCheckConstraint(
                    "chk_statement_line_match",
                    "(\"Status\" = 'Matched' AND \"MatchedTransactionTypeCode\" IS NOT NULL "
                        + "AND \"MatchedTransactionId\" IS NOT NULL AND \"MatchedAt\" IS NOT NULL) "
                        + "OR (\"Status\" <> 'Matched' AND \"MatchedTransactionTypeCode\" IS NULL "
                        + "AND \"MatchedTransactionId\" IS NULL AND \"MatchedAt\" IS NULL)");

                // Setting a line aside is a decision, and a decision with no
                // reason is indistinguishable from a mistake six months later.
                table.HasCheckConstraint(
                    "chk_statement_line_ignored_reason",
                    "\"Status\" <> 'Ignored' OR \"Note\" IS NOT NULL");

                // Only the three money documents reconcile. Anything else naming
                // itself here is a bug in whatever wrote it.
                table.HasCheckConstraint(
                    "chk_statement_line_matched_type",
                    "\"MatchedTransactionTypeCode\" IS NULL "
                        + "OR \"MatchedTransactionTypeCode\" IN ('SPM', 'RCM', 'TRM')");
            });
        });

        modelBuilder.Entity<StatementImportProfile>(b =>
        {
            b.HasKey(e => e.StatementImportProfileId);

            // One mapping per account. A second would make "how is this file
            // read" a question with two answers.
            b.HasIndex(e => new { e.OrgId, e.BankAccountId })
                .IsUnique()
                .HasDatabaseName("IX_StatementImportProfiles_Account");

            b.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table => table.HasCheckConstraint(
                // Two columns or one signed column, and it has to be one of the
                // two — a profile naming neither cannot read an amount at all.
                "chk_import_profile_amount_shape",
                "(\"WithdrawalColumn\" IS NOT NULL AND \"DepositColumn\" IS NOT NULL "
                    + "AND \"AmountColumn\" IS NULL) "
                    + "OR (\"AmountColumn\" IS NOT NULL AND \"WithdrawalColumn\" IS NULL "
                    + "AND \"DepositColumn\" IS NULL)"));
        });

        modelBuilder.Entity<TransactionRatio>(b =>
        {
            b.HasKey(e => e.TransactionRatioId);
            b.HasIndex(e => new { e.OrgId, e.SourceTransactionTypeCode, e.SourceTransactionId });
            b.HasIndex(e => new { e.OrgId, e.TargetTransactionTypeCode, e.TargetTransactionId });
            
            b.ToTable(t => t.HasCheckConstraint(
                "chk_transactionratio_amount",
                "\"Amount\" > 0"
            ));
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
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
