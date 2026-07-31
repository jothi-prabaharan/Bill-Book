using Contacts.Entity.Models;
using Contacts.Entity.TableEntities;
using Contacts.Repository;
using Microsoft.EntityFrameworkCore;

namespace Contacts.Api.Services;

/// <summary>
/// The contact person role master, maintained from a popup on the contact list.
/// Small enough that the whole list loads at once, so ordering is by hand rather
/// than by search.
/// </summary>
public sealed class ContactPersonRoleService
{
    private const int OrderGap = 10;

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
            DisplayOrder = highest + OrderGap,
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
        ContactPersonRole? moved = await _db.ContactPersonRoles
            .FirstOrDefaultAsync(r => r.ContactPersonRoleId == request.MovedId, ct);

        if (moved is null)
        {
            return SaveRoleOutcome.NotFound;
        }

        int? above = await OrderOfAsync(request.PreviousId, ct);
        int? below = await OrderOfAsync(request.NextId, ct);

        if (above is int a && below is int b && b - a >= 2)
        {
            moved.DisplayOrder = a + ((b - a) / 2);
            await _db.SaveChangesAsync(ct);
            return SaveRoleOutcome.Ok;
        }

        if (above is int last && below is null)
        {
            moved.DisplayOrder = last + OrderGap;
            await _db.SaveChangesAsync(ct);
            return SaveRoleOutcome.Ok;
        }

        if (above is null && below is int first && first > 0)
        {
            moved.DisplayOrder = first / 2;
            await _db.SaveChangesAsync(ct);
            return SaveRoleOutcome.Ok;
        }

        await RenumberAsync(moved, request, ct);
        return SaveRoleOutcome.Ok;
    }

    /// <summary>Writes the default contact person roles for a newly created organization.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (await _db.ContactPersonRoles.IgnoreQueryFilters().AnyAsync(r => r.OrgId == orgId, ct))
        {
            return 0;
        }

        IReadOnlyList<ContactPersonRole> seed = Repository.SeedData.ContactPersonRolesSeed.Build(orgId);
        _db.ContactPersonRoles.AddRange(seed);
        await _db.SaveChangesAsync(ct);
        return seed.Count;
    }

    private async Task RenumberAsync(
        ContactPersonRole moved, ReorderRequest request, CancellationToken ct)
    {
        List<ContactPersonRole> all = await _db.ContactPersonRoles
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.RoleName)
            .ToListAsync(ct);

        all.Remove(moved);

        int insertAt;
        if (request.NextId is long nextId)
        {
            int index = all.FindIndex(r => r.ContactPersonRoleId == nextId);
            insertAt = index < 0 ? all.Count : index;
        }
        else if (request.PreviousId is long previousId)
        {
            int index = all.FindIndex(r => r.ContactPersonRoleId == previousId);
            insertAt = index < 0 ? all.Count : index + 1;
        }
        else
        {
            insertAt = 0;
        }

        all.Insert(insertAt, moved);

        for (int i = 0; i < all.Count; i++)
        {
            all[i].DisplayOrder = (i + 1) * OrderGap;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int?> OrderOfAsync(long? roleId, CancellationToken ct)
    {
        if (roleId is not long id)
        {
            return null;
        }

        ContactPersonRole? row = await _db.ContactPersonRoles
            .FirstOrDefaultAsync(r => r.ContactPersonRoleId == id, ct);

        return row?.DisplayOrder;
    }

    private Task<bool> NameTakenAsync(string roleName, long? exceptId, CancellationToken ct)
    {
        string name = roleName.Trim();
        return _db.ContactPersonRoles.AnyAsync(
            r => r.RoleName == name && (exceptId == null || r.ContactPersonRoleId != exceptId), ct);
    }
}
