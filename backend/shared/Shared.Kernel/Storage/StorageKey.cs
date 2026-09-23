using System.Text.RegularExpressions;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Storage;

/// <summary>
/// The product a stored file belongs to — the third folder of every key.
///
/// Only Retail ERP exists today. HRMS &amp; Payroll and School are designed and
/// will add members when they are built; their stage H0 introduces an App flag
/// on roles, permissions and menus, and this should become that same enum
/// rather than a second list that has to agree with it.
/// </summary>
public enum StorageApp
{
    RetailErp = 1,
}

/// <summary>
/// The module a stored file belongs to — the fourth folder of every key.
/// Contacts is its own module here even though the Master service serves it,
/// because that is how a person looking for a GST certificate thinks of it.
/// </summary>
public enum StorageModule
{
    Master = 1,
    Contacts = 2,
    Accounting = 3,
    Inventory = 4,
    Sales = 5,
    Purchase = 6,
    Customer = 7,
    Reporting = 8,
    Printing = 9,
}

/// <summary>
/// Whose file, and which part of the product it belongs to: the four folders
/// every key starts with.
/// </summary>
public sealed record StorageScope(string CustomerCode, Guid OrgId, StorageApp App, StorageModule Module)
{
    /// <summary>
    /// The scope for the current request. Refuses rather than guessing when the
    /// token carries no <c>customer_code</c> — a token minted before the claim
    /// existed — because filing the file somewhere else would scatter one
    /// customer's documents across two layouts with nothing to reconcile them.
    /// Signing in again issues a token that has it.
    /// </summary>
    public static StorageScope For(ITenantContext tenant, StorageApp app, StorageModule module)
    {
        (Guid _, Guid orgId) = tenant.Require();

        if (string.IsNullOrWhiteSpace(tenant.CustomerCode))
        {
            throw new InvalidOperationException(
                "This request's token carries no customer_code, which every stored file is " +
                "filed under. Signing in again issues one that does.");
        }

        return new StorageScope(tenant.CustomerCode, orgId, app, module);
    }
}

/// <summary>
/// Composes every storage key, and is the only thing that should:
///
/// <code>
/// {customerCode}/{orgId}/{app}/{module}/{area}/…
/// 0000000042/3f2c…/retail-erp/contacts/attachments/17/9ab1….pdf
/// 0000000042/3f2c…/retail-erp/sales/invoices/5521.pdf
/// </code>
///
/// <b>Why this order.</b> Customer first, because that is the unit an operator
/// looks for, deletes on offboarding and exports on request — and the code
/// rather than the id because a person can read it. Then the branch, because a
/// branch is a separate set of books. Then product and module, so HRMS and
/// School slot in beside Retail ERP without moving anything that exists.
///
/// <b>Every segment is validated.</b> Blob Storage accepts any string as a name,
/// so nothing downstream would catch a customer code with a slash in it or a
/// document name of "../x". Refusing here is what keeps one customer's prefix
/// from reaching into another's.
///
/// <b>Folder names are fixed text, not derived from the enum names.</b>
/// Renaming <see cref="StorageModule.Customer"/> in code must not quietly start
/// filing new documents under a different folder from the old ones.
/// </summary>
public static class StorageKey
{
    // Letters, digits, dot, underscore and hyphen; must not start with a dot.
    // Deliberately narrower than Blob Storage allows: a segment that needs
    // escaping anywhere is a segment that will be mishandled somewhere.
    private static readonly Regex SafeSegment =
        new("^[A-Za-z0-9_-][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// A key for an uploaded file:
    /// <c>{prefix}/{area}/{ownerId}/{guid}{extension}</c>. The random component
    /// means an uploaded name never decides where the file lands.
    /// </summary>
    public static string BuildKey(StorageScope scope, string area, long ownerId, string fileName)
    {
        string extension = Path.GetExtension(fileName);

        // Guard rather than trust: an extension is only ever appended to a name
        // this method generates, but a caller-supplied one containing a slash
        // would still escape the prefix.
        if (extension.Contains('/') || extension.Contains('\\') || extension.Length > 16)
        {
            extension = string.Empty;
        }

        return $"{Prefix(scope)}/{Segment(area, nameof(area))}/{ownerId}/{Guid.NewGuid():N}{extension}";
    }

    /// <summary>
    /// A key for a document the system generates under a name it chooses, such
    /// as an archived invoice: <c>{prefix}/{area}/{documentName}</c>. Writing
    /// the same document twice replaces it, and blob versioning keeps the
    /// earlier copy.
    /// </summary>
    public static string DocumentKey(StorageScope scope, string area, string documentName) =>
        $"{Prefix(scope)}/{Segment(area, nameof(area))}/{Segment(documentName, nameof(documentName))}";

    /// <summary>
    /// <c>{customerCode}/{orgId}/{app}/{module}</c> — the folder holding
    /// everything one module stores for one branch.
    /// </summary>
    public static string Prefix(StorageScope scope) =>
        $"{Segment(scope.CustomerCode, "customerCode")}/{scope.OrgId}/{Folder(scope.App)}/{Folder(scope.Module)}";

    public static string Folder(StorageApp app) => app switch
    {
        StorageApp.RetailErp => "retail-erp",
        _ => throw new ArgumentOutOfRangeException(nameof(app), app, "No folder is defined for this app."),
    };

    public static string Folder(StorageModule module) => module switch
    {
        StorageModule.Master => "master",
        StorageModule.Contacts => "contacts",
        StorageModule.Accounting => "accounting",
        StorageModule.Inventory => "inventory",
        StorageModule.Sales => "sales",
        StorageModule.Purchase => "purchase",
        StorageModule.Customer => "customer",
        StorageModule.Reporting => "reporting",
        StorageModule.Printing => "printing",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, "No folder is defined for this module."),
    };

    private static string Segment(string value, string name)
    {
        if (value is null || !SafeSegment.IsMatch(value) || value.Contains(".."))
        {
            throw new ArgumentException(
                $"'{value}' cannot be a storage path segment. Segments are 1-128 letters, digits, " +
                "dots, underscores and hyphens, and may not start with a dot or contain '..'.",
                name);
        }

        return value;
    }
}
