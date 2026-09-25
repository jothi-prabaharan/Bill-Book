using MaintenanceContract.Entity.Enums;
using MaintenanceContract.Entity.Models;
using MaintenanceContract.Entity.TableEntities;
using MaintenanceContract.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.School;

namespace MaintenanceContract.Api.Services;

public enum AmcOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,
    StateRule = 4,

    /// <summary>Master, Facility or WorkOrder could not be reached.</summary>
    Unavailable = 5,
}

public sealed record AmcResult(AmcOutcome Outcome, long? Id = null, string? Detail = null)
{
    public static AmcResult Ok(long id) => new(AmcOutcome.Ok, id);

    public static AmcResult Fail(AmcOutcome outcome, string? detail = null) => new(outcome, null, detail);
}

/// <summary>
/// AMC contracts, covered assets and visits (S8, TK-68). The vendor is checked
/// through Master's contacts, the assets through Facility, and a visit's work
/// order is raised through WorkOrder under <c>AMC:{visitId}</c>, which WorkOrder
/// is idempotent on.
/// </summary>
public sealed class MaintenanceContractService
{
    private readonly MaintenanceContractDbContext _db;
    private readonly IContactDirectory _contacts;
    private readonly IFacilityClient _facility;
    private readonly IWorkOrderClient _workOrders;
    private readonly TimeProvider _clock;
    private readonly ILogger<MaintenanceContractService> _log;

    public MaintenanceContractService(
        MaintenanceContractDbContext db,
        IContactDirectory contacts,
        IFacilityClient facility,
        IWorkOrderClient workOrders,
        TimeProvider clock,
        ILogger<MaintenanceContractService> log)
    {
        _db = db;
        _contacts = contacts;
        _facility = facility;
        _workOrders = workOrders;
        _clock = clock;
        _log = log;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    public async Task<List<ContractView>> ListAsync(ContractStatus? status, CancellationToken ct)
    {
        DateOnly today = Today;
        List<AmcContract> rows = await _db.AmcContracts.AsNoTracking()
            .Where(c => status == null
                || (status == ContractStatus.Expired
                    ? c.ContractStatus == ContractStatus.Expired || (c.ContractStatus == ContractStatus.Active && c.EndDate < today)
                    : status == ContractStatus.Active
                        ? c.ContractStatus == ContractStatus.Active && c.EndDate >= today
                        : c.ContractStatus == status))
            .OrderBy(c => c.EndDate)
            .Take(1000)
            .ToListAsync(ct);
        return await ViewsAsync(rows, today, ct);
    }

    public async Task<ContractView?> GetAsync(long id, CancellationToken ct)
    {
        AmcContract? row = await _db.AmcContracts.AsNoTracking().FirstOrDefaultAsync(c => c.AmcContractId == id, ct);
        return row is null ? null : (await ViewsAsync([row], Today, ct)).Single();
    }

    public async Task<List<VisitView>> VisitsAsync(long contractId, CancellationToken ct) =>
        await _db.AmcVisits.AsNoTracking()
            .Where(v => v.AmcContractId == contractId)
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.AmcVisitId)
            .Select(v => new VisitView
            {
                AmcVisitId = v.AmcVisitId,
                AmcContractId = v.AmcContractId,
                VisitDate = v.VisitDate,
                VisitKind = v.VisitKind,
                FacilityAssetId = v.FacilityAssetId,
                Remarks = v.Remarks,
                WorkOrderId = v.WorkOrderId,
                WorkOrderNo = v.WorkOrderNo,
            })
            .ToListAsync(ct);

