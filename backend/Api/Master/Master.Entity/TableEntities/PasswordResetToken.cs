using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// Link-based token for user invitations (7-day expiry). Forgot-password uses
/// OTP instead — see <see cref="OtpVerification"/>.
/// </summary>
public class PasswordResetToken : AuditableEntity
{
    public long PasswordResetTokenId { get; set; }

    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Token hash is required.")]
    [MaxLength(500, ErrorMessage = "Token hash cannot exceed 500 characters.")]
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Single-use — set when the token is consumed.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
