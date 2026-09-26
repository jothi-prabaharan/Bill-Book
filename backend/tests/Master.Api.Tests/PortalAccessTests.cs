using System.IdentityModel.Tokens.Jwt;
using Master.Api.Services;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Revocable portal access (TK-94): a link carries a code whose hash alone is
/// stored, the portal exchanges it for a one-hour session while the grant
/// lives, and a revoked or expired grant, or a deactivated contact, is refused.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalAccessTests
{
    private readonly PostgresFixture _postgres;

    public PortalAccessTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public void A_code_that_is_not_a_portal_code_is_not_read()
    {
        Assert.False(PortalAccessService.TryReadCode(null, out _, out _));
        Assert.False(PortalAccessService.TryReadCode("", out _, out _));
        Assert.False(PortalAccessService.TryReadCode("bb_abc_def", out _, out _));
        Assert.False(PortalAccessService.TryReadCode($"pg_{Guid.NewGuid():N}_{Guid.NewGuid():N}_short", out _, out _));
        Assert.False(PortalAccessService.TryReadCode($"pg_{Guid.Empty:N}_{Guid.NewGuid():N}_{new string('a', 43)}", out _, out _));
    }

    [Theory]
    [InlineData(null, 90)]
    [InlineData("", 90)]
    [InlineData("abc", 90)]
    [InlineData("0", 90)]
    [InlineData("-5", 90)]
    [InlineData("99999", 90)]
    [InlineData("30", 30)]
    [InlineData("365", 365)]
    public void The_link_lifetime_setting_falls_back_to_ninety_days_when_it_makes_no_sense(string? value, int days) =>
        Assert.Equal(days, ConfigurationPortalLinkLifetime.Parse(value));

    [SkippableFact]
    public async Task A_link_names_its_branch_and_its_code_is_never_stored_in_clear()
    {
        Harness h = await Harness.CreateAsync(_postgres);

        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;

        Assert.True(PortalAccessService.TryReadCode(link.Code, out Guid customerId, out Guid orgId));
        Assert.Equal(h.Tenant.CustomerId, customerId);
        Assert.Equal(h.Tenant.OrgId, orgId);

        PortalGrant grant = await h.Db.PortalGrants.AsNoTracking().SingleAsync();
        string secret = link.Code.Split('_', 4)[3];

        Assert.Equal(HashUtil.Sha256(link.Code), grant.CodeHash);
        Assert.DoesNotContain(secret, grant.CodeHash, StringComparison.Ordinal);
        Assert.Equal(h.Clock.GetUtcNow().AddDays(90), grant.ExpiresAt);
    }

    [SkippableFact]
    public async Task A_live_code_opens_a_one_hour_session_naming_its_contact_and_grant()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.School, default))!;

        PortalSessionToken session = (await h.Portal.ExchangeAsync(link.Code, default))!;

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        PortalGrant grant = await h.Db.PortalGrants.AsNoTracking().SingleAsync();

        Assert.Equal(h.ContactId.ToString(), token.Claims.Single(c => c.Type == RequirePortalAccessAttribute.ContactClaim).Value);
        Assert.Equal(grant.PortalGrantId.ToString(), token.Claims.Single(c => c.Type == RequirePortalAccessAttribute.GrantClaim).Value);
        Assert.Equal("School", token.Claims.Single(c => c.Type == RequireAppAttribute.ClaimType).Value);
        Assert.DoesNotContain(token.Claims, c => c.Type == "permission");
        Assert.Equal(App.School, session.App);
        Assert.Equal(h.Clock.GetUtcNow().AddHours(1), session.ExpiresAt);
        Assert.Equal(h.Clock.GetUtcNow(), grant.LastUsedAt);
    }

    [SkippableFact]
    public async Task Revoking_stops_a_fresh_session_for_every_link_the_contact_has()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink first = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;
        PortalLink second = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;

        Assert.Equal(2, (await h.Portal.GetStateAsync(h.ContactId, default))!.LiveLinks);
        Assert.Equal(2, await h.Portal.RevokeAsync(h.ContactId, default));

        Assert.Null(await h.Portal.ExchangeAsync(first.Code, default));
        Assert.Null(await h.Portal.ExchangeAsync(second.Code, default));
        Assert.Equal(0, (await h.Portal.GetStateAsync(h.ContactId, default))!.LiveLinks);

        // A link made after the revoke works: revoking is not a ban.
        PortalLink third = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;
        Assert.NotNull(await h.Portal.ExchangeAsync(third.Code, default));
    }

    [SkippableFact]
    public async Task An_expired_link_is_refused()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;

        h.Clock.Advance(TimeSpan.FromDays(90));

        Assert.Null(await h.Portal.ExchangeAsync(link.Code, default));
    }

    [SkippableFact]
    public async Task A_session_opened_in_a_links_last_minutes_ends_with_the_link()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;

        h.Clock.Advance(TimeSpan.FromDays(90) - TimeSpan.FromMinutes(20));

        Assert.Equal(link.ExpiresAt, (await h.Portal.ExchangeAsync(link.Code, default))!.ExpiresAt);
    }

    [SkippableFact]
    public async Task A_deactivated_contacts_link_is_refused()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;

        await h.Db.Contacts.Where(c => c.ContactId == h.ContactId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.IsActive, false));

        Assert.Null(await h.Portal.ExchangeAsync(link.Code, default));
    }

    [SkippableFact]
    public async Task A_code_with_its_secret_or_its_branch_changed_matches_nothing()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        PortalLink link = (await h.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default))!;
        string[] parts = link.Code.Split('_', 4);

        string otherSecret = $"{parts[0]}_{parts[1]}_{parts[2]}_{new string('A', parts[3].Length)}";
        Assert.Null(await h.Portal.ExchangeAsync(otherSecret, default));

        // The same secret under another branch's id hashes to nothing, and that
        // branch's filter could not see this grant anyway.
        Harness other = await Harness.CreateAsync(_postgres, h.Tenant.CustomerId);
        string otherBranch = $"{parts[0]}_{parts[1]}_{other.Tenant.OrgId:N}_{parts[3]}";
        Assert.Null(await other.Portal.ExchangeAsync(otherBranch, default));
        Assert.Null(await other.Portal.ExchangeAsync(link.Code, default));
    }

    [SkippableFact]
    public async Task Another_branchs_contact_gets_no_link_and_cannot_be_revoked()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        Harness other = await Harness.CreateAsync(_postgres, h.Tenant.CustomerId);

        Assert.Null(await other.Portal.CreateLinkAsync(h.ContactId, App.RetailErp, default));
        Assert.Null(await other.Portal.RevokeAsync(h.ContactId, default));
        Assert.Null(await other.Portal.GetStateAsync(h.ContactId, default));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FixedLifetime(int days) : IPortalLinkLifetime
    {
        public Task<int> DaysAsync(Guid orgId, CancellationToken ct) => Task.FromResult(days);
    }

    private sealed class StaffUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => null;
    }

    private sealed record Harness(ContactsDbContext Db, TenantContext Tenant, PortalAccessService Portal, ManualClock Clock, long ContactId)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture postgres, Guid? customerId = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var tenant = new TenantContext
            {
                CustomerId = customerId ?? Guid.NewGuid(),
                OrgId = Guid.NewGuid(),
                CustomerCode = "0000000042",
            };

            ContactsDbContext db = postgres.CreateContext(tenant);

            var contact = new Contact
            {
                ContactCode = $"C{Random.Shared.Next(100000, 999999)}",
                DisplayName = "Kaveri",
                LegalName = "Kaveri Constructions Pvt Ltd",
                CurrencyCode = "INR",
                IsCustomer = true,
            };
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            var clock = new ManualClock(new DateTimeOffset(2026, 9, 26, 6, 0, 0, TimeSpan.Zero));
            var tokens = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "bill-book",
                Audience = "bill-book",
                SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
                RefreshTokenDays = 7,
            }), clock);

            var portal = new PortalAccessService(db, new FixedLifetime(90), tenant, tokens, new StaffUser(), clock);
            return new Harness(db, tenant, portal, clock, contact.ContactId);
        }
    }
}
