using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Identity.Entity.TableEntities;

public class LoginHistory : AuditableEntity
{
    public long LoginHistoryId { get; set; }

    public Guid UserId { get; set; }

    public Guid? OrgId { get; set; }

    public DateTimeOffset LoginAt { get; set; }

    public bool IsSuccessful { get; set; }

    [MaxLength(200, ErrorMessage = "Failure reason cannot exceed 200 characters.")]
    public string? FailureReason { get; set; }

    [MaxLength(45, ErrorMessage = "IP address cannot exceed 45 characters.")]
    public string? IpAddress { get; set; }

    [MaxLength(300, ErrorMessage = "User agent cannot exceed 300 characters.")]
    public string? UserAgent { get; set; }
}
