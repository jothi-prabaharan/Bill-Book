using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Platform.Entity.Models;
using Platform.Entity.TableEntities;
using Platform.Repository;
using Shared.Kernel.Ordering;

namespace Platform.Api.Services;

/// <summary>
/// Branches for an organization.
///
/// Every query filters on the org id the controller has already checked against
/// the caller's claim. There is no global query filter here — <c>plt</c> is in
/// the master database, which holds every customer's organizations, so the
/// <c>Where(b =&gt; b.OrgId == orgId)</c> in each method is the whole boundary.
/// It is not optional in any of them.
/// </summary>
public sealed class BranchService
{
    private readonly PlatformDbContext _db;
    private readonly IStateDirectory _states;

    public BranchService(PlatformDbContext db, IStateDirectory states)
    {
        _db = db;
        _states = states;
    }

    public async Task<IReadOnlyList<BranchListItem>> ListAsync(
        Guid orgId, bool includeInactive, CancellationToken ct) =>
        await _db.Branches
            .Where(b => b.OrgId == orgId && (includeInactive || b.IsActive))
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.BranchName)
            .Select(ToListItem)
            .ToListAsync(ct);

    public async Task<BranchListItem?> GetAsync(Guid orgId, long branchId, CancellationToken ct) =>
        await _db.Branches
            .Where(b => b.OrgId == orgId && b.BranchId == branchId)
            .Select(ToListItem)
            .FirstOrDefaultAsync(ct);

    public async Task<SaveBranchResult> CreateAsync(
        Guid orgId, SaveBranchRequest request, CancellationToken ct)
    {
        if (!await _db.Organizations.AnyAsync(o => o.OrgId == orgId, ct))
        {
            return new SaveBranchResult(SaveBranchOutcome.OrganizationNotFound, null);
        }

        SaveBranchOutcome validation = await ValidateAsync(orgId, request, null, ct);
        if (validation != SaveBranchOutcome.Ok)
        {
            return new SaveBranchResult(validation, null);
        }

        // The first branch is the head office whatever the caller asked for:
        // an organization with branches and no head office has nowhere to put a
        // document that names none.
        bool isFirst = !await _db.Branches.AnyAsync(b => b.OrgId == orgId, ct);

        var branch = new Branch { OrgId = orgId };
        Apply(branch, request);
        branch.IsHeadOffice = isFirst || request.IsHeadOffice;
        branch.DisplayOrder = await NextDisplayOrderAsync(orgId, ct);

        if (branch.IsHeadOffice)
        {
            await ClearHeadOfficeAsync(orgId, null, ct);
        }

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);

        return new SaveBranchResult(SaveBranchOutcome.Ok, branch.BranchId);
    }

    public async Task<SaveBranchResult> UpdateAsync(
        Guid orgId, long branchId, SaveBranchRequest request, CancellationToken ct)
    {
        Branch? branch = await _db.Branches
            .FirstOrDefaultAsync(b => b.OrgId == orgId && b.BranchId == branchId, ct);

        if (branch is null)
        {
            return new SaveBranchResult(SaveBranchOutcome.NotFound, null);
        }

        SaveBranchOutcome validation = await ValidateAsync(orgId, request, branchId, ct);
        if (validation != SaveBranchOutcome.Ok)
        {
            return new SaveBranchResult(validation, null);
        }

        // Demoting or deactivating the head office without promoting another
        // leaves the organization with none. Refused rather than repaired,
        // because picking a replacement is not this code's decision.
        bool losingHeadOffice = branch.IsHeadOffice && (!request.IsHeadOffice || !request.IsActive);
        if (losingHeadOffice)
        {
            return new SaveBranchResult(SaveBranchOutcome.HeadOfficeRequired, null);
        }

        if (request.IsHeadOffice && !branch.IsHeadOffice)
        {
            await ClearHeadOfficeAsync(orgId, branchId, ct);
        }

        Apply(branch, request);
        branch.IsHeadOffice = request.IsHeadOffice;

        await _db.SaveChangesAsync(ct);
        return new SaveBranchResult(SaveBranchOutcome.Ok, branch.BranchId);
    }

    /// <summary>
    /// Deactivates. Branches are never deleted — documents, journal lines and
    /// numbering series all point at the id, and a deleted branch would leave
    /// them naming nothing.
    /// </summary>
    public async Task<SaveBranchOutcome> DeactivateAsync(
        Guid orgId, long branchId, CancellationToken ct)
    {
        Branch? branch = await _db.Branches
            .FirstOrDefaultAsync(b => b.OrgId == orgId && b.BranchId == branchId, ct);

        if (branch is null)
        {
            return SaveBranchOutcome.NotFound;
        }

        if (branch.IsHeadOffice)
        {
            return SaveBranchOutcome.HeadOfficeRequired;
        }

        branch.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return SaveBranchOutcome.Ok;
    }

    public async Task<SaveBranchOutcome> ReorderAsync(
        Guid orgId, ReorderRequest request, CancellationToken ct)
    {
        List<Branch> all = await _db.Branches
            .Where(b => b.OrgId == orgId)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.BranchName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, b => b.BranchId, b => b.DisplayOrder,
                (b, order) => b.DisplayOrder = order))
        {
            return SaveBranchOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveBranchOutcome.Ok;
    }

    /// <summary>
    /// Creates the head office for a brand-new organization, so a branch picker
    /// is never empty on day one. Idempotent: an organization that already has
    /// branches is left exactly as it is.
    /// </summary>
    public async Task SeedHeadOfficeAsync(Guid orgId, CancellationToken ct)
    {
        if (await _db.Branches.AnyAsync(b => b.OrgId == orgId, ct))
        {
            return;
        }

        Organization? org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.OrgId == orgId, ct);

        if (org is null)
        {
            return;
        }

        _db.Branches.Add(new Branch
        {
            OrgId = orgId,
            BranchCode = "HO",
            BranchName = "Head Office",
            IsHeadOffice = true,
            // The organization's own registration and address, because on day
            // one the head office is the organization.
            Gstin = org.Gstin,
            AddressLine1 = org.AddressLine1,
            AddressLine2 = org.AddressLine2,
            City = org.City,
            StateId = org.StateId,
            PostalCode = org.PostalCode,
            CountryId = org.CountryId,
            PhoneNumber = org.PhoneNumber,
            MobileNumber = org.MobileNumber,
            Email = org.Email,
            DisplayOrder = Reordering.Gap,
            IsActive = true,
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<SaveBranchOutcome> ValidateAsync(
        Guid orgId, SaveBranchRequest request, long? branchId, CancellationToken ct)
    {
        string code = request.BranchCode.Trim().ToUpperInvariant();
        string name = request.BranchName.Trim();

        if (await _db.Branches.AnyAsync(
                b => b.OrgId == orgId && b.BranchCode == code
                    && (branchId == null || b.BranchId != branchId), ct))
        {
            return SaveBranchOutcome.DuplicateCode;
        }

        if (await _db.Branches.AnyAsync(
                b => b.OrgId == orgId && b.BranchName == name
                    && (branchId == null || b.BranchId != branchId), ct))
        {
            return SaveBranchOutcome.DuplicateName;
        }

        // Same rule as a contact's GSTIN: the first two digits are the state
        // code, and a mismatch splits every document's tax the wrong way.
        if (!string.IsNullOrWhiteSpace(request.Gstin) && request.StateId is int stateId)
        {
            string? stateCode = await _states.GetStateCodeAsync(stateId, ct);
            if (stateCode is not null
                && !request.Gstin.Trim().StartsWith(stateCode, StringComparison.Ordinal))
            {
                return SaveBranchOutcome.GstinStateMismatch;
            }
        }

        return SaveBranchOutcome.Ok;
    }

    /// <summary>
    /// Clears the current head office before another is set, so the filtered
    /// unique index never sees two — the two writes are in one SaveChanges.
    /// </summary>
    private async Task ClearHeadOfficeAsync(Guid orgId, long? exceptBranchId, CancellationToken ct)
    {
        List<Branch> existing = await _db.Branches
            .Where(b => b.OrgId == orgId && b.IsHeadOffice
                && (exceptBranchId == null || b.BranchId != exceptBranchId))
            .ToListAsync(ct);

        foreach (Branch row in existing)
        {
            row.IsHeadOffice = false;
        }
    }

    private async Task<int> NextDisplayOrderAsync(Guid orgId, CancellationToken ct)
    {
        // Nullable cast rather than DefaultIfEmpty: Max over no rows returns
        // SQL NULL, which throws on a non-nullable int.
        int? highest = await _db.Branches
            .Where(b => b.OrgId == orgId)
            .MaxAsync(b => (int?)b.DisplayOrder, ct);

        return (highest ?? 0) + Reordering.Gap;
    }

    private static void Apply(Branch branch, SaveBranchRequest request)
    {
        branch.BranchCode = request.BranchCode.Trim().ToUpperInvariant();
        branch.BranchName = request.BranchName.Trim();
        branch.Gstin = Trimmed(request.Gstin)?.ToUpperInvariant();
        branch.AddressLine1 = Trimmed(request.AddressLine1);
        branch.AddressLine2 = Trimmed(request.AddressLine2);
        branch.City = Trimmed(request.City);
        branch.StateId = request.StateId;
        branch.PostalCode = Trimmed(request.PostalCode);
        branch.CountryId = request.CountryId;
        branch.PhoneNumber = Trimmed(request.PhoneNumber);
        branch.MobileNumber = Trimmed(request.MobileNumber);
        branch.Email = Trimmed(request.Email);
        branch.IsActive = request.IsActive;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// An expression rather than a method, so EF composes it into the SQL
    /// instead of pulling every column back and mapping in memory.
    /// </summary>
    private static readonly Expression<Func<Branch, BranchListItem>> ToListItem = b => new BranchListItem
    {
        BranchId = b.BranchId,
        BranchCode = b.BranchCode,
        BranchName = b.BranchName,
        IsHeadOffice = b.IsHeadOffice,
        Gstin = b.Gstin,
        AddressLine1 = b.AddressLine1,
        AddressLine2 = b.AddressLine2,
        City = b.City,
        StateId = b.StateId,
        PostalCode = b.PostalCode,
        CountryId = b.CountryId,
        PhoneNumber = b.PhoneNumber,
        MobileNumber = b.MobileNumber,
        Email = b.Email,
        DisplayOrder = b.DisplayOrder,
        IsActive = b.IsActive,
    };
}
