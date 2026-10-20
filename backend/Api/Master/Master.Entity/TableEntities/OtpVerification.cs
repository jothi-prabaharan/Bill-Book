using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// A short numeric code sent by email, SMS or WhatsApp. Backs OTP-based
/// forgot-password and the check on a trial signup (TK-196, TK-197), and is
/// reusable for email/mobile confirmation.
/// </summary>
public class OtpVerification : AuditableEntity
{
    public long OtpVerificationId { get; set; }

    /// <summary>
    /// The account the code is for. Null for a <see cref="OtpPurpose.TrialSignup"/>
    /// code, which is sent before the account exists and is found by
    /// <see cref="Destination"/> instead (TK-196).
    /// </summary>
    public Guid? UserId { get; set; }

    public OtpPurpose Purpose { get; set; }

    public OtpChannel Channel { get; set; }

    /// <summary>
    /// Where the code went: the email, or the masked mobile, for a password
    /// reset; for a trial signup the whole address, lower-cased, or the whole
    /// mobile number, because with no user yet that is what the code is matched
    /// on, and a signup is accepted only for the address or number that proved it.
    /// </summary>
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
