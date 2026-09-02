using System;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

public class ReminderProfile : OrgScopedEntity
{
    public int ReminderProfileId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProfileName { get; set; } = null!;

    public int DaysOverdueTrigger { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ReminderLog : OrgScopedEntity
{
    public long ReminderLogId { get; set; }

    public long InvoiceId { get; set; }

    public int ReminderProfileId { get; set; }

    public DateTimeOffset SentAt { get; set; }

    [MaxLength(50)]
    public string NotificationType { get; set; } = "Email";
}
