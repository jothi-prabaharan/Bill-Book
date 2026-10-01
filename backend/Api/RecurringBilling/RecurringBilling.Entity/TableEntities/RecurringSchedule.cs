using Shared.Kernel.Tenancy;
using RecurringBilling.Entity.Enums;

namespace RecurringBilling.Entity.TableEntities;

public class RecurringSchedule : OrgScopedEntity
{
    public long RecurringScheduleId { get; set; }
    
    public ScheduleDocumentType DocumentType { get; set; }
    
    public long TemplateDocumentId { get; set; }
    
    public ScheduleInterval Interval { get; set; }
    
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    
    public DateOnly NextDueDate { get; set; }
    
    public ScheduleStatus Status { get; set; }
    
    public ScheduleRunStatus RunStatus { get; set; }
    public int RunAttempts { get; set; }
        public string? RunError { get; set; }
}

