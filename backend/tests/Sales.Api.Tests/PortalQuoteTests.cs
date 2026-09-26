using Microsoft.EntityFrameworkCore;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// A contact answers a quote in the client portal (TK-96): once, only a posted
/// quote still valid, and only their own — another contact's quote, or a draft,
/// is not found.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalQuoteTests
{
    private const long Contact = 42;
    private const long Stranger = 43;

    private static readonly DateOnly Today = new(2026, 9, 26);

    private readonly PostgresFixture _pg;

    public PortalQuoteTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_posted_quote_is_accepted_once_and_the_answer_is_recorded()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long quote = await h.QuoteAsync(Contact, DocumentStatus.Posted, validUntil: Today.AddDays(10));

        Assert.Equal(PortalQuoteOutcome.Ok,
            await h.Portal.RespondAsync(Contact, quote, QuoteResponse.Accepted, "  Priya  ", "Please deliver by Friday", default));
        Assert.Equal(PortalQuoteOutcome.AlreadyAnswered,
            await h.Portal.RespondAsync(Contact, quote, QuoteResponse.Rejected, "Priya", null, default));

        Quote row = await h.Db.Quotes.AsNoTracking().SingleAsync(q => q.QuoteId == quote);
        Assert.Equal(QuoteResponse.Accepted, row.CustomerResponse);
        Assert.Equal("Priya", row.RespondedByName);
        Assert.Equal("Please deliver by Friday", row.ResponseNote);
        Assert.NotNull(row.RespondedAt);

        PortalQuoteItem listed = Assert.Single(await h.Portal.ListAsync(Contact, default));
        Assert.Equal("Accepted", listed.CustomerResponse);
        Assert.False(listed.CanRespond);
    }

    [SkippableFact]
    public async Task A_quote_past_its_validity_is_refused()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long quote = await h.QuoteAsync(Contact, DocumentStatus.Posted, validUntil: Today.AddDays(-1));

        Assert.Equal(PortalQuoteOutcome.Lapsed,
            await h.Portal.RespondAsync(Contact, quote, QuoteResponse.Accepted, "Priya", null, default));
        Assert.False(Assert.Single(await h.Portal.ListAsync(Contact, default)).CanRespond);
    }

    [SkippableFact]
    public async Task Another_contacts_quote_and_a_draft_are_not_found_and_not_listed()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long theirs = await h.QuoteAsync(Stranger, DocumentStatus.Posted, validUntil: Today.AddDays(10));
        long draft = await h.QuoteAsync(Contact, DocumentStatus.Draft, validUntil: Today.AddDays(10));

        Assert.Equal(PortalQuoteOutcome.NotFound,
            await h.Portal.RespondAsync(Contact, theirs, QuoteResponse.Accepted, "Priya", null, default));
        Assert.Equal(PortalQuoteOutcome.NotFound,
            await h.Portal.RespondAsync(Contact, draft, QuoteResponse.Accepted, "Priya", null, default));
        Assert.Empty(await h.Portal.ListAsync(Contact, default));

        Quote untouched = await h.Db.Quotes.AsNoTracking().SingleAsync(q => q.QuoteId == theirs);
        Assert.Equal(QuoteResponse.None, untouched.CustomerResponse);
    }

    [SkippableFact]
    public async Task A_quote_already_turned_into_an_order_cannot_be_answered()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long quote = await h.QuoteAsync(Contact, DocumentStatus.Posted, validUntil: Today.AddDays(10));

        h.Db.SalesOrders.Add(new SalesOrder
        {
            OrgId = h.OrgId,
            TransactionTypeCode = "SOR",
            DocumentNo = "SO/26/0001",
            DocumentDate = Today,
            ContactId = Contact,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            QuoteId = quote,
        });
        await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();

        Assert.Equal(PortalQuoteOutcome.Converted,
            await h.Portal.RespondAsync(Contact, quote, QuoteResponse.Rejected, "Priya", null, default));
        Assert.False(Assert.Single(await h.Portal.ListAsync(Contact, default)).CanRespond);
    }

    private sealed record Harness(SalesDbContext Db, Guid OrgId, PortalQuoteService Portal)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Skip.If(pg.SkipReason is not null, pg.SkipReason ?? string.Empty);

            Guid orgId = Guid.NewGuid();
            SalesDbContext db = pg.CreateContext(Guid.NewGuid(), orgId);
            await Task.CompletedTask;
            return new Harness(db, orgId, new PortalQuoteService(db, new FixedClock()));
        }

        public async Task<long> QuoteAsync(long contactId, DocumentStatus status, DateOnly validUntil)
        {
            bool posted = status == DocumentStatus.Posted;
            var quote = new Quote
            {
                OrgId = OrgId,
                TransactionTypeCode = "QTE",
                DocumentNo = $"QT/26/{Random.Shared.Next(10000, 99999)}",
                DocumentDate = Today.AddDays(-20),
                ValidUntil = validUntil,
                ContactId = contactId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 1_180m,
                TotalAmountBase = 1_180m,
                Status = status,
                PostedAt = posted ? DateTimeOffset.UtcNow : null,
                PostedBy = posted ? Guid.NewGuid() : null,
            };

            Db.Quotes.Add(quote);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return quote.QuoteId;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 26, 6, 0, 0, TimeSpan.Zero);
    }
}
