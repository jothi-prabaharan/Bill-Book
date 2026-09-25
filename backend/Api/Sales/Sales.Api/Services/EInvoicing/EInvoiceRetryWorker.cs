using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Repository;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// Retries e-invoices still waiting for the IRP (TK-92): every Pending row
/// whose next attempt is due, in each branch, every few minutes. A hosted
/// service in Sales rather than a process of its own, as the design says.
///
/// It is also what registers a posting whose inline attempt never ran, since
/// work queued to run after a commit runs only inside a request. Failed rows
/// are left alone: those wait for a person to fix the document and press
/// retry.
/// </summary>
public sealed class EInvoiceRetryWorker : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>Rows per branch per tick, so one branch's backlog cannot hold up the rest.</summary>
    public const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly ILogger<EInvoiceRetryWorker> _log;

    public EInvoiceRetryWorker(IServiceScopeFactory scopes, ITenantEnumerator branches, ILogger<EInvoiceRetryWorker> log)
    {
        _scopes = scopes;
        _branches = branches;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Every branch, once. Public so a run can be driven without the schedule.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        int attempted = 0;
        foreach (ActiveOrganization branch in await _branches.ListAsync(ct))
        {
            try
            {
                attempted += await RunBranchAsync(branch, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "E-invoice retries for {OrgId} failed; the next tick tries again.", branch.OrgId);
            }
        }

        return attempted;
    }

    private async Task<int> RunBranchAsync(ActiveOrganization branch, CancellationToken ct)
    {
        using IServiceScope scope = _scopes.CreateScope();
        TenantContext tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.CustomerId = branch.CustomerId;
        tenant.OrgId = branch.OrgId;

        // Resolved only now: the context takes its connection and its query
        // filter from the tenant when it is built.
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var registrar = scope.ServiceProvider.GetRequiredService<EInvoiceRegistrar>();
        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        List<long> due = await db.EInvoices.AsNoTracking()
            .Where(e => e.Status == EInvoiceStatus.Pending && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.NextAttemptAt)
            .ThenBy(e => e.EInvoiceId)
            .Select(e => e.EInvoiceId)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (long id in due)
        {
            await registrar.RegisterAsync(id, null, ct);
        }

        return due.Count;
    }
}
