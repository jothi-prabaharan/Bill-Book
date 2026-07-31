using Contacts.Entity.TableEntities;

namespace Contacts.Repository.SeedData;

/// <summary>
/// The contact person roles written when an organization is created. Renamable,
/// never deletable — code matches on RoleSystemName, so relabelling "Accounts"
/// to "Accounts &amp; Finance" changes the screen and nothing else.
/// </summary>
public static class ContactPersonRolesSeed
{
    public static IReadOnlyList<ContactPersonRole> Build(Guid orgId) =>
    [
        Role(orgId, 10, "PRIMARY", "Primary", isDefault: true),
        Role(orgId, 20, "OWNER", "Owner / Proprietor"),
        Role(orgId, 30, "ACCOUNTS", "Accounts"),
        Role(orgId, 40, "PURCHASE", "Purchase"),
        Role(orgId, 50, "SALES", "Sales"),
        Role(orgId, 60, "DISPATCH", "Dispatch"),
        Role(orgId, 70, "SUPPORT", "Support"),
        Role(orgId, 80, "OTHER", "Other"),
    ];

    private static ContactPersonRole Role(
        Guid orgId, int displayOrder, string systemName, string name, bool isDefault = false) =>
        new()
        {
            OrgId = orgId,
            RoleSystemName = systemName,
            RoleName = name,
            DisplayOrder = displayOrder,
            IsDefault = isDefault,
            IsSystem = true,
            IsActive = true,
        };
}