    public async Task<AmcResult> SaveAsync(long? id, SaveContractRequest request, CancellationToken ct)
    {
        if (request.EndDate <= request.StartDate)
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "The contract must end after it starts.");
        }

        List<long> assetIds = [.. request.FacilityAssetIds.Distinct()];

        AmcContract? contract = null;
        if (id is long existing)
        {
            contract = await _db.AmcContracts.FirstOrDefaultAsync(c => c.AmcContractId == existing, ct);
            if (contract is null)
            {
                return AmcResult.Fail(AmcOutcome.NotFound);
            }

            if (!AmcRules.TakesChanges(contract.ContractStatus) || AmcRules.Effective(contract.ContractStatus, contract.EndDate, Today) == ContractStatus.Expired)
            {
                return AmcResult.Fail(AmcOutcome.StateRule, "An expired or terminated contract takes no changes. Record a renewal as a new contract.");
            }

            if (!AmcRules.TermsEditable(contract.ContractStatus) && TermsChanged(contract, request))
            {
                return AmcResult.Fail(AmcOutcome.StateRule,
                    "An active contract's vendor, number, dates, value and cover are fixed. Change its covered assets, reminder or remarks only.");
            }
        }

        AmcResult? refused = await CheckReferencesAsync(request.VendorContactId, assetIds, ct);
        if (refused is not null)
        {
            return refused;
        }

        if (contract is { ContractStatus: ContractStatus.Active }
            && await ConflictsAsync(contract.AmcContractId, assetIds, contract.StartDate, contract.EndDate, ct) is { Count: > 0 })
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "An asset chosen is already under another active contract for these dates.");
        }

        if (contract is null)
        {
            contract = new AmcContract();
            _db.AmcContracts.Add(contract);
        }

        contract.ContractNo = request.ContractNo.Trim();
        contract.VendorContactId = request.VendorContactId;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.ContractValue = request.ContractValue;
        contract.BillingFrequency = request.BillingFrequency;
        contract.VisitsPerYear = request.VisitsPerYear;
        contract.AmcCoverage = request.AmcCoverage;
        contract.RenewalReminderDays = request.RenewalReminderDays;
        contract.ReminderEmail = string.IsNullOrWhiteSpace(request.ReminderEmail) ? null : request.ReminderEmail.Trim();
        contract.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();

        if (await _db.AmcContracts.AnyAsync(c => c.AmcContractId != contract.AmcContractId
                && c.VendorContactId == contract.VendorContactId && c.ContractNo == contract.ContractNo, ct))
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "This vendor already has a contract with that number.");
        }

        await _db.SaveChangesAsync(ct);

        // The covered list is replaced: removed assets go, new ones come.
        List<AmcCoveredAsset> covered = await _db.AmcCoveredAssets.Where(a => a.AmcContractId == contract.AmcContractId).ToListAsync(ct);
        _db.AmcCoveredAssets.RemoveRange(covered.Where(a => !assetIds.Contains(a.FacilityAssetId)));
        foreach (long assetId in assetIds.Where(a => covered.All(c => c.FacilityAssetId != a)))
        {
            _db.AmcCoveredAssets.Add(new AmcCoveredAsset { AmcContractId = contract.AmcContractId, FacilityAssetId = assetId });
        }

        await _db.SaveChangesAsync(ct);
        return AmcResult.Ok(contract.AmcContractId);
    }

    /// <summary>Puts a Draft contract in force, once none of its assets is under another active contract for the same dates.</summary>
    public async Task<AmcResult> ActivateAsync(long id, CancellationToken ct)
    {
        AmcContract? contract = await _db.AmcContracts.FirstOrDefaultAsync(c => c.AmcContractId == id, ct);
        if (contract is null)
        {
            return AmcResult.Fail(AmcOutcome.NotFound);
        }

        if (contract.ContractStatus != ContractStatus.Draft)
        {
            return AmcResult.Fail(AmcOutcome.StateRule, "Only a draft contract can be activated.");
        }

        if (contract.EndDate < Today)
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "The contract has already ended.");
        }

        List<long> assetIds = await _db.AmcCoveredAssets.Where(a => a.AmcContractId == id).Select(a => a.FacilityAssetId).ToListAsync(ct);
        if (assetIds.Count == 0)
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "Add the assets the contract covers before activating it.");
        }

        if (await ConflictsAsync(id, assetIds, contract.StartDate, contract.EndDate, ct) is { Count: > 0 })
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "An asset on this contract is already under another active contract for these dates.");
        }

        contract.ContractStatus = ContractStatus.Active;
        await _db.SaveChangesAsync(ct);
        return AmcResult.Ok(id);
    }

    public async Task<AmcResult> TerminateAsync(long id, string reason, CancellationToken ct)
    {
        AmcContract? contract = await _db.AmcContracts.FirstOrDefaultAsync(c => c.AmcContractId == id, ct);
        if (contract is null)
        {
            return AmcResult.Fail(AmcOutcome.NotFound);
        }

        if (!AmcRules.TakesChanges(contract.ContractStatus))
        {
            return AmcResult.Fail(AmcOutcome.StateRule, "The contract has already ended.");
        }

        contract.ContractStatus = ContractStatus.Terminated;
        contract.TerminationReason = reason.Trim();
        await _db.SaveChangesAsync(ct);
        return AmcResult.Ok(id);
    }

    /// <summary>
    /// Records a visit, and raises a work order for it when asked. The visit is
    /// saved first so its id keys the work order: a retried visit raises nothing
    /// twice, and a refusal from WorkOrder rolls the visit back with the request.
    /// </summary>
    public async Task<AmcResult> RecordVisitAsync(long contractId, RecordVisitRequest request, CancellationToken ct)
    {
        AmcContract? contract = await _db.AmcContracts.AsNoTracking().FirstOrDefaultAsync(c => c.AmcContractId == contractId, ct);
        if (contract is null)
        {
            return AmcResult.Fail(AmcOutcome.NotFound);
        }

        if (!AmcRules.IsInForce(contract.ContractStatus, contract.StartDate, contract.EndDate, request.VisitDate))
        {
            return AmcResult.Fail(AmcOutcome.StateRule, "A visit is recorded against an active contract, within its dates.");
        }

        if (request.FacilityAssetId is long assetId
            && !await _db.AmcCoveredAssets.AnyAsync(a => a.AmcContractId == contractId && a.FacilityAssetId == assetId, ct))
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "Choose an asset this contract covers.");
        }

        if (request.RaiseWorkOrder && request.FacilityAssetId is null)
        {
            return AmcResult.Fail(AmcOutcome.Invalid, "Choose the asset the work order is for.");
        }

        var visit = new AmcVisit
        {
            AmcContractId = contractId,
            VisitDate = request.VisitDate,
            VisitKind = request.VisitKind,
            FacilityAssetId = request.FacilityAssetId,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        _db.AmcVisits.Add(visit);
        await _db.SaveChangesAsync(ct);

        if (request.RaiseWorkOrder)
        {
            try
            {
                RaisedWorkOrder raised = await _workOrders.RaiseAsync(new RaiseWorkOrder
                {
                    SourceKey = SourceKey(visit.AmcVisitId),
                    Title = $"{(request.VisitKind == VisitKind.Breakdown ? "Breakdown" : "AMC visit")} · contract {contract.ContractNo}",
                    WorkOrderSource = "Amc",
                    FacilityAssetId = request.FacilityAssetId,
                    ReportedDate = request.VisitDate,
                    DueDate = request.VisitDate,
                    AmcContractId = contractId,
                }, ct);
                visit.WorkOrderId = raised.WorkOrderId;
                visit.WorkOrderNo = raised.WorkOrderNo;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogError(ex, "The work order for AMC visit {VisitId} could not be raised.", visit.AmcVisitId);
                return AmcResult.Fail(AmcOutcome.Unavailable);
            }
        }

        return AmcResult.Ok(visit.AmcVisitId);
    }

    /// <summary>
    /// The contracts due a renewal reminder on <paramref name="on"/>, for
    /// Notification.Worker. Active contracts past their end date are marked
    /// Expired first, so the stored status the list filters on stays true.
    /// </summary>
    public async Task<List<AmcRenewalDue>> RenewalsDueAsync(DateOnly on, CancellationToken ct)
    {
        await _db.AmcContracts
            .Where(c => c.ContractStatus == ContractStatus.Active && c.EndDate < on)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ContractStatus, ContractStatus.Expired), ct);

        List<AmcContract> due = (await _db.AmcContracts.AsNoTracking()
                .Where(c => c.ContractStatus == ContractStatus.Active && c.RenewalReminderDays > 0 && c.EndDate >= on)
                .ToListAsync(ct))
            .Where(c => AmcRules.IsRenewalDue(c.ContractStatus, c.EndDate, c.RenewalReminderDays, on))
            .ToList();
        if (due.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<long, ContactSummary> vendors;
        try
        {
            vendors = await _contacts.FindAsync(due.Select(c => c.VendorContactId).Distinct(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The reminder still goes; it names the contract instead of the vendor.
            _log.LogWarning(ex, "Vendor names for AMC renewal reminders could not be read.");
            vendors = new Dictionary<long, ContactSummary>();
        }

        return [.. due.Select(c => new AmcRenewalDue
        {
            AmcContractId = c.AmcContractId,
            ContractNo = c.ContractNo,
            VendorName = vendors.TryGetValue(c.VendorContactId, out ContactSummary? v) ? v.DisplayName : "the vendor",
            EndDate = c.EndDate,
            ContractValue = c.ContractValue,
            ReminderEmail = c.ReminderEmail,
        })];
    }

    public static string SourceKey(long visitId) => $"AMC:{visitId}";

    // ---- Helpers ---------------------------------------------------------------

    private static bool TermsChanged(AmcContract c, SaveContractRequest r) =>
        c.ContractNo != r.ContractNo.Trim()
        || c.VendorContactId != r.VendorContactId
        || c.StartDate != r.StartDate
        || c.EndDate != r.EndDate
        || c.ContractValue != r.ContractValue
        || c.BillingFrequency != r.BillingFrequency
        || c.VisitsPerYear != r.VisitsPerYear
        || c.AmcCoverage != r.AmcCoverage;

    /// <summary>The assets among <paramref name="assetIds"/> already under another active contract whose term overlaps.</summary>
    private async Task<List<long>> ConflictsAsync(long contractId, List<long> assetIds, DateOnly start, DateOnly end, CancellationToken ct) =>
        await (from a in _db.AmcCoveredAssets
               join c in _db.AmcContracts on a.AmcContractId equals c.AmcContractId
               where assetIds.Contains(a.FacilityAssetId)
                   && c.AmcContractId != contractId
                   && c.ContractStatus == ContractStatus.Active
                   && c.StartDate <= end && start <= c.EndDate
               select a.FacilityAssetId)
            .Distinct()
            .ToListAsync(ct);

    private async Task<AmcResult?> CheckReferencesAsync(long vendorId, List<long> assetIds, CancellationToken ct)
    {
        try
        {
            IReadOnlyDictionary<long, ContactSummary> vendors = await _contacts.FindAsync([vendorId], ct);
            if (!vendors.TryGetValue(vendorId, out ContactSummary? vendor) || !vendor.IsVendor || !vendor.IsActive)
            {
                return AmcResult.Fail(AmcOutcome.Invalid, "Choose an active vendor of this branch.");
            }

            if (assetIds.Count > 0)
            {
                FacilityLookupResponse found = await _facility.LookupAsync(assetIds, [], ct);
                if (assetIds.Any(id => !found.Assets.Any(a => a.Id == id && a.IsUsable)))
                {
                    return AmcResult.Fail(AmcOutcome.Invalid, "Choose assets in use in this branch.");
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "An AMC contract's vendor or assets could not be checked.");
            return AmcResult.Fail(AmcOutcome.Unavailable);
        }

        return null;
    }

    private async Task<List<ContractView>> ViewsAsync(List<AmcContract> rows, DateOnly today, CancellationToken ct)
    {
        List<long> ids = [.. rows.Select(r => r.AmcContractId)];
        var assets = (await _db.AmcCoveredAssets.AsNoTracking().Where(a => ids.Contains(a.AmcContractId))
                .Select(a => new { a.AmcContractId, a.FacilityAssetId }).ToListAsync(ct))
            .ToLookup(a => a.AmcContractId, a => a.FacilityAssetId);
        var visits = await _db.AmcVisits.AsNoTracking().Where(v => ids.Contains(v.AmcContractId))
            .GroupBy(v => v.AmcContractId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return [.. rows.Select(c => new ContractView
        {
            AmcContractId = c.AmcContractId,
            ContractNo = c.ContractNo,
            VendorContactId = c.VendorContactId,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            ContractValue = c.ContractValue,
            BillingFrequency = c.BillingFrequency,
            VisitsPerYear = c.VisitsPerYear,
            AmcCoverage = c.AmcCoverage,
            RenewalReminderDays = c.RenewalReminderDays,
            ReminderEmail = c.ReminderEmail,
            ContractStatus = AmcRules.Effective(c.ContractStatus, c.EndDate, today),
            TerminationReason = c.TerminationReason,
            Remarks = c.Remarks,
            FacilityAssetIds = [.. assets[c.AmcContractId]],
            VisitsMade = visits.GetValueOrDefault(c.AmcContractId),
        })];
    }
}
