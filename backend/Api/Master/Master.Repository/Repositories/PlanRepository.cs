using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Licensing;

namespace Master.Repository.Repositories;

/// <summary>
/// The customer's plan: what it allows the signed-in branch, how much of that is
/// used, and asking for a higher one (D-68, D-70, TK-209).
///
/// There is no payment in the app. A request is recorded, mailed to the address
/// in <c>Licensing:UpgradeRequestEmail</c>, and a platform operator changes the
/// plan from the admin screens.
/// </summary>
public sealed class PlanRepository
{
    /// <summary>The configuration key holding the address upgrade requests are mailed to.</summary>
    public const string UpgradeRequestEmailKey = "Licensing:UpgradeRequestEmail";

    private readonly AdminDbContext _db;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<PlanRepository> _log;

    public PlanRepository(AdminDbContext db, IEmailSender email, IConfiguration config, ILogger<PlanRepository> log)
    {
        _db = db;
        _email = email;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// The plan and its limits as they apply to <paramref name="orgId"/>, or
    /// null when the customer holds no licence. A customer that is never billed
    /// is shown as it is stored, Elite, with nothing left to upgrade to.
    /// </summary>
    public async Task<PlanSummaryResponse?> GetSummaryAsync(Guid customerId, Guid orgId, CancellationToken ct)
    {
        var row = await (
            from l in _db.Licenses
            join c in _db.Customers on l.CustomerId equals c.CustomerId
            where l.CustomerId == customerId
            select new
            {
                l.LicenseType,
                l.MaxUsers,
                l.MonthlySalesTransactionLimit,
                l.MonthlyPurchaseTransactionLimit,
                c.Kind,
            }).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        bool unbilled = CustomerKindRepository.NeverLapses(row.Kind);

        // Everyone ever given access to the branch, revoked or not (D-68).
        int used = await _db.UserOrganizationRoles
            .Where(a => a.OrgId == orgId)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(ct);

        bool requested = await _db.UpgradeRequests.AnyAsync(
            r => r.CustomerId == customerId && r.Status == UpgradeRequestStatus.Pending, ct);

        int? maxUsers = unbilled ? null : row.MaxUsers;

        return new PlanSummaryResponse
        {
            Plan = PlanCatalogRepository.Name(row.LicenseType),
            LicenseType = row.LicenseType,
            MaxUsers = maxUsers,
            UsersUsed = used,
            UserLimitMessage = maxUsers is int limit && used >= limit
                ? PlanCatalogRepository.UserLimitMessage(row.LicenseType, limit)
                : null,
            MonthlySalesTransactionLimit = unbilled ? null : row.MonthlySalesTransactionLimit,
            MonthlyPurchaseTransactionLimit = unbilled ? null : row.MonthlyPurchaseTransactionLimit,
            CanUpgrade = !unbilled && row.LicenseType != LicenseType.Elite,
            UpgradeRequested = requested,
        };
    }

    /// <summary>
    /// Records a request to move to a higher plan and mails it. A customer with
    /// one request waiting does not make another: the second ask is answered
    /// with the first, so a double click, or two people hitting the same limit,
    /// send one email.
    /// </summary>
    public async Task<UpgradeRequestResult> RequestUpgradeAsync(
        Guid customerId, Guid orgId, Guid userId, UpgradeRequestInput input, CancellationToken ct)
    {
        var customer = await (
            from l in _db.Licenses
            join c in _db.Customers on l.CustomerId equals c.CustomerId
            where l.CustomerId == customerId
            select new { l.LicenseType, c.Kind, c.Name, c.CustomerCode }).FirstOrDefaultAsync(ct);
        if (customer is null)
        {
            return new UpgradeRequestResult(UpgradeRequestOutcome.NoLicence, null);
        }

        LicenseType target = input.ToPlan ?? LicenseType.Elite;
        if (CustomerKindRepository.NeverLapses(customer.Kind)
            || !PlanCatalogRepository.IsUpgrade(customer.LicenseType, target))
        {
            return new UpgradeRequestResult(UpgradeRequestOutcome.NotAnUpgrade, null);
        }

        if (await _db.UpgradeRequests.AnyAsync(
                r => r.CustomerId == customerId && r.Status == UpgradeRequestStatus.Pending, ct))
        {
            return new UpgradeRequestResult(UpgradeRequestOutcome.AlreadyRequested, null);
        }

        var request = new UpgradeRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            RequestedByUserId = userId,
            FromPlan = customer.LicenseType,
            ToPlan = target,
            Reason = input.Reason,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
        };
        _db.UpgradeRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        await MailAsync(request, customer.Name, customer.CustomerCode, ct);
        return new UpgradeRequestResult(UpgradeRequestOutcome.Recorded, request.UpgradeRequestId);
    }

