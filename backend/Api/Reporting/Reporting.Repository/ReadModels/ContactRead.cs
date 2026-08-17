using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>con.Contacts</c>, read-only. One master with roles: the same row is a
/// customer, a vendor, or both, according to its flags.
///
/// <b>This is a different schema in the same physical database</b> — the
/// customer's own — which is why it can be joined at all. The master database's
/// tables (countries, states, users) cannot, and have to be resolved in C#.
/// </summary>
public class ContactRead : OrgScopedEntity
{
    public long ContactId { get; set; }

    public string ContactCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? LegalName { get; set; }

    public bool IsCustomer { get; set; }

    public bool IsVendor { get; set; }

    public string? Gstin { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public long? PaymentTermId { get; set; }

    public int? PlaceOfSupplyStateId { get; set; }

    public bool IsActive { get; set; }
}
