using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace BillBook.Worker.SavingsScheme;

/// <summary>
/// Savings scheme reminders and lapses (TK-141), twice a day by default. Only
/// Jewellery branches with the <see cref="AddOn.SavingsScheme"/> add-on are
/// visited; the work itself is <see cref="SavingsSchemeRun"/>.
/// </summary>
public sealed class SavingsSchemeWorker : BranchWorkerBase<SavingsSchemeWorker>
{
    public SavingsSchemeWorker(IServiceScopeFactory scopes, ILogger<SavingsSchemeWorker> logger, IConfiguration config)
        : base(scopes, logger, config)
    {
    }

    protected override string JobName => "SavingsScheme";

    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(12);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken ct)
    {
        if (branch.BusinessType != BusinessType.Jewellery || (branch.AddOns & AddOn.SavingsScheme) == 0)
        {
            return;
        }

        SavingsSchemeRunResult result = await scope.ServiceProvider
            .GetRequiredService<SavingsSchemeRun>()
            .RunAsync(branch.CustomerId, branch.OrgId, ct);

        if (result.Lapsed > 0 || result.Sent > 0 || result.Stopped)
        {
            _logger.LogInformation(
                "Savings schemes for {OrgId}: {Lapsed} lapsed, {Sent} reminders sent, {NoEmail} without an email{Stopped}.",
                branch.OrgId, result.Lapsed, result.Sent, result.NoEmail, result.Stopped ? "; stopped early" : "");
        }
    }
}