    /// <summary>
    /// Tells the operators. The request is already saved, so a missing address
    /// or a mailbox that cannot send is logged and not the customer's problem:
    /// the request waits on the admin screens either way.
    /// </summary>
    private async Task MailAsync(UpgradeRequest request, string customerName, string customerCode, CancellationToken ct)
    {
        string? to = _config[UpgradeRequestEmailKey];
        if (string.IsNullOrWhiteSpace(to))
        {
            _log.LogWarning(
                "{Key} is not set; upgrade request {RequestId} was recorded and not mailed.",
                UpgradeRequestEmailKey, request.UpgradeRequestId);
            return;
        }

        var person = await _db.Users
            .Where(u => u.UserId == request.RequestedByUserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(ct);
        string branch = await _db.Organizations
            .Where(o => o.OrgId == request.OrgId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        string from = PlanCatalogRepository.Name(request.FromPlan);
        string toPlan = PlanCatalogRepository.Name(request.ToPlan);
        string why = request.Reason is PlanLimitKind kind ? kind.ToString() : "From the Plan screen";
        string who = person is null ? "unknown" : $"{person.DisplayName} <{person.Email}>";
        string note = request.Note ?? "(none)";

        static string Html(string text) => System.Net.WebUtility.HtmlEncode(text);

        try
        {
            await _email.SendAsync(new EmailMessage
            {
                ToEmail = to.Trim(),
                Subject = $"Upgrade request: {customerName} ({customerCode}), {from} to {toPlan}",
                HtmlBody =
                    $"<p><strong>{Html(customerName)}</strong> ({Html(customerCode)}) asked to move from {Html(from)} to {Html(toPlan)}.</p>" +
                    $"<p>Branch: {Html(branch)}<br/>Asked by: {Html(who)}<br/>Reason: {Html(why)}<br/>Note: {Html(note)}</p>",
                TextBody =
                    $"{customerName} ({customerCode}) asked to move from {from} to {toPlan}.\n\n" +
                    $"Branch: {branch}\nAsked by: {who}\nReason: {why}\nNote: {note}",
            }, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Upgrade request {RequestId} was recorded and could not be mailed.", request.UpgradeRequestId);
        }
    }

    /// <summary>
    /// Brings the licences already stored in line with their plans, at every
    /// start (D-68, TK-209). Idempotent. A paid plan or a trial is never left
    /// without a limit, because an empty limit means none; Elite is never left
    /// with one, which also retires the 10,000 users an unbilled customer's
    /// licence once carried in place of "none". A limit that is already set on
    /// anything else is an operator's decision and stays.
    /// </summary>
    public static async Task ApplyDefaultsAsync(AdminDbContext db, CancellationToken ct)
    {
        await db.Licenses
            .Where(l => l.LicenseType == LicenseType.Elite
                && (l.MaxUsers != null || l.MonthlySalesTransactionLimit != null || l.MonthlyPurchaseTransactionLimit != null))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.MaxUsers, (int?)null)
                .SetProperty(l => l.MonthlySalesTransactionLimit, (int?)null)
                .SetProperty(l => l.MonthlyPurchaseTransactionLimit, (int?)null), ct);

        await db.Licenses
            .Where(l => l.LicenseType == LicenseType.Trial && l.MaxUsers == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.MaxUsers, (int?)PlanCatalogRepository.TrialMaxUsers), ct);

        await db.Licenses
            .Where(l => l.LicenseType != LicenseType.Trial && l.LicenseType != LicenseType.Elite && l.MaxUsers == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.MaxUsers, (int?)PlanCatalogRepository.ProfessionalMaxUsers), ct);

        await db.Licenses
            .Where(l => l.LicenseType != LicenseType.Elite && l.MonthlySalesTransactionLimit == null)
            .ExecuteUpdateAsync(s => s.SetProperty(
                l => l.MonthlySalesTransactionLimit, (int?)PlanCatalogRepository.ProfessionalMonthlyTransactions), ct);

        await db.Licenses
            .Where(l => l.LicenseType != LicenseType.Elite && l.MonthlyPurchaseTransactionLimit == null)
            .ExecuteUpdateAsync(s => s.SetProperty(
                l => l.MonthlyPurchaseTransactionLimit, (int?)PlanCatalogRepository.ProfessionalMonthlyTransactions), ct);
    }
}

public enum UpgradeRequestOutcome
{
    Recorded = 1,
    AlreadyRequested = 2,
    NotAnUpgrade = 3,
    NoLicence = 4,
}

public sealed record UpgradeRequestResult(UpgradeRequestOutcome Outcome, long? UpgradeRequestId);
