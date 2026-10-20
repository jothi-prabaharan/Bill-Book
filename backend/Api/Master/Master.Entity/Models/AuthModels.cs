using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;

namespace Master.Entity.Models;

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

    /// <summary>The organisation (customer) the branch belongs to; the switcher groups by it.</summary>
    public string? CustomerName { get; set; }

    /// <summary>The branch's trade, as a <c>BusinessType</c> name: Retail, Pharma, Jewellery, School, Service or Restaurant.</summary>
    public string BusinessType { get; set; } = "Retail";

    /// <summary>The currency the branch keeps its books in.</summary>
    public string? BaseCurrency { get; set; }

    /// <summary>The branch's GSTIN, so the switcher can be searched by it. Null on an unregistered branch.</summary>
    public string? Gstin { get; set; }

    public string? City { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? PostalCode { get; set; }

    public int? StateId { get; set; }
}

public class SessionStateResponse
{
    public Guid? LastAccessedOrgId { get; set; }
    public IReadOnlyList<AccessibleOrgDto> Organizations { get; set; } = new List<AccessibleOrgDto>();
}

public class LoginResponse : TokenResponse
{
    public Guid CurrentOrgId { get; set; }

    public IReadOnlyList<AccessibleOrgDto> Organizations { get; set; } = new List<AccessibleOrgDto>();
}

// ---- Organization selection ---------------------------------------------

public class SelectOrganizationRequest
{
    [Required(ErrorMessage = "Organization id is required.")]
    public Guid OrgId { get; set; }
}

/// <summary>
/// A refresh token presented for rotation, or for logout.
///
/// <b>In the body, not a header or a query string.</b> A credential in a URL is
/// written to every access log and proxy cache between the client and here.
/// </summary>
public class RefreshRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    [MaxLength(200, ErrorMessage = "Refresh token cannot exceed 200 characters.")]
    public string RefreshToken { get; set; } = null!;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;

    public int AccessExpiresInSeconds { get; set; }

    /// <summary>Trial / Active / Expired — the shell gates on this.</summary>
    public string LicenseStatus { get; set; } = null!;

    /// <summary>
    /// When access to the branch just selected ends — the earlier of the
    /// customer's licence expiry and the branch's own.
    /// </summary>
    public DateOnly? LicenseExpiry { get; set; }

    /// <summary>
    /// True when it is the branch's date. The expired page reads it to choose
    /// between asking the customer to renew and telling them the branch itself
    /// has closed.
    /// </summary>
    public bool ExpiryIsBranchLevel { get; set; }
}

// ---- Forgot password (OTP) ----------------------------------------------

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// How the code is sent: by email by default (TK-197), or on WhatsApp or by
    /// SMS (TK-196). A WhatsApp or SMS code goes to the mobile number on the
    /// account; with none there, nothing is sent and the answer is the same 200.
    /// </summary>
    [EnumDataType(typeof(OtpChannel), ErrorMessage = "Choose email, WhatsApp or SMS for the code.")]
    public OtpChannel Channel { get; set; } = OtpChannel.Email;
}

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(6, ErrorMessage = "Code must be 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
    public string Code { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(6, ErrorMessage = "Code must be 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(200, ErrorMessage = "Password cannot exceed 200 characters.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required.")]
    [MaxLength(200, ErrorMessage = "Password cannot exceed 200 characters.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = null!;
}

/// <summary>A generic 200 that never reveals whether an account exists.</summary>
public class MessageResponse
{
    public string Message { get; set; } = null!;
}
