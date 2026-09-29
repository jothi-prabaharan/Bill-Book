using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Services.Schedules;

/// <summary>The branch's active users and what each may do now, from Master (TK-114).</summary>
public interface IBranchUsers
{
    /// <summary>Null when Master could not be asked.</summary>
    Task<IReadOnlyList<BranchUserAccess>?> ListAsync(CancellationToken ct);
}

public sealed class HttpBranchUsers : IBranchUsers
{
    private readonly HttpClient _master;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpBranchUsers> _log;

    public HttpBranchUsers(HttpClient master, ITenantContext tenant, ILogger<HttpBranchUsers> log)
    {
        _master = master;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IReadOnlyList<BranchUserAccess>?> ListAsync(CancellationToken ct)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        try
        {
            return await _master.GetFromJsonAsync<List<BranchUserAccess>>(
                $"internal/users/branch?orgId={orgId}&app={App.RetailErp}", ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) && !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Could not read the users of organization {OrgId}", orgId);
            return null;
        }
    }
}

/// <summary>One report email to one person.</summary>
public sealed record ReportMail(string ToEmail, string ToName, string Subject, string HtmlBody, string FileName, byte[] Workbook);

/// <summary>Sends a scheduled report's email (TK-114).</summary>
public interface IReportMailer
{
    Task SendAsync(ReportMail mail, CancellationToken ct);
}

/// <summary>
/// Posts to Master's <c>internal/notifications/email</c>, which sends through
/// Notification.Worker where it runs and in process where it does not. The
/// customer's own mailbox, so the report comes from the business.
/// </summary>
public sealed class MasterReportMailer : IReportMailer
{
    private readonly HttpClient _master;
    private readonly ITenantContext _tenant;

    public MasterReportMailer(HttpClient master, ITenantContext tenant)
    {
        _master = master;
        _tenant = tenant;
    }

    public async Task SendAsync(ReportMail mail, CancellationToken ct)
    {
        using HttpResponseMessage response = await _master.PostAsJsonAsync("internal/notifications/email", new
        {
            toEmail = mail.ToEmail,
            toName = mail.ToName,
            subject = mail.Subject,
            htmlBody = mail.HtmlBody,
            customerId = _tenant.CustomerId,
            attachments = new[]
            {
                new { fileName = mail.FileName, contentType = ExcelReportWriter.ContentType, content = mail.Workbook },
            },
        }, ct);

        if (response.StatusCode != HttpStatusCode.Accepted && !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Master refused the email with {(int)response.StatusCode}.", null, response.StatusCode);
        }
    }
}
