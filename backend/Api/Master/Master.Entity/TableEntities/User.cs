using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class User : AuditableEntity
{
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    /// <summary>BCrypt hash (work factor 12). Null/empty for invited users until they set one.</summary>
    [MaxLength(500, ErrorMessage = "Password hash cannot exceed 500 characters.")]
    public string? PasswordHash { get; set; }

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    public bool EmailConfirmed { get; set; }

    public bool MobileConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockedOutUntil { get; set; }

    public Guid? LastAccessedOrgId { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Runs the platform itself, across every customer (D-01). When set, sign-in
    /// puts every <c>platform.*</c> permission in the token — the only way any
    /// token carries one, because <c>Role</c> rows are shared across customers
    /// and a role granting <c>platform.*</c> would grant it to that role's
    /// holders everywhere. Set only by <c>Bootstrap:OperatorEmails</c> at startup
    /// or by an existing operator; no tenant screen can reach it.
    /// </summary>
    public bool IsPlatformOperator { get; set; }
}
