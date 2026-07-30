using System.ComponentModel.DataAnnotations;

namespace Identity.Entity.Models;

/// <summary>
/// Internal request from the Platform provisioner: create the tenant's owner
/// user with the Owner role on the first organization.
/// </summary>
public class CreateOwnerUserRequest
{
    [Required(ErrorMessage = "Organization id is required.")]
    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = null!;
}
