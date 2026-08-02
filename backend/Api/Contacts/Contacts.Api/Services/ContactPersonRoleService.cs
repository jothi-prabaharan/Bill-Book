using Contacts.Entity.Models;
using Contacts.Entity.TableEntities;
using Contacts.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Ordering;

namespace Contacts.Api.Services;

/// <summary>
/// The contact person role master, maintained from a popup on the contact list.
/// Small enough that the whole list loads at once, so ordering is by hand rather
/// than by search.
/// </summary>
public sealed class ContactPersonRoleService
{
    private readonly ContactsDbContext _db;

    public ContactPersonRoleService(ContactsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContactPersonRoleListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<ContactPersonRole> query = _db.ContactPersonRoles;

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        List<ContactPersonRole> roles = await query
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.RoleName)
            .ToListAsync(ct);

        // One grouped query rather than a count per row — this list is rendered
        // in a popup and would otherwise fire a query per role.
        Dictionary<long, int> usage = await _db.ContactPersons
            .GroupBy(p => p.ContactPersonRoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        return roles.Select(r => new ContactPersonRoleListItem
        {
            ContactPersonRoleId = r.ContactPersonRoleId,
            RoleSystemName = r.RoleSystemName,
            RoleName = r.RoleName,
            DisplayOrder = r.DisplayOrder,
            IsDefault = r.IsDefault,
            IsSystem = r.IsSystem,
            IsActive = r.IsActive,
            UsageCount = usage.GetValueOrDefault(r.ContactPersonRoleId),
        }).ToList();
    }

    public async Task<SaveRoleOutcome> CreateAsync(
        SaveContactPersonRoleRequest request, CancellationToken ct)
    {
        if (await NameTakenAsync(request.RoleName, null, ct))
        {
            return SaveRoleOutcome.DuplicateName;
        }

        int highest = await _db.ContactPersonRoles
            .Select(r => (int?)r.DisplayOrder)
            .MaxAsync(ct) ?? 0;

        _db.ContactPersonRoles.Add(new ContactPersonRole
        {
            RoleSystemName = null,
            RoleName = request.RoleName.Trim(),
            DisplayOrder = highest + Reordering.Gap,
            IsDefault = false,
            IsSystem = false,
            IsActive = request.IsActive,
        });

        await _db.SaveChangesAsync(ct);
        return SaveRoleOutcome.Ok;
    }

    /// <summary>
    /// Renames and toggles active. Allowed on seeded roles too — the hidden
    /// RoleSystemName is what code matches on, so the label is free to change.
    /// </summary>
    public async Task<SaveRoleOutcome> UpdateAsync(
        long roleId, SaveContactPersonRoleRequest request, CancellationToken ct)
    {
        ContactPersonRole? role = await _db.ContactPersonRoles
            .FirstOrDefaultAsync(r => r.ContactPersonRoleId == roleId, ct);

        if (role is null)
        {
            return SaveRoleOutcome.NotFound;
        }

        if (await NameTakenAsync(request.RoleName, roleId, ct))
        {
            return SaveRoleOutcome.DuplicateName;
        }

        // Deactivating the default would leave new person rows preselecting an
        // inactive value.
        if (role.IsDefault && !request.IsActive)
        {
            return SaveRoleOutcome.DefaultRoleMustStayActive;
        }

        role.RoleName = request.RoleName.Trim();
        role.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return SaveRoleOutcome.Ok;
    }

    public async Task<SaveRoleOutcome> SetDefaultAsync(long roleId, CancellationToken ct)
    {
        ContactPersonRole? role = await _db.ContactPersonRoles
            .FirstOrDefaultAsync(r => r.ContactPersonRoleId == roleId, ct);

        if (role is null)
        {
            return SaveRoleOutcome.NotFound;
        }

        List<ContactPersonRole> previous = await _db.ContactPersonRoles
            .Where(r => r.IsDefault && r.ContactPersonRoleId != roleId)
            .ToListAsync(ct);

        foreach (ContactPersonRole row in previous)
        {
            row.IsDefault = false;
        }

        role.IsDefault = true;
        role.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return SaveRoleOutcome.Ok;
    }

    /// <summary>
    /// Deletes a role outright when nothing holds it. A role in use is refused
    /// rather than cascaded — the people holding it would vanish with it.
    /// </summary>
    public async Task<SaveRoleOutcome> DeleteAsync(long roleId, CancellationToken ct)
    {
        ContactPersonRole? role = await _db.ContactPersonRoles
            .FirstOrDefaultAsync(r => r.ContactPersonRoleId == roleId, ct);

        if (role is null)
        {
            return SaveRoleOutcome.NotFound;
        }

        if (role.IsSystem)
        {
            return SaveRoleOutcome.SystemRoleUndeletable;
        }

        bool inUse = await _db.ContactPersons.AnyAsync(p => p.ContactPersonRoleId == roleId, ct);
        if (inUse)
        {
            return SaveRoleOutcome.RoleInUse;
        }

        _db.ContactPersonRoles.Remove(role);
        await _db.SaveChangesAsync(ct);
        return SaveRoleOutcome.Ok;
    }

    public async Task<SaveRoleOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        List<ContactPersonRole> all = await _db.ContactPersonRoles
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.RoleName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, r => r.ContactPersonRoleId, r => r.DisplayOrder,
                (r, order) => r.DisplayOrder = order))
        {
            return SaveRoleOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveRoleOutcome.Ok;
    }

    /// <summary>
    /// Writes the default contact person roles for an organization, adding only
    /// what is missing. Safe to re-run: a role added to the seed list later
    /// reaches organizations created before it existed. Matched on
    /// <c>RoleSystemName</c>, which is what the rest of the code matches on too,
    /// so a renamed role is recognised as present rather than seeded again
    /// under its original label.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> existing = await _db.ContactPersonRoles
            .IgnoreQueryFilters()
            .Where(r => r.OrgId == orgId && r.RoleSystemName != null)
            .Select(r => r.RoleSystemName!)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing];

        // RoleName is unique per organization too, and this master is editable
        // from a popup — a customer-created "Dispatch" is entirely likely. Skip
        // the row rather than let a name clash throw the whole seeding call.
        List<string> names = await _db.ContactPersonRoles
            .IgnoreQueryFilters()
            .Where(r => r.OrgId == orgId)
            .Select(r => r.RoleName)
            .ToListAsync(ct);

        HashSet<string> taken = [.. names];

        List<ContactPersonRole> missing = Repository.SeedData.ContactPersonRolesSeed.Build(orgId)
            .Where(r => r.RoleSystemName is not null
                && !present.Contains(r.RoleSystemName)
                && !taken.Contains(r.RoleName))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        // One default per organization is a filtered unique index; only claim it
        // if it is going spare.
        if (await _db.ContactPersonRoles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.OrgId == orgId && r.IsDefault, ct))
        {
            foreach (ContactPersonRole role in missing)
            {
                role.IsDefault = false;
            }
        }

        _db.ContactPersonRoles.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }

    private Task<bool> NameTakenAsync(string roleName, long? exceptId, CancellationToken ct)
    {
        string name = roleName.Trim();
        return _db.ContactPersonRoles.AnyAsync(
            r => r.RoleName == name && (exceptId == null || r.ContactPersonRoleId != exceptId), ct);
    }
}
