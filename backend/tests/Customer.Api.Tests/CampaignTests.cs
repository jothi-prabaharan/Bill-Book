using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using Customer.Repository.Sanitization;
using Customer.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

public sealed class CampaignTokenAndSanitizationTests
{
    private readonly IUnsubscribeTokenSigner _signer;

    public CampaignTokenAndSanitizationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Unsubscribe:SigningKey"] = "test-secret-key-for-campaign-unsubscribe-2026-xyz"
            })
            .Build();

        _signer = new UnsubscribeTokenSigner(config);
    }

    [Fact]
    public void UnsubscribeToken_ValidPayload_SignsAndValidatesSuccessfully()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string email = "Recipient@example.com";

        string token = _signer.GenerateToken(customerId, orgId, email);

        Assert.NotEmpty(token);
        Assert.Contains('.', token);

        bool isValid = _signer.TryValidateToken(token, out UnsubscribeTokenPayload? payload);

        Assert.True(isValid);
        Assert.NotNull(payload);
        Assert.Equal(customerId, payload.CustomerId);
        Assert.Equal(orgId, payload.OrgId);
        Assert.Equal("recipient@example.com", payload.Email);
    }

    [Fact]
    public void UnsubscribeToken_TamperedPayload_Refused()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string email = "target@example.com";

        string token = _signer.GenerateToken(customerId, orgId, email);
        string[] parts = token.Split('.');

        // Tamper with base64 payload by changing a character
        char replacementChar = parts[0][0] == 'e' ? 'f' : 'e';
        string tamperedPayload = replacementChar + parts[0][1..];
        string tamperedToken = $"{tamperedPayload}.{parts[1]}";

        bool isValid = _signer.TryValidateToken(tamperedToken, out UnsubscribeTokenPayload? payload);

        Assert.False(isValid);
        Assert.Null(payload);
    }

    [Fact]
    public void UnsubscribeToken_TamperedSignature_Refused()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string email = "target@example.com";

        string token = _signer.GenerateToken(customerId, orgId, email);
        string[] parts = token.Split('.');

        // Tamper with signature
        char replacementChar = parts[1][0] == 'a' ? 'b' : 'a';
        string tamperedSig = replacementChar + parts[1][1..];
        string tamperedToken = $"{parts[0]}.{tamperedSig}";

        bool isValid = _signer.TryValidateToken(tamperedToken, out UnsubscribeTokenPayload? payload);

        Assert.False(isValid);
        Assert.Null(payload);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    [InlineData("singlepartonly")]
    [InlineData("part1.part2.part3")]
    public void UnsubscribeToken_MalformedInput_Refused(string malformedToken)
    {
        bool isValid = _signer.TryValidateToken(malformedToken, out UnsubscribeTokenPayload? payload);

        Assert.False(isValid);
        Assert.Null(payload);
    }

    [Fact]
    public void TemplateSanitizer_RemovesScriptsAndDangerousAttributes()
    {
        string maliciousHtml = """
            <div>
                <h1>Special Offer</h1>
                <p>Hello <span style="color: red;">Valued Customer</span>!</p>
                <script>alert('xss');</script>
                <img src="https://example.com/logo.png" onload="alert('pwned')" />
                <a href="javascript:alert('attack')">Click here</a>
            </div>
            """;

        string cleanHtml = TemplateSanitizer.Sanitize(maliciousHtml);

        Assert.DoesNotContain("<script", cleanHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert('xss')", cleanHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", cleanHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", cleanHtml, StringComparison.OrdinalIgnoreCase);

        // Safe markup should remain intact
        Assert.Contains("Special Offer", cleanHtml);
        Assert.Contains("https://example.com/logo.png", cleanHtml);
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class CampaignDatabaseTests
{
    private readonly PostgresFixture _postgres;

    public CampaignDatabaseTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task UnsubscribedAddress_NeverResolvesIntoAudience()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };

        string unsubscribedEmail = $"unsub_{Guid.NewGuid():N}@example.com";
        string activeEmail = $"active_{Guid.NewGuid():N}@example.com";

        // Seed two leads
        db.Leads.AddRange(
            new Lead
            {
                Name = "Unsubscribed Lead",
                Email = unsubscribedEmail,
                Status = LeadStatus.New,
                Source = LeadSource.Website
            },
            new Lead
            {
                Name = "Active Lead",
                Email = activeEmail,
                Status = LeadStatus.New,
                Source = LeadSource.Website
            });

        // Mark first email as unsubscribed in EmailConsents
        db.EmailConsents.Add(new EmailConsent
        {
            Email = unsubscribedEmail.ToLowerInvariant(),
            Status = ConsentStatus.Unsubscribed,
            Source = ConsentSource.UnsubscribeLink,
            ChangedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var consentRepo = new EmailConsentRepository(db);
        var audienceRepo = new AudienceRepository(
            db,
            tenant,
            new FakeContactsClient([]),
            new FakeReportingClient([]),
            consentRepo);

        var rules = new AudienceRules
        {
            Leads = new LeadRule
            {
                Statuses = [LeadStatus.New]
            }
        };

        var preview = await audienceRepo.ResolvePreviewAsync(rules, default);

        Assert.Equal(1, preview.TotalSubscribedRecipients);
        Assert.Equal(1, preview.TotalSuppressedRecipients);
        Assert.Contains(preview.SampleRecipients, r => r.Email.Equals(activeEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(preview.SampleRecipients, r => r.Email.Equals(unsubscribedEmail, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeContactsClient : IContactsClient
    {
        private readonly List<ContactItemDto> _contacts;
        public FakeContactsClient(List<ContactItemDto> contacts) => _contacts = contacts;

        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) =>
            Task.FromResult(_contacts.Any(c => c.ContactId == contactId));

        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct) =>
            Task.FromResult<CreatedContact?>(new CreatedContact(999, "CON-999", request.DisplayName));

        public Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct) =>
            Task.FromResult(_contacts);
    }

    private sealed class FakeReportingClient : IReportingClient
    {
        private readonly List<long> _contactIds;
        public FakeReportingClient(List<long> contactIds) => _contactIds = contactIds;

        public Task<List<long>> SegmentContactsAsync(
            Guid customerId,
            Guid orgId,
            PurchaseHistoryRule rule,
            CancellationToken ct) => Task.FromResult(_contactIds);

        public Task<Dictionary<long, (DateOnly Date, decimal Total)>> GetFirstInvoicesAsync(
            Guid customerId,
            Guid orgId,
            List<long> contactIds,
            CancellationToken ct) => Task.FromResult(new Dictionary<long, (DateOnly Date, decimal Total)>());
    }
}
