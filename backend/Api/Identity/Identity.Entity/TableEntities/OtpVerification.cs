using System.ComponentModel.DataAnnotations;
using Identity.Entity.Enums;
using Shared.Kernel.Entities;

namespace Identity.Entity.TableEntities;

/// <summary>
/// A short numeric code sent to email or mobile. Backs OTP-based
/// forgot-password, and reusable for email/mobile confirmation.
/// </summary>
public class OtpVerification : AuditableEntity
{
    public long OtpVerificationId { get; set; }

    public Guid UserId { get; set; }

    public OtpPurpose Purpose { get; set; }

    public OtpChannel Channel { get; set; }

    /// <summary>The email or masked mobile the code was sent to.</summary>
    [Required(ErrorMessage = "Destination is required.")]
    [MaxLength(200, ErrorMessage = "Destination cannot exceed 200 characters.")]
    public string Destination { get; set; } = null!;

    /// <summary>SHA-256 of the 6-digit code — never stored plaintext.</summary>
    [Required(ErrorMessage = "Code hash is required.")]
    [MaxLength(500, ErrorMessage = "Code hash cannot exceed 500 characters.")]
    public string CodeHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Locks after 5 wrong tries.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Single-use — set when the code is verified.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
