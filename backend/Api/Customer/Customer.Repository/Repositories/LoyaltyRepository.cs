using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Customer.Repository.Repositories;

/// <summary>
/// Loyalty points (TK-166): what a posted bill earns its contact, taken back
/// when the bill is voided, and a contact's balance and history.
///
/// <b>Once per document.</b> Earning for an invoice that has already earned
/// changes nothing and says so, as does reversing one already reversed; a
/// unique index on (kind, source, source id) is the backstop for two calls
/// at the same instant. Sales computes the points from the branch's rate and
/// calls here after the bill posts, over the internal API (rule 8).
/// </summary>
public sealed class LoyaltyRepository
{
    /// <summary>How much history a contact's page shows.</summary>
    public const int HistoryLimit = 50;

    private readonly CustomerDbContext _db;
    private readonly TimeProvider _clock;

    public LoyaltyRepository(CustomerDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    /// <summary>Records what an invoice earned its contact, once.</summary>
    public async Task<LoyaltyPostingResult> EarnAsync(EarnLoyaltyRequest request, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        LoyaltyTransaction? earned = await FindAsync(LoyaltyTransactionKind.Earned, request.InvoiceId, ct);
        if (earned is not null)
        {
            LoyaltyAccount owner = await _db.LoyaltyAccounts.AsNoTracking().FirstAsync(a => a.LoyaltyAccountId == earned.LoyaltyAccountId, ct);
            return new LoyaltyPostingResult { ContactId = owner.ContactId, Points = earned.Points, Balance = owner.PointsBalance, AlreadyRecorded = true };
        }

        long accountId = await AccountIdForAsync(request.ContactId, ct);
        int balance = await MoveBalanceAsync(accountId, request.Points, ct);
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = accountId,
            Kind = LoyaltyTransactionKind.Earned,
            Points = request.Points,
            Source = LoyaltySource.Invoice,
            SourceId = request.InvoiceId,
            DocumentNo = Trimmed(request.DocumentNo),
            OccurredAt = Now,
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new LoyaltyPostingResult { ContactId = request.ContactId, Points = request.Points, Balance = balance };
    }

    /// <summary>
    /// Takes back what a voided invoice earned, once. Null when it earned
    /// nothing, which is not an error: the branch may have had no rate, or the
    /// bill no contact.
    /// </summary>
    public async Task<LoyaltyPostingResult?> ReverseAsync(long invoiceId, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        LoyaltyTransaction? earned = await FindAsync(LoyaltyTransactionKind.Earned, invoiceId, ct);
        if (earned is null)
        {
            return null;
        }

        LoyaltyAccount account = await _db.LoyaltyAccounts.AsNoTracking().FirstAsync(a => a.LoyaltyAccountId == earned.LoyaltyAccountId, ct);
        LoyaltyTransaction? reversed = await FindAsync(LoyaltyTransactionKind.Reversed, invoiceId, ct);
        if (reversed is not null)
        {
            return new LoyaltyPostingResult { ContactId = account.ContactId, Points = reversed.Points, Balance = account.PointsBalance, AlreadyRecorded = true };
        }

        int balance = await MoveBalanceAsync(account.LoyaltyAccountId, -earned.Points, ct);
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = account.LoyaltyAccountId,
            Kind = LoyaltyTransactionKind.Reversed,
            Points = -earned.Points,
            Source = LoyaltySource.Invoice,
            SourceId = invoiceId,
            DocumentNo = earned.DocumentNo,
            OccurredAt = Now,
            Note = "The bill was voided.",
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new LoyaltyPostingResult { ContactId = account.ContactId, Points = -earned.Points, Balance = balance };
    }

    /// <summary>A contact's balance and latest history, newest first. Zero and empty for a contact who never earned.</summary>
    public async Task<LoyaltyAccountView> GetAsync(long contactId, CancellationToken ct)
    {
        LoyaltyAccount? account = await _db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ContactId == contactId, ct);
        if (account is null)
        {
            return new LoyaltyAccountView { ContactId = contactId };
        }

        return new LoyaltyAccountView
        {
            ContactId = contactId,
            PointsBalance = account.PointsBalance,
            Transactions = await _db.LoyaltyTransactions
                .AsNoTracking()
                .Where(t => t.LoyaltyAccountId == account.LoyaltyAccountId)
                .OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.LoyaltyTransactionId)
                .Take(HistoryLimit)
                .Select(t => new LoyaltyTransactionView
                {
                    LoyaltyTransactionId = t.LoyaltyTransactionId,
                    Kind = t.Kind,
                    Points = t.Points,
                    Source = t.Source,
                    SourceId = t.SourceId,
                    DocumentNo = t.DocumentNo,
                    OccurredAt = t.OccurredAt,
                    Note = t.Note,
                })
                .ToListAsync(ct),
        };
    }

    private Task<LoyaltyTransaction?> FindAsync(LoyaltyTransactionKind kind, long invoiceId, CancellationToken ct) =>
        _db.LoyaltyTransactions.FirstOrDefaultAsync(
            t => t.Kind == kind && t.Source == LoyaltySource.Invoice && t.SourceId == invoiceId, ct);

    /// <summary>The contact's account, made on their first points.</summary>
    private async Task<long> AccountIdForAsync(long contactId, CancellationToken ct)
    {
        long id = await _db.LoyaltyAccounts.Where(a => a.ContactId == contactId).Select(a => a.LoyaltyAccountId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return id;
        }

        LoyaltyAccount account = new() { ContactId = contactId };
        _db.LoyaltyAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account.LoyaltyAccountId;
    }

    /// <summary>
    /// Moves a balance by a signed number of points in one statement, so two
    /// bills for one contact at the same moment both count, and answers the
    /// balance after it.
    /// </summary>
    private async Task<int> MoveBalanceAsync(long accountId, int points, CancellationToken ct)
    {
        await _db.LoyaltyAccounts
            .Where(a => a.LoyaltyAccountId == accountId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.PointsBalance, a => a.PointsBalance + points), ct);
        return await _db.LoyaltyAccounts.Where(a => a.LoyaltyAccountId == accountId).Select(a => a.PointsBalance).FirstAsync(ct);
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
