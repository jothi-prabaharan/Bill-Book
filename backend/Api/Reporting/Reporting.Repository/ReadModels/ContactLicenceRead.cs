#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>con.ContactLicences</c>, read-only. A contact's drug licence, FSSAI
/// registration or other permit.
///
/// <b>A contact has many, so a report showing one must choose which.</b> Account
/// Transaction's "Permit No" is the contact's active licence; a contact with two
/// active licences is real (a pharmacy holds separate retail and wholesale drug
/// licences), so the report takes the first by id rather than pretending there is
/// only one. Showing an expired permit against a posting would be worse than
/// showing nothing, which is why <c>IsActive</c> and <c>ExpiresOn</c> are both
/// carried rather than just the number.
/// </summary>
public class ContactLicenceRead : OrgScopedEntity
{
    public long ContactLicenceId { get; set; }

    public long ContactId { get; set; }

    /// <summary>Matches <c>LicenceType</c> in Master.</summary>
    public int LicenceType { get; set; }

    public string LicenceNumber { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public bool IsActive { get; set; }
}
