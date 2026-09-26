using System.Security.Cryptography;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

/// <summary>A portal link just made: the code to put in it, shown once, and when it stops working.</summary>
public sealed record PortalLink(string Code, DateTimeOffset ExpiresAt);

/// <summary>A portal session: the one-hour token, when it runs out, and which portal it opens.</summary>
public sealed record PortalSessionToken(string Token, DateTimeOffset ExpiresAt, App App);

/// <summary>What the contact screen shows about a contact's portal access.</summary>
public sealed record PortalAccessState(int LiveLinks, DateTimeOffset? LastUsedAt, DateTimeOffset? ExpiresAt);

/// <summary>
/// Revocable portal access (TK-94, design "Client portal" → Access).
///
/// A portal link carries a code, not a token. The portal exchanges the code
/// for a one-hour token while the grant behind it lives, and exchanges it again
/// before the hour runs out. Staff revoke a contact's grants, and from then on
/// no new token is issued; a token already issued runs out within the hour.
///
/// <b>The code names its own customer and branch</b>
/// (<c>pg_{customer}_{org}_{secret}</c>), the way an API key names its
/// customer. The session request is anonymous, so nothing else says which
/// shard to open or which tenant to set; with both taken from the code, the
/// grant is then found under the ordinary query filter and RLS policy, by its
/// hash alone. The two ids are no secret — the portal token carries them
/// anyway — and only the 256-bit secret opens anything. A code whose ids are
/// altered hashes to nothing.
/// </summary>
public sealed class PortalAccessService
{
    /// <summary>Days a link works when the branch has not set <c>portal.linkDays</c>.</summary>
    public const int DefaultLinkDays = 90;

    private const string Prefix = "pg";

    private readonly ContactsDbContext _db;
    private readonly IPortalLinkLifetime _lifetime;
    private readonly ITenantContext _tenant;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PortalAccessService(
        ContactsDbContext db,
        IPortalLinkLifetime lifetime,
        ITenantContext tenant,
        ITokenService tokens,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _lifetime = lifetime;
        _tenant = tenant;
        _tokens = tokens;
        _user = user;
        _clock = clock;
    }

    /// <summary>
    /// A new link for a contact of the caller's branch, or null when there is
    /// no such contact. Older links keep working; revoking ends them all.
    /// </summary>
    public async Task<PortalLink?> CreateLinkAsync(long contactId, App app, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        if (!await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            return null;
        }

        string code = NewCode(customerId, orgId);
        DateTimeOffset expires = _clock.GetUtcNow().AddDays(await _lifetime.DaysAsync(orgId, ct));

        _db.PortalGrants.Add(new PortalGrant
        {
            ContactId = contactId,
            App = app,
            CodeHash = HashUtil.Sha256(code),
            ExpiresAt = expires,
        });

        await _db.SaveChangesAsync(ct);
        return new PortalLink(code, expires);
    }

    /// <summary>How many of the contact's links still work, and when one was last used. Null when there is no such contact.</summary>
    public async Task<PortalAccessState?> GetStateAsync(long contactId, CancellationToken ct)
    {
        if (!await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            return null;
        }

        DateTimeOffset now = _clock.GetUtcNow();

        var grants = await _db.PortalGrants
            .AsNoTracking()
            .Where(g => g.ContactId == contactId)
            .Select(g => new { g.RevokedAt, g.ExpiresAt, g.LastUsedAt })
            .ToListAsync(ct);

        var live = grants.Where(g => g.RevokedAt == null && g.ExpiresAt > now).ToList();

        return new PortalAccessState(
            live.Count,
            grants.Max(g => g.LastUsedAt),
            live.Count == 0 ? null : live.Max(g => g.ExpiresAt));
    }

