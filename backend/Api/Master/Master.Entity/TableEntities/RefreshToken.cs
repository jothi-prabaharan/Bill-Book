using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class RefreshToken : AuditableEntity
{
    public long RefreshTokenId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the token — never the plaintext.</summary>
    [Required(ErrorMessage = "Token hash is required.")]
    [MaxLength(500, ErrorMessage = "Token hash cannot exceed 500 characters.")]
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on rotation, logout, or password reset.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    [MaxLength(45, ErrorMessage = "IP address cannot exceed 45 characters.")]
    public string? IpAddress { get; set; }

    [MaxLength(300, ErrorMessage = "User agent cannot exceed 300 characters.")]
    public string? UserAgent { get; set; }
}
