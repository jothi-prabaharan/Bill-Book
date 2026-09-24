using Customer.Repository;
using Inventory.Repository;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Turns every empty-string phone into NULL, once, at startup (D-04, TK-21).
///
/// <b>LINQ rather than a data migration.</b> The card asked for one migration
/// per schema, but a migration can only update rows in raw SQL, which hard rule
/// 1 does not allow for this. <see cref="Shared.Kernel.Validation.PhoneNumbers"/>
/// stops new blanks at every write; this clears the ones written before it. It
/// is idempotent — each statement touches only rows still holding <c>''</c> —
/// so running on every start costs six indexed-nothing updates.
///
/// It runs where Master already migrates every tenant schema. The tenant tables
/// are read past the query filter because no branch is chosen at startup, which
/// is the same thing the seeding beside it does; row-level security still
/// applies, so on a deployment whose login does not bypass it this clears only
/// what a superuser-run migration could see. There is no released deployment
/// with data yet, so the rows it exists for are in developers' databases.
/// </summary>
public static class BlankPhoneBackfill
{
    public static async Task<int> RunAdminAsync(AdminDbContext db, CancellationToken ct)
    {
        int cleared = 0;

        cleared += await db.Users.Where(u => u.MobileNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MobileNumber, (string?)null), ct);
        cleared += await db.Organizations.Where(o => o.PhoneNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PhoneNumber, (string?)null), ct);
        cleared += await db.Organizations.Where(o => o.MobileNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.MobileNumber, (string?)null), ct);

        return cleared;
    }

    public static async Task<int> RunTenantAsync(
        ContactsDbContext contacts, InventoryDbContext inventory, CustomerDbContext customer, CancellationToken ct)
    {
        int cleared = 0;

        cleared += await contacts.ContactAddresses.IgnoreQueryFilters().Where(a => a.PhoneNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PhoneNumber, (string?)null), ct);
        cleared += await contacts.ContactAddresses.IgnoreQueryFilters().Where(a => a.MobileNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MobileNumber, (string?)null), ct);
        cleared += await contacts.ContactPersons.IgnoreQueryFilters().Where(p => p.PhoneNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PhoneNumber, (string?)null), ct);
        cleared += await contacts.ContactPersons.IgnoreQueryFilters().Where(p => p.MobileNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.MobileNumber, (string?)null), ct);
        cleared += await inventory.Warehouses.IgnoreQueryFilters().Where(w => w.PhoneNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.PhoneNumber, (string?)null), ct);
        cleared += await inventory.Warehouses.IgnoreQueryFilters().Where(w => w.MobileNumber == "")
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.MobileNumber, (string?)null), ct);
        cleared += await customer.Leads.IgnoreQueryFilters().Where(l => l.Phone == "")
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Phone, (string?)null), ct);

        return cleared;
    }
}
