using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// A registration the contact holds, with the date it stops being valid.
///
/// The expiry is the reason this is a table rather than two columns on the
/// contact: a pharma distributor holds several licences with different dates,
/// and selling Schedule H stock to one whose licence has lapsed is an offence.
/// </summary>
public class ContactLicence : OrgScopedEntity
{
    public long ContactLicenceId { get; set; }

    public long ContactId { get; set; }

    public LicenceType LicenceType { get; set; } = LicenceType.DrugLicence;

    [Required(ErrorMessage = "Licence number is required.")]
    [MaxLength(50, ErrorMessage = "Licence number cannot exceed 50 characters.")]
    public string LicenceNumber { get; set; } = null!;

    /// <summary>Free text — "Form 20B", "Retail", the issuing authority's own wording.</summary>
    [MaxLength(100, ErrorMessage = "Description cannot exceed 100 characters.")]
    public string? Description { get; set; }

    [MaxLength(100, ErrorMessage = "Issuing authority cannot exceed 100 characters.")]
    public string? IssuingAuthority { get; set; }

    public DateOnly? IssuedOn { get; set; }

    /// <summary>Null means it does not expire. Everything else is checked against it.</summary>
    public DateOnly? ExpiresOn { get; set; }

    public bool IsActive { get; set; } = true;
}
