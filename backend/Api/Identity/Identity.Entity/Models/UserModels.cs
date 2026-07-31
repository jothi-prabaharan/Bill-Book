using System.ComponentModel.DataAnnotations;

namespace Identity.Entity.Models;

public class UserListItem
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? MobileNumber { get; set; }

    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public DateTimeOffset? LastLoginAt { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Invited but has not set a password yet.</summary>
    public bool IsInvitePending { get; set; }

    public bool IsLockedOut { get; set; }
}

public class InviteUserRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    /// <summary>Optional. Mobile verification is not implemented yet.</summary>
    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Role is required.")]
    public int RoleId { get; set; }
}

/// <summary>Completes an invitation: the link token plus the chosen password.</summary>
public class AcceptInvitationRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Token is required.")]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = null!;
}
