using System.Data;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Jewellery savings schemes (TK-141): definitions, enrolments, instalments,
/// redemption, refunds, and the daily lapse.
///
/// <b>Nothing here posts on its own.</b> Every rupee moves through a document
/// Accounting already has, so the ledger, numbering, period locks and audit are
/// theirs:
/// <list type="bullet">
/// <item>an instalment is a <b>receipt</b> (source 9, a customer prepayment) — it
/// credits the customer's prepayment advance under Accounts Payable;</item>
/// <item>redemption <b>allocates</b> those receipts to the invoice, as the
/// Allocate dialog does, and the bonus is a <b>system journal</b> (Dr Savings
/// Scheme Bonus / Cr the customer's receivable) allocated to it like a credit
/// note;</item>
/// <item>a refund is a <b>spend</b> (source 19, returning a customer advance),
/// with the receipts allocated to it so they no longer count as unspent.</item>
/// </list>
/// The accounting decisions themselves are in <see cref="SavingsSchemeRulesRepository"/>.
/// </summary>
public sealed class SavingsSchemeRepository
{
    private const int CustomerPrepaymentSource = 9;
    private const int CustomerAdvanceRefundSource = 19;
    private const int JournalLedgerSource = 12;
    private const int ControlLedgerType = 3;
    private const int ReminderLeadDays = 3;

    private readonly AccountingDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ReceiveMoneyRepository _receipts;
    private readonly SpendMoneyRepository _spends;
    private readonly JournalRepository _journals;
    private readonly AllocationRepository _allocations;
    private readonly TimeProvider _clock;

    public SavingsSchemeRepository(
        AccountingDbContext db,
        ITenantContext tenant,
        ReceiveMoneyRepository receipts,
        SpendMoneyRepository spends,
        JournalRepository journals,
        AllocationRepository allocations,
        TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _receipts = receipts;
        _spends = spends;
        _journals = journals;
        _allocations = allocations;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    // ---- Schemes -----------------------------------------------------------

    public async Task<List<SavingsSchemeView>> ListSchemesAsync(CancellationToken ct)
    {
        var rows = await _db.SavingsSchemes.AsNoTracking()
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Code)
            .Select(s => new
            {
                Scheme = s,
                Active = _db.SchemeEnrolments.Count(e => e.SavingsSchemeId == s.SavingsSchemeId
                    && (e.Status == EnrolmentStatus.Active || e.Status == EnrolmentStatus.Matured)),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new SavingsSchemeView
        {
            SavingsSchemeId = r.Scheme.SavingsSchemeId,
            Code = r.Scheme.Code,
            Name = r.Scheme.Name,
            InstalmentAmount = r.Scheme.InstalmentAmount,
            Months = r.Scheme.Months,
            BonusType = r.Scheme.BonusType.ToString(),
            BonusValue = r.Scheme.BonusValue,
            BonusAmount = SavingsSchemeRulesRepository.Bonus(
                r.Scheme.BonusType, r.Scheme.BonusValue, r.Scheme.InstalmentAmount, r.Scheme.Months),
            GraceDays = r.Scheme.GraceDays,
            IsActive = r.Scheme.IsActive,
            ActiveEnrolments = r.Active,
        })];
    }

