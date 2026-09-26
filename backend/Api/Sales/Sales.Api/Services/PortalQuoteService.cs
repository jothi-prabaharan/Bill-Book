using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;

namespace Sales.Api.Services;

/// <summary>A quote as a contact sees it in the portal (TK-96).</summary>
public sealed class PortalQuoteItem
{
    public long QuoteId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly ValidUntil { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>None, Accepted or Rejected.</summary>
    public string CustomerResponse { get; set; } = string.Empty;

    public DateTimeOffset? RespondedAt { get; set; }

    public string? RespondedByName { get; set; }

    /// <summary>Whether the contact can still answer: unanswered, still valid, and not yet made into an order.</summary>
    public bool CanRespond { get; set; }
}

public enum PortalQuoteOutcome
{
    Ok = 0,
    NotFound = 1,
    Lapsed = 2,
    AlreadyAnswered = 3,
    Converted = 4,
}

/// <summary>
/// A contact's quotes in the client portal, and their answer to one (TK-96,
/// design "Client portal" → Quotes). Only posted quotes, only the token's
/// contact; another contact's quote, or a draft, is not found. A quote is
/// answered once, while it is still valid, and the answer is recorded rather
/// than acted on: turning an accepted quote into an order stays a staff decision.
/// </summary>
public sealed class PortalQuoteService
{
    private readonly SalesDbContext _db;
    private readonly TimeProvider _clock;

    public PortalQuoteService(SalesDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    public async Task<List<PortalQuoteItem>> ListAsync(long contactId, CancellationToken ct)
    {
        DateOnly today = Today;

        return await Posted(contactId)
            .OrderByDescending(q => q.DocumentDate)
            .ThenByDescending(q => q.QuoteId)
            .Select(q => new PortalQuoteItem
            {
                QuoteId = q.QuoteId,
                DocumentNo = q.DocumentNo,
                DocumentDate = q.DocumentDate,
                ValidUntil = q.ValidUntil,
                CurrencyCode = q.CurrencyCode,
                TotalAmount = q.TotalAmount,
                CustomerResponse = q.CustomerResponse.ToString(),
                RespondedAt = q.RespondedAt,
                RespondedByName = q.RespondedByName,
                CanRespond = q.CustomerResponse == QuoteResponse.None
                    && q.ValidUntil >= today
                    && !_db.SalesOrders.Any(o => o.QuoteId == q.QuoteId),
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Records the contact's answer. The write is guarded on the quote still
    /// being unanswered and valid, so two answers racing each other record one,
    /// and the row count says which.
    /// </summary>
    public async Task<PortalQuoteOutcome> RespondAsync(
        long contactId, long quoteId, QuoteResponse answer, string name, string? note, CancellationToken ct)
    {
        DateOnly today = Today;
        DateTimeOffset now = _clock.GetUtcNow();

        var quote = await Posted(contactId)
            .Where(q => q.QuoteId == quoteId)
            .Select(q => new { q.CustomerResponse, q.ValidUntil })
            .FirstOrDefaultAsync(ct);

        if (quote is null)
        {
            return PortalQuoteOutcome.NotFound;
        }

        if (quote.CustomerResponse != QuoteResponse.None)
        {
            return PortalQuoteOutcome.AlreadyAnswered;
        }

        if (quote.ValidUntil < today)
        {
            return PortalQuoteOutcome.Lapsed;
        }

        if (await _db.SalesOrders.AnyAsync(o => o.QuoteId == quoteId, ct))
        {
            return PortalQuoteOutcome.Converted;
        }

        string trimmedName = name.Trim();
        string? trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        int answered = await Posted(contactId)
            .Where(q => q.QuoteId == quoteId && q.CustomerResponse == QuoteResponse.None && q.ValidUntil >= today)
            .ExecuteUpdateAsync(q => q
                .SetProperty(x => x.CustomerResponse, answer)
                .SetProperty(x => x.RespondedAt, now)
                .SetProperty(x => x.RespondedByName, trimmedName)
                .SetProperty(x => x.ResponseNote, trimmedNote),
                ct);

        return answered == 1 ? PortalQuoteOutcome.Ok : PortalQuoteOutcome.AlreadyAnswered;
    }

    private IQueryable<Quote> Posted(long contactId) =>
        _db.Quotes.AsNoTracking().Where(q => q.ContactId == contactId && q.Status == DocumentStatus.Posted);
}