    /// <summary>
    /// Revokes every link the contact has, and answers how many were still live.
    /// Null when there is no such contact.
    /// </summary>
    public async Task<int?> RevokeAsync(long contactId, CancellationToken ct)
    {
        if (!await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            return null;
        }

        DateTimeOffset now = _clock.GetUtcNow();

        List<PortalGrant> live = await _db.PortalGrants
            .Where(g => g.ContactId == contactId && g.RevokedAt == null && g.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (PortalGrant grant in live)
        {
            grant.RevokedAt = now;
            grant.RevokedBy = _user.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return live.Count;
    }

    /// <summary>
    /// The customer and branch a code names, or false when it is not a portal
    /// code at all. The caller sets the tenant from these before
    /// <see cref="ExchangeAsync"/> opens the database.
    /// </summary>
    public static bool TryReadCode(string? code, out Guid customerId, out Guid orgId)
    {
        customerId = Guid.Empty;
        orgId = Guid.Empty;

        string[] parts = (code ?? string.Empty).Split('_', 4);

        return parts.Length == 4
            && parts[0] == Prefix
            && parts[3].Length >= 40
            && Guid.TryParseExact(parts[1], "N", out customerId)
            && Guid.TryParseExact(parts[2], "N", out orgId)
            && customerId != Guid.Empty
            && orgId != Guid.Empty;
    }

    /// <summary>
    /// A one-hour session for a live grant, or null — for a code that matches
    /// nothing, a revoked or expired grant, or a contact since deactivated. The
    /// four are not told apart, so the answer says nothing about which codes
    /// exist. The tenant must already be the code's (<see cref="TryReadCode"/>).
    /// </summary>
    public async Task<PortalSessionToken?> ExchangeAsync(string code, CancellationToken ct, string? customerCode = null)
    {
        string hash = HashUtil.Sha256(code);
        DateTimeOffset now = _clock.GetUtcNow();

        await using ITransactionScope scope = await _db.Database.BeginScopeAsync(ct);

        PortalGrant? grant = await _db.PortalGrants
            .FirstOrDefaultAsync(g => g.CodeHash == hash && g.RevokedAt == null && g.ExpiresAt > now, ct);

        if (grant is null
            || !await _db.Contacts.AnyAsync(c => c.ContactId == grant.ContactId && c.IsActive, ct))
        {
            return null;
        }

        grant.LastUsedAt = now;
        await _db.SaveChangesAsync(ct);
        await scope.CommitAsync(ct);

        (string token, DateTimeOffset expires) = _tokens.CreatePortalToken(
            grant.CustomerId, grant.OrgId, grant.ContactId, grant.PortalGrantId, grant.App, customerCode);

        // Never past the grant: a session opened in a link's last minutes ends with it.
        return new PortalSessionToken(token, expires < grant.ExpiresAt ? expires : grant.ExpiresAt, grant.App);
    }

    private static string NewCode(Guid customerId, Guid orgId) =>
        $"{Prefix}_{customerId:N}_{orgId:N}_{Base64Url(RandomNumberGenerator.GetBytes(32))}";

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>How many days a branch's portal links work (TK-94).</summary>
public interface IPortalLinkLifetime
{
    Task<int> DaysAsync(Guid orgId, CancellationToken ct);
}

/// <summary>
/// Reads <c>portal.linkDays</c> from <c>mst.Configurations</c>: the branch's
/// override, else the shipped default, else 90. Anything outside a day to ten
/// years reads as the default rather than as a link that never works or never ends.
/// </summary>
public sealed class ConfigurationPortalLinkLifetime : IPortalLinkLifetime
{
    public const string Code = "portal.linkDays";

    private readonly AdminDbContext _admin;

    public ConfigurationPortalLinkLifetime(AdminDbContext admin) => _admin = admin;

    public async Task<int> DaysAsync(Guid orgId, CancellationToken ct)
    {
        var rows = await _admin.Configurations
            .AsNoTracking()
            .Where(c => c.Code == Code && (c.OrgId == null || c.OrgId == orgId))
            .Select(c => new { c.OrgId, c.Value })
            .ToListAsync(ct);

        string? value = rows.FirstOrDefault(r => r.OrgId == orgId)?.Value
            ?? rows.FirstOrDefault(r => r.OrgId == null)?.Value;

        return Parse(value);
    }

    public static int Parse(string? value) =>
        int.TryParse(value, out int days) && days is > 0 and <= 3650 ? days : PortalAccessService.DefaultLinkDays;
}