    /// <summary>
    /// Creates or changes a scheme. A change reaches only those who join after
    /// it: every enrolment carries its own copy of the terms.
    /// </summary>
    public async Task<SavingsSchemeResult> SaveSchemeAsync(long? schemeId, SaveSavingsSchemeRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();

        if (request.BonusType == SchemeBonusType.Percent && request.BonusValue > 100m)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "A percentage bonus cannot exceed 100.");
        }

        if (await _db.SavingsSchemes.AnyAsync(s => s.Code == code && s.SavingsSchemeId != (schemeId ?? 0), ct))
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Conflict, Detail: "Another scheme already has that code.");
        }

        SavingsScheme? scheme = schemeId is long id
            ? await _db.SavingsSchemes.FirstOrDefaultAsync(s => s.SavingsSchemeId == id, ct)
            : new SavingsScheme();

        if (scheme is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.NotFound);
        }

        scheme.Code = code;
        scheme.Name = request.Name.Trim();
        scheme.InstalmentAmount = request.InstalmentAmount;
        scheme.Months = request.Months;
        scheme.BonusType = request.BonusType;
        scheme.BonusValue = request.BonusType == SchemeBonusType.None ? 0m : request.BonusValue;
        scheme.GraceDays = request.GraceDays;
        scheme.IsActive = request.IsActive;

        if (schemeId is null)
        {
            _db.SavingsSchemes.Add(scheme);
        }

        await _db.SaveChangesAsync(ct);
        return new SavingsSchemeResult(SavingsSchemeOutcome.Ok, scheme.SavingsSchemeId);
    }

    // ---- Enrolments --------------------------------------------------------

    public async Task<List<SchemeEnrolmentListItem>> ListEnrolmentsAsync(
        EnrolmentStatus? status, long? contactId, CancellationToken ct)
    {
        IQueryable<SchemeEnrolment> query = _db.SchemeEnrolments.AsNoTracking();
        if (status is EnrolmentStatus wanted)
        {
            query = query.Where(e => e.Status == wanted);
        }

        if (contactId is long contact)
        {
            query = query.Where(e => e.ContactId == contact);
        }

        return await Summarize(query.OrderByDescending(e => e.StartDate).ThenByDescending(e => e.SchemeEnrolmentId))
            .ToListAsync(ct);
    }

    public async Task<SchemeEnrolmentView?> GetEnrolmentAsync(long enrolmentId, CancellationToken ct)
    {
        SchemeEnrolment? e = await _db.SchemeEnrolments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchemeEnrolmentId == enrolmentId, ct);
        if (e is null)
        {
            return null;
        }

        SchemeEnrolmentListItem summary = await Summarize(_db.SchemeEnrolments.Where(x => x.SchemeEnrolmentId == enrolmentId))
            .FirstAsync(ct);

        List<SchemeInstalmentView> instalments = await _db.SchemeInstalments.AsNoTracking()
            .Where(i => i.SchemeEnrolmentId == enrolmentId)
            .OrderBy(i => i.InstalmentNumber)
            .Select(i => new SchemeInstalmentView
            {
                SchemeInstalmentId = i.SchemeInstalmentId,
                InstalmentNumber = i.InstalmentNumber,
                DueDate = i.DueDate,
                Amount = i.Amount,
                ReceiveMoneyId = i.ReceiveMoneyId,
                PaidOn = i.PaidOn,
            })
            .ToListAsync(ct);

        return new SchemeEnrolmentView
        {
            SchemeEnrolmentId = summary.SchemeEnrolmentId,
            EnrolmentNo = summary.EnrolmentNo,
            SavingsSchemeId = summary.SavingsSchemeId,
            SchemeName = summary.SchemeName,
            ContactId = summary.ContactId,
            StartDate = summary.StartDate,
            MaturityDate = summary.MaturityDate,
            InstalmentAmount = summary.InstalmentAmount,
            Months = summary.Months,
            InstalmentsPaid = summary.InstalmentsPaid,
            PaidPrincipal = summary.PaidPrincipal,
            BonusAmount = summary.BonusAmount,
            NextDueDate = summary.NextDueDate,
            Status = summary.Status,
            GraceDays = e.GraceDays,
            MaturedOn = e.MaturedOn,
            LapsedOn = e.LapsedOn,
            RedeemedInvoiceId = e.RedeemedInvoiceId,
            RedeemedOn = e.RedeemedOn,
            PrincipalRedeemed = e.PrincipalRedeemed,
            BonusRedeemed = e.BonusRedeemed,
            BonusJournalId = e.BonusJournalId,
            RefundSpendMoneyId = e.RefundSpendMoneyId,
            RefundedOn = e.RefundedOn,
            PrincipalAvailable = (await UnspentReceiptsAsync(enrolmentId, ct)).Sum(r => r.Remaining),
            Instalments = instalments,
        };
    }

    private IQueryable<SchemeEnrolmentListItem> Summarize(IQueryable<SchemeEnrolment> query) =>
        from e in query
        join s in _db.SavingsSchemes on e.SavingsSchemeId equals s.SavingsSchemeId
        select new SchemeEnrolmentListItem
        {
            SchemeEnrolmentId = e.SchemeEnrolmentId,
            EnrolmentNo = e.EnrolmentNo,
            SavingsSchemeId = e.SavingsSchemeId,
            SchemeName = s.Name,
            ContactId = e.ContactId,
            StartDate = e.StartDate,
            MaturityDate = e.MaturityDate,
            InstalmentAmount = e.InstalmentAmount,
            Months = e.Months,
            InstalmentsPaid = _db.SchemeInstalments.Count(i => i.SchemeEnrolmentId == e.SchemeEnrolmentId && i.ReceiveMoneyId != null),
            PaidPrincipal = e.PaidPrincipal,
            BonusAmount = e.BonusAmount,
            NextDueDate = _db.SchemeInstalments
                .Where(i => i.SchemeEnrolmentId == e.SchemeEnrolmentId && i.ReceiveMoneyId == null)
                .OrderBy(i => i.InstalmentNumber)
                .Select(i => (DateOnly?)i.DueDate)
                .FirstOrDefault(),
            Status = e.Status.ToString(),
        };

    /// <summary>
    /// Joins a customer to a scheme, copying its terms and laying out every
    /// instalment. The customer must already have an advance account, which is
    /// also how a contact id that names nobody in this branch is refused.
    /// </summary>
    public async Task<SavingsSchemeResult> EnrolAsync(EnrolRequest request, CancellationToken ct)
    {
        SavingsScheme? scheme = await _db.SavingsSchemes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SavingsSchemeId == request.SavingsSchemeId, ct);

        if (scheme is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.NotFound, Detail: "The scheme was not found.");
        }

        if (!scheme.IsActive)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "This scheme is closed to new members.");
        }

        if (await ContactSubAccountAsync(SystemAccount.AccountsPayable, request.ContactId, SubAccountPurpose.PrepaymentAdvance, ct) is null)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Invalid,
                Detail: "That customer has no advance account in this branch. Save them as a customer first.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        SchemeEnrolment enrolment = new()
        {
            SavingsSchemeId = scheme.SavingsSchemeId,
            ContactId = request.ContactId,
            StartDate = request.StartDate,
            MaturityDate = request.StartDate.AddMonths(scheme.Months),
            InstalmentAmount = scheme.InstalmentAmount,
            Months = scheme.Months,
            BonusAmount = SavingsSchemeRulesRepository.Bonus(scheme.BonusType, scheme.BonusValue, scheme.InstalmentAmount, scheme.Months),
            GraceDays = scheme.GraceDays,
            Status = EnrolmentStatus.Active,
        };

        _db.SchemeEnrolments.Add(enrolment);
        await _db.SaveChangesAsync(ct);

        enrolment.EnrolmentNo = $"{scheme.Code}-{enrolment.SchemeEnrolmentId:D5}";
        _db.SchemeInstalments.AddRange(SavingsSchemeRulesRepository.Schedule(request.StartDate, scheme.Months)
            .Select(s => new SchemeInstalment
            {
                SchemeEnrolmentId = enrolment.SchemeEnrolmentId,
                InstalmentNumber = s.Number,
                DueDate = s.Due,
                Amount = scheme.InstalmentAmount,
            }));

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SavingsSchemeResult(SavingsSchemeOutcome.Ok, enrolment.SchemeEnrolmentId);
    }

    /// <summary>
    /// Takes the next unpaid instalment as a posted receipt to the customer's
    /// prepayment advance. Paying the last one matures the enrolment. Two
    /// counters taking the same instalment at once cannot both succeed: the
    /// instalment's row version refuses the second.
    /// </summary>
    public async Task<SavingsSchemeResult> PayInstalmentAsync(long enrolmentId, PayInstalmentRequest request, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        SchemeEnrolment? enrolment = await _db.SchemeEnrolments
            .FirstOrDefaultAsync(e => e.SchemeEnrolmentId == enrolmentId, ct);
        if (enrolment is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.NotFound);
        }

        if (enrolment.Status != EnrolmentStatus.Active)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Conflict, Detail: $"This enrolment is {enrolment.Status.ToString().ToLowerInvariant()} and takes no more instalments.");
        }

        SchemeInstalment? next = await _db.SchemeInstalments
            .Where(i => i.SchemeEnrolmentId == enrolmentId && i.ReceiveMoneyId == null)
            .OrderBy(i => i.InstalmentNumber)
            .FirstOrDefaultAsync(ct);
        if (next is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Conflict, Detail: "Every instalment has been paid.");
        }

        MoneyDocumentResult created = await _receipts.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = request.PaymentDate,
            BankAccountId = request.BankAccountId,
            ContactId = enrolment.ContactId,
            Amount = next.Amount,
            PaymentMethod = request.PaymentMethod,
            ReferenceNo = request.ReferenceNo,
            Memo = $"Savings scheme {enrolment.EnrolmentNo}, instalment {next.InstalmentNumber} of {enrolment.Months}",
            Lines =
            [
                new SaveMoneyLineRequest
                {
                    LedgerSourceId = CustomerPrepaymentSource,
                    Amount = next.Amount,
                    LineMemo = $"{enrolment.EnrolmentNo} instalment {next.InstalmentNumber}",
                },
            ],
        }, ct);

        if (created.Outcome != MoneyDocumentOutcome.Ok)
        {
            return Refused(created.Detail, "The receipt was refused.");
        }

        MoneyDocumentResult posted = await _receipts.PostAsync(created.DocumentId, ct);
        if (posted.Outcome != MoneyDocumentOutcome.Ok)
        {
            return Refused(posted.Detail, "The receipt could not be posted.");
        }

        next.ReceiveMoneyId = created.DocumentId;
        next.PaidOn = request.PaymentDate;
        enrolment.PaidPrincipal += next.Amount;

        bool last = !await _db.SchemeInstalments.AnyAsync(
            i => i.SchemeEnrolmentId == enrolmentId && i.ReceiveMoneyId == null && i.SchemeInstalmentId != next.SchemeInstalmentId, ct);
        if (last)
        {
            enrolment.Status = EnrolmentStatus.Matured;
            enrolment.MaturedOn = request.PaymentDate;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Retry, Detail: "That instalment was being taken at the same time. Open the enrolment again.");
        }

        await tx.CommitAsync(ct);
        return new SavingsSchemeResult(SavingsSchemeOutcome.Ok, created.DocumentId);
    }

    /// <summary>
    /// Spends the enrolment on a posted invoice for the same customer: the
    /// bonus first (matured enrolments only), then what was paid, neither past
    /// what the invoice still owes. Runs serializable, because the allocations
    /// under it are.
    /// </summary>
    public async Task<SavingsSchemeResult> RedeemAsync(long enrolmentId, RedeemEnrolmentRequest request, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(IsolationLevel.Serializable, ct);

        SchemeEnrolment? enrolment = await _db.SchemeEnrolments
            .FirstOrDefaultAsync(e => e.SchemeEnrolmentId == enrolmentId, ct);
        if (enrolment is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.NotFound);
        }

        if (enrolment.Status is EnrolmentStatus.Redeemed or EnrolmentStatus.Refunded)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Conflict, Detail: $"This enrolment has already been {enrolment.Status.ToString().ToLowerInvariant()}.");
        }

        if (enrolment.Status == EnrolmentStatus.Active && !request.CloseEarly)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Conflict,
                Detail: "This enrolment has not matured. Redeeming it now closes it early and gives up the bonus.");
        }

        // Is the invoice this customer's, posted, and still owing?
        var legs = await _db.JournalLedger.AsNoTracking()
            .Where(l => l.TransactionTypeCode == "INV" && l.TransactionId == request.InvoiceId && l.LedgerTypeId == ControlLedgerType)
            .Select(l => new { l.ContactId, l.DebitAmountBase, l.CreditAmountBase })
            .ToListAsync(ct);

        if (legs.Count == 0)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "Only a posted invoice can be paid from a scheme.");
        }

        if (legs.Any(l => l.ContactId is long c && c != enrolment.ContactId))
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "That invoice is another customer's.");
        }

        decimal claimed = await _db.TransactionRatios
            .Where(t => t.TargetTransactionTypeCode == "INV" && t.TargetTransactionId == request.InvoiceId && !t.IsVoided)
            .SumAsync(t => t.Amount, ct);
        decimal outstanding = legs.Sum(l => l.DebitAmountBase - l.CreditAmountBase) - claimed;

        if (outstanding <= 0m)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "That invoice owes nothing.");
        }

        List<UnspentReceipt> receipts = await UnspentReceiptsAsync(enrolmentId, ct);
        decimal bonus = enrolment.Status == EnrolmentStatus.Matured ? enrolment.BonusAmount : 0m;
        (decimal bonusApplied, decimal principalApplied) =
            SavingsSchemeRulesRepository.Split(bonus, receipts.Sum(r => r.Remaining), outstanding);

        if (bonusApplied + principalApplied <= 0m)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "There is nothing left in this enrolment to spend.");
        }

        (Guid customerId, Guid orgId) = _tenant.Require();
        string memo = $"Savings scheme {enrolment.EnrolmentNo} on invoice {request.InvoiceId}";

        decimal left = principalApplied;
        foreach (UnspentReceipt receipt in receipts.Where(r => r.Remaining > 0m))
        {
            if (left <= 0m)
            {
                break;
            }

            decimal amount = Math.Min(left, receipt.Remaining);
            AllocationResult allocated = await _allocations.AllocateAsync(new AllocateTransactionRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                SourceTransactionTypeCode = "RCM",
                SourceTransactionId = receipt.ReceiveMoneyId,
                TargetTransactionTypeCode = "INV",
                TargetTransactionId = request.InvoiceId,
                Amount = amount,
                AllocationDate = request.RedemptionDate,
                Notes = memo,
            }, ct);

            if (allocated.Outcome != AllocationOutcome.Ok)
            {
                return AllocationRefused(allocated);
            }

            left -= amount;
        }

        long? journalId = null;
        if (bonusApplied > 0m)
        {
            long? bonusAccount = await _db.Accounts
                .Where(a => a.AccountSystemName == SystemAccountNames.Of(SystemAccount.SavingsSchemeBonus))
                .Select(a => (long?)a.AccountId)
                .FirstOrDefaultAsync(ct);
            SubAccount? receivable = await ContactSubAccountAsync(SystemAccount.AccountsReceivable, enrolment.ContactId, SubAccountPurpose.Primary, ct);

            if (bonusAccount is null || receivable is null)
            {
                return new SavingsSchemeResult(
                    SavingsSchemeOutcome.PostingRefused,
                    Detail: "The chart has no Savings Scheme Bonus account, or the customer has no receivable account. Retry the branch's seeding from the admin portal.");
            }

            SaveJournalResult journal = await _journals.PostSystemAsync(new SaveJournalRequest
            {
                JournalDate = request.RedemptionDate,
                Reference = $"SCHEME {enrolment.EnrolmentNo}",
                Memo = memo,
                Lines =
                [
                    new SaveJournalLineRequest { AccountId = bonusAccount.Value, DebitAmount = bonusApplied, LineMemo = "Savings scheme bonus" },
                    new SaveJournalLineRequest { AccountId = receivable.AccountId, SubAccountId = receivable.SubAccountId, CreditAmount = bonusApplied, LineMemo = "Savings scheme bonus" },
                ],
            }, JournalLedgerSource, ct);

            if (journal.Outcome != SaveJournalOutcome.Ok)
            {
                return Refused(journal.Detail, "The bonus journal was refused.");
            }

            AllocationResult allocated = await _allocations.AllocateAsync(new AllocateTransactionRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                SourceTransactionTypeCode = "JRN",
                SourceTransactionId = journal.JournalId,
                TargetTransactionTypeCode = "INV",
                TargetTransactionId = request.InvoiceId,
                Amount = bonusApplied,
                AllocationDate = request.RedemptionDate,
                Notes = memo,
            }, ct);

            if (allocated.Outcome != AllocationOutcome.Ok)
            {
                return AllocationRefused(allocated);
            }

            journalId = journal.JournalId;
        }

        enrolment.Status = EnrolmentStatus.Redeemed;
        enrolment.RedeemedInvoiceId = request.InvoiceId;
        enrolment.RedeemedOn = request.RedemptionDate;
        enrolment.PrincipalRedeemed = principalApplied;
        enrolment.BonusRedeemed = bonusApplied;
        enrolment.BonusJournalId = journalId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SavingsSchemeResult(SavingsSchemeOutcome.Ok, enrolmentId);
    }

    /// <summary>
    /// Gives back what was paid and not yet spent, never the bonus. The receipts
    /// are allocated to the refund so they stop counting as unspent.
    /// </summary>
    public async Task<SavingsSchemeResult> RefundAsync(long enrolmentId, RefundEnrolmentRequest request, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(IsolationLevel.Serializable, ct);

        SchemeEnrolment? enrolment = await _db.SchemeEnrolments
            .FirstOrDefaultAsync(e => e.SchemeEnrolmentId == enrolmentId, ct);
        if (enrolment is null)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.NotFound);
        }

        if (enrolment.Status is EnrolmentStatus.Redeemed or EnrolmentStatus.Refunded)
        {
            return new SavingsSchemeResult(
                SavingsSchemeOutcome.Conflict, Detail: $"This enrolment has already been {enrolment.Status.ToString().ToLowerInvariant()}.");
        }

        List<UnspentReceipt> receipts = await UnspentReceiptsAsync(enrolmentId, ct);
        decimal refundable = receipts.Sum(r => r.Remaining);
        if (refundable <= 0m)
        {
            return new SavingsSchemeResult(SavingsSchemeOutcome.Invalid, Detail: "Nothing has been paid in that is still unspent.");
        }

        MoneyDocumentResult created = await _spends.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = request.RefundDate,
            BankAccountId = request.BankAccountId,
            ContactId = enrolment.ContactId,
            Amount = refundable,
            PaymentMethod = request.PaymentMethod,
            ReferenceNo = request.ReferenceNo,
            Memo = $"Savings scheme {enrolment.EnrolmentNo} refunded",
            Lines =
            [
                new SaveMoneyLineRequest
                {
                    LedgerSourceId = CustomerAdvanceRefundSource,
                    Amount = refundable,
                    LineMemo = $"{enrolment.EnrolmentNo} refund",
                },
            ],
        }, ct);

        if (created.Outcome != MoneyDocumentOutcome.Ok)
        {
            return Refused(created.Detail, "The refund was refused.");
        }

        MoneyDocumentResult posted = await _spends.PostAsync(created.DocumentId, ct);
        if (posted.Outcome != MoneyDocumentOutcome.Ok)
        {
            // An approval on spend money cannot be waited out inside one request;
            // the refund is undone and the reason given.
            return Refused(posted.Detail, "The refund could not be posted. If spend money needs approval in this branch, refund it through Spend money instead.");
        }

        (Guid customerId, Guid orgId) = _tenant.Require();
        foreach (UnspentReceipt receipt in receipts.Where(r => r.Remaining > 0m))
        {
            AllocationResult allocated = await _allocations.AllocateAsync(new AllocateTransactionRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                SourceTransactionTypeCode = "RCM",
                SourceTransactionId = receipt.ReceiveMoneyId,
                TargetTransactionTypeCode = "SPM",
                TargetTransactionId = created.DocumentId,
                Amount = receipt.Remaining,
                AllocationDate = request.RefundDate,
                Notes = $"Savings scheme {enrolment.EnrolmentNo} refunded",
            }, ct);

            if (allocated.Outcome != AllocationOutcome.Ok)
            {
                return AllocationRefused(allocated);
            }
        }

        enrolment.Status = EnrolmentStatus.Refunded;
        enrolment.RefundSpendMoneyId = created.DocumentId;
        enrolment.RefundedOn = request.RefundDate;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SavingsSchemeResult(SavingsSchemeOutcome.Ok, created.DocumentId);
    }

    // ---- Lookups for the page ---------------------------------------------

    /// <summary>The branch's active bank and cash accounts in its own currency, so the counter can choose without banking rights.</summary>
    public async Task<List<SchemeBankAccountOption>> BankAccountsAsync(CancellationToken ct)
    {
        string? currency = await _db.BankAccounts.AsNoTracking()
            .Where(b => b.IsActive && b.LedgerAccountId != null)
            .GroupBy(b => b.CurrencyCode)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        return await _db.BankAccounts.AsNoTracking()
            .Where(b => b.IsActive && b.LedgerAccountId != null && b.CurrencyCode == currency)
            .OrderByDescending(b => b.IsDefault).ThenBy(b => b.DisplayOrder).ThenBy(b => b.AccountName)
            .Select(b => new SchemeBankAccountOption { BankAccountId = b.BankAccountId, AccountName = b.AccountName, IsDefault = b.IsDefault })
            .ToListAsync(ct);
    }

    /// <summary>The enrolled customer's posted invoices that still owe something, oldest first.</summary>
    public async Task<List<SchemeInvoiceOption>> OpenInvoicesAsync(long enrolmentId, CancellationToken ct)
    {
        long? contactId = await _db.SchemeEnrolments.AsNoTracking()
            .Where(e => e.SchemeEnrolmentId == enrolmentId)
            .Select(e => (long?)e.ContactId)
            .FirstOrDefaultAsync(ct);

        if (contactId is null)
        {
            return [];
        }

        var rows = await _db.JournalLedger.AsNoTracking()
            .Where(l => l.TransactionTypeCode == "INV" && l.LedgerTypeId == ControlLedgerType && l.ContactId == contactId)
            .GroupBy(l => l.TransactionId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                DocumentNo = g.Max(l => l.DocumentNo),
                Date = g.Min(l => l.LedgerDate),
                Net = g.Sum(l => l.DebitAmountBase) - g.Sum(l => l.CreditAmountBase),
                Claimed = _db.TransactionRatios
                    .Where(t => t.TargetTransactionTypeCode == "INV" && t.TargetTransactionId == g.Key && !t.IsVoided)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        return [.. rows
            .Where(r => r.Net - r.Claimed > 0m)
            .OrderBy(r => r.Date)
            .Select(r => new SchemeInvoiceOption { InvoiceId = r.InvoiceId, DocumentNo = r.DocumentNo, DocumentDate = r.Date, Outstanding = r.Net - r.Claimed })];
    }

    // ---- The daily run -----------------------------------------------------

    /// <summary>
    /// Lapses every active enrolment whose earliest unpaid instalment is past
    /// its grace days, and lists the instalments to remind customers of: due
    /// within <see cref="ReminderLeadDays"/> days, or late but not yet lapsed.
    /// </summary>
    public async Task<RunSavingsSchemesResponse> RunAsync(DateOnly? asOf, CancellationToken ct)
    {
        DateOnly today = asOf ?? Today;
        RunSavingsSchemesResponse response = new();

        var open = await (
            from e in _db.SchemeEnrolments
            join s in _db.SavingsSchemes on e.SavingsSchemeId equals s.SavingsSchemeId
            where e.Status == EnrolmentStatus.Active
            select new
            {
                Enrolment = e,
                s.Name,
                Next = _db.SchemeInstalments
                    .Where(i => i.SchemeEnrolmentId == e.SchemeEnrolmentId && i.ReceiveMoneyId == null)
                    .OrderBy(i => i.InstalmentNumber)
                    .FirstOrDefault(),
            }).ToListAsync(ct);

        foreach (var row in open)
        {
            if (row.Next is null)
            {
                continue;
            }

            if (SavingsSchemeRulesRepository.IsLapsed(row.Next.DueDate, row.Enrolment.GraceDays, today))
            {
                row.Enrolment.Status = EnrolmentStatus.Lapsed;
                row.Enrolment.LapsedOn = today;
                response.Lapsed++;
                continue;
            }

            if (row.Next.DueDate <= today.AddDays(ReminderLeadDays))
            {
                response.Reminders.Add(new SchemeReminder
                {
                    SchemeInstalmentId = row.Next.SchemeInstalmentId,
                    ContactId = row.Enrolment.ContactId,
                    EnrolmentNo = row.Enrolment.EnrolmentNo,
                    SchemeName = row.Name,
                    InstalmentNumber = row.Next.InstalmentNumber,
                    Amount = row.Next.Amount,
                    DueDate = row.Next.DueDate,
                    Kind = row.Next.DueDate < today ? "Overdue" : "Due",
                });
            }
        }

        if (response.Lapsed > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return response;
    }

    // ---- Helpers -----------------------------------------------------------

    private sealed record UnspentReceipt(long ReceiveMoneyId, decimal Remaining);

    /// <summary>Each paid instalment's receipt, less whatever has already been allocated from it.</summary>
    private async Task<List<UnspentReceipt>> UnspentReceiptsAsync(long enrolmentId, CancellationToken ct)
    {
        var rows = await (
            from i in _db.SchemeInstalments
            join r in _db.ReceiveMoney on i.ReceiveMoneyId equals r.ReceiveMoneyId
            where i.SchemeEnrolmentId == enrolmentId && r.Status == MoneyDocumentStatus.Posted
            orderby i.InstalmentNumber
            select new
            {
                r.ReceiveMoneyId,
                r.Amount,
                Claimed = _db.TransactionRatios
                    .Where(t => t.SourceTransactionTypeCode == "RCM" && t.SourceTransactionId == r.ReceiveMoneyId && !t.IsVoided)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
            }).ToListAsync(ct);

        return [.. rows.Select(r => new UnspentReceipt(r.ReceiveMoneyId, Math.Max(0m, r.Amount - r.Claimed)))];
    }

    private Task<SubAccount?> ContactSubAccountAsync(
        SystemAccount parent, long contactId, SubAccountPurpose purpose, CancellationToken ct)
    {
        string name = SystemAccountNames.Of(parent);
        return (
            from s in _db.SubAccounts
            join a in _db.Accounts on s.AccountId equals a.AccountId
            where a.AccountSystemName == name
                && s.ReferenceType == SubAccountReferenceType.Contact
                && s.ReferenceId == contactId
                && s.Purpose == purpose
                && s.TaxComponent == TaxComponent.None
            select s).FirstOrDefaultAsync(ct);
    }

    private static SavingsSchemeResult Refused(string? detail, string fallback) =>
        new(SavingsSchemeOutcome.PostingRefused, Detail: detail ?? fallback);

    private static SavingsSchemeResult AllocationRefused(AllocationResult result) =>
        new(result.Outcome == AllocationOutcome.Retry ? SavingsSchemeOutcome.Retry : SavingsSchemeOutcome.PostingRefused,
            Detail: result.Message ?? "The allocation was refused.");
}
