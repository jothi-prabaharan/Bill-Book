using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Platform.Entity.TableEntities;

/// <summary>
/// Outbound mail account for invitations, OTPs and reset mail. One system
/// default (CustomerId null); a customer may override with its own mailbox.
/// </summary>
public class SmtpSettings : AuditableEntity
{
    public Guid SmtpSettingsId { get; set; }

    /// <summary>Null = system default. Set = this customer's own mailbox.</summary>
    public Guid? CustomerId { get; set; }

    [Required(ErrorMessage = "Host is required.")]
    [MaxLength(200, ErrorMessage = "Host cannot exceed 200 characters.")]
    public string Host { get; set; } = null!;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; set; }

    public bool UseSsl { get; set; } = true;

    [Required(ErrorMessage = "From email is required.")]
    [EmailAddress(ErrorMessage = "From email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "From email cannot exceed 200 characters.")]
    public string FromEmail { get; set; } = null!;

    [Required(ErrorMessage = "From name is required.")]
    [MaxLength(200, ErrorMessage = "From name cannot exceed 200 characters.")]
    public string FromName { get; set; } = null!;

    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(200, ErrorMessage = "Username cannot exceed 200 characters.")]
    public string Username { get; set; } = null!;

    /// <summary>
    /// AES-encrypted with a Key Vault key — NOT hashed. The worker recovers the
    /// plaintext to authenticate to the SMTP server. Never plaintext, never logged.
    /// </summary>
    [Required(ErrorMessage = "Password is required.")]
    [MaxLength(1000, ErrorMessage = "Encrypted password cannot exceed 1000 characters.")]
    public string PasswordEncrypted { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
