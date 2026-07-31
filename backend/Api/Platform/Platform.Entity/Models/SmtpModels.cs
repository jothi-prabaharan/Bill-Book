using System.ComponentModel.DataAnnotations;

namespace Platform.Entity.Models;

/// <summary>
/// SMTP settings for display. The password is deliberately absent — it is
/// write-only and never returned to a client.
/// </summary>
public class SmtpSettingsDto
{
    public Guid? SmtpSettingsId { get; set; }

    /// <summary>Null = the platform-wide default row.</summary>
    public Guid? CustomerId { get; set; }

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public bool UseSsl { get; set; }

    public string FromEmail { get; set; } = null!;

    public string FromName { get; set; } = null!;

    public string Username { get; set; } = null!;

    /// <summary>True when a password is stored, so the UI can show ••••••.</summary>
    public bool HasPassword { get; set; }

    public bool IsActive { get; set; }

    /// <summary>True when this customer has no row of its own and inherits the default.</summary>
    public bool IsInherited { get; set; }
}

public class SaveSmtpSettingsRequest
{
    [Required(ErrorMessage = "Host is required.")]
    [MaxLength(200, ErrorMessage = "Host cannot exceed 200 characters.")]
    public string Host { get; set; } = null!;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; set; } = 587;

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
    /// Write-only. Leave null to keep the stored password unchanged; supply a
    /// value to replace it. It is encrypted before storage, never hashed.
    /// </summary>
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SendTestEmailRequest
{
    [Required(ErrorMessage = "Recipient is required.")]
    [EmailAddress(ErrorMessage = "Recipient must be a valid email address.")]
    public string ToEmail { get; set; } = null!;
}

/// <summary>Internal service-to-service send request.</summary>
public class SendEmailRequest
{
    [Required(ErrorMessage = "Recipient is required.")]
    [EmailAddress(ErrorMessage = "Recipient must be a valid email address.")]
    public string ToEmail { get; set; } = null!;

    public string? ToName { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    public string Subject { get; set; } = null!;

    [Required(ErrorMessage = "Body is required.")]
    public string HtmlBody { get; set; } = null!;

    public string? TextBody { get; set; }

    /// <summary>Null uses the platform default mailbox.</summary>
    public Guid? CustomerId { get; set; }
}
