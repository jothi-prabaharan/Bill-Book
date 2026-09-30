using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class RefreshToken : AuditableEntity
{
    public long RefreshTokenId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The branch this session is signed in to.
    ///
    /// <b>A refresh has to mint the same access token again</b>, and an access
    /// token carries <c>org_id</c> — so without this, refreshing would either
    /// have to ask the user which branch they were in (which is not a refresh)
    /// or pick one (which would silently move a signed-in user's books).
    /// </summary>
    public Guid OrgId { get; set; }

    /// <summary>
    /// The chain this token belongs to: one sign-in, then every token rotated
    /// out of it.
    ///
    /// <b>This is what makes reuse detection possible.</b> A refresh token is
    /// used once and replaced. If a token that was already spent is presented
    /// again, either it was stolen or the legitimate client is replaying — and
    /// there is no way to tell which, so the safe answer is to end the whole
    /// chain rather than the one token. Without a family id the best available
    /// response would be to refuse that single token and leave the thief holding
    /// the one that was rotated into its place.
    /// </summary>
    public Guid FamilyId { get; set; }

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
