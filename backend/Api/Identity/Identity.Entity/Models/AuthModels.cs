using System.ComponentModel.DataAnnotations;
using Identity.Entity.Enums;

namespace Identity.Entity.Models;

// ---- Login ---------------------------------------------------------------

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = null!;
}

public class AccessibleOrgDto
{
    public Guid OrgId { get; set; }

    public string OrgName { get; set; } = null!;

    public string RoleName { get; set; } = null!;
}

public class LoginResponse
{
    public string PreAuthToken { get; set; } = null!;

    public int ExpiresInSeconds { get; set; }

    public IReadOnlyList<AccessibleOrgDto> Organizations { get; set; } = new List<AccessibleOrgDto>();

    /// <summary>False when the user has exactly one org — the client auto-selects it.</summary>
    public bool RequiresOrgSelection { get; set; }
}

// ---- Organization selection ---------------------------------------------

public class SelectOrganizationRequest
{
    [Required(ErrorMessage = "Organization id is required.")]
    public Guid OrgId { get; set; }
}

public class TokenResponse
{
    public string AccessToken { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;

    public int AccessExpiresInSeconds { get; set; }

    /// <summary>Trial / Active / Expired — the shell gates on this.</summary>
    public string LicenseStatus { get; set; } = null!;

    public DateOnly? LicenseExpiry { get; set; }
}

// ---- Forgot password (OTP) ----------------------------------------------

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = null!;

    public OtpChannel Channel { get; set; } = OtpChannel.Email;
}

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
    public string Code { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = null!;
}

/// <summary>A generic 200 that never reveals whether an account exists.</summary>
public class MessageResponse
{
    public string Message { get; set; } = null!;
}
