using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// Billing and shipping addresses. The reason this is a table rather than
/// columns on the contact: for goods the place of supply follows the delivery
/// address, so a customer with a godown in another state changes the tax split
/// on those invoices.
/// </summary>
public class ContactAddress : OrgScopedEntity
{
    public long ContactAddressId { get; set; }

    public long ContactId { get; set; }

    /// <summary>What the address is: registered office, warehouse, branch (TK-188).</summary>
    public AddressKind Kind { get; set; } = AddressKind.RegisteredOffice;

    /// <summary>
    /// Where documents bill this contact. One per contact, enforced by a
    /// filtered unique index; it may be the default shipping address too.
    /// </summary>
    public bool IsDefaultBilling { get; set; }

    /// <summary>
    /// Where documents deliver to this contact, and so, for goods, the place of
    /// supply. One per contact, enforced by a filtered unique index.
    /// </summary>
    public bool IsDefaultShipping { get; set; }

    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string? Label { get; set; }

    [Required(ErrorMessage = "Address line 1 is required.")]
    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string AddressLine1 { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "Landmark cannot exceed 100 characters.")]
    public string? Landmark { get; set; }

    [Required(ErrorMessage = "City is required.")]
    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string City { get; set; } = null!;

    /// <summary>Unenforced reference to mst.States.</summary>
    public int? StateId { get; set; }

    /// <summary>Unenforced reference to mst.Countries.</summary>
    public int CountryId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    /// <summary>A branch with its own registration. Its first two digits must match this state.</summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? Gstin { get; set; }

    // Who to ask for at this address is a ContactPerson whose ContactAddressId
    // names it (TK-188), so a name, role, phone and email live in one place.

    public bool IsActive { get; set; } = true;
}
