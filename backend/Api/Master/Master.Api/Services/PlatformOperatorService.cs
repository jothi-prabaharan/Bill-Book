using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Who runs the platform (D-01): <c>mst.Users.IsPlatformOperator</c>, never a
/// role.
///
/// Two ways to set it, and only two. <c>Bootstrap:OperatorEmails</c> grants it
/// at every startup — the way the first operator exists, since nobody can grant
/// it before there is one — and an existing operator can grant or revoke it
/// through <c>api/admin/platform-operators</c>. No tenant screen can reach
/// either path: the endpoint needs <c>platform.edit</c>, which only an operator's
/// token carries.
/// </summary>
public sealed class PlatformOperatorService
{
    /// <summary>The permission module whose every code an operator's token carries.</summary>
    public const string PlatformModule = "platform";

    private readonly AdminDbContext _db;

    public PlatformOperatorService(AdminDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlatformOperatorItem>> ListAsync(CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.IsPlatformOperator)
            .OrderBy(u => u.Email)
            .Select(u => new PlatformOperatorItem(u.UserId, u.Email, u.DisplayName, u.IsActive))
            .ToListAsync(ct);

    /// <summary>
    /// Grants or revokes the flag. An operator cannot revoke their own — the
    /// last one doing so would leave nobody able to grant it back short of a
    /// redeploy with new bootstrap settings.
    /// </summary>
    public async Task<PlatformOperatorOutcome> SetAsync(
        Guid userId, bool isOperator, Guid? callerId, CancellationToken ct)
    {
        if (!isOperator && callerId == userId)
        {
            return PlatformOperatorOutcome.CannotRevokeSelf;
        }

        int changed = await _db.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsPlatformOperator, isOperator), ct);

        return changed == 0 ? PlatformOperatorOutcome.NotFound : PlatformOperatorOutcome.Ok;
    }

    /// <summary>
    /// Every <c>platform.*</c> code in the catalogue. What an operator's token
    /// adds to their role's permissions.
    /// </summary>
    public static IQueryable<string> PlatformPermissionCodes(AdminDbContext db) =>
        db.Permissions.Where(p => p.Module == PlatformModule).Select(p => p.Code);

    /// <summary>
    /// <c>Bootstrap:OperatorEmails</c> as a list: either a comma- or
    /// semicolon-separated string (an environment variable) or an array
    /// (appsettings). Trimmed and lower-cased; blanks dropped.
    /// </summary>
    public static IReadOnlyList<string> BootstrapEmails(IConfiguration config)
    {
        IConfigurationSection section = config.GetSection("Bootstrap:OperatorEmails");

        IEnumerable<string?> raw = section.Value is { } single
            ? single.Split([',', ';'])
            : section.GetChildren().Select(c => c.Value);

        return raw
            .Select(e => e?.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .Select(e => e!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Sets the flag on every existing user named by bootstrap. Grant-only: an
    /// address removed from the setting keeps the flag until an operator revokes
    /// it, so a configuration typo cannot lock every operator out at once.
    /// Returns how many users gained it.
    /// </summary>
    public static Task<int> ApplyBootstrapAsync(
        AdminDbContext db, IReadOnlyList<string> emails, CancellationToken ct) =>
        emails.Count == 0
            ? Task.FromResult(0)
            : db.Users
                .Where(u => !u.IsPlatformOperator && emails.Contains(u.Email.ToLower()))
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsPlatformOperator, true), ct);
}

public sealed record PlatformOperatorItem(Guid UserId, string Email, string DisplayName, bool IsActive);

public sealed class SetPlatformOperatorRequest
{
    public bool IsPlatformOperator { get; set; }
}

public enum PlatformOperatorOutcome
{
    Ok,
    NotFound,
    CannotRevokeSelf,
}
