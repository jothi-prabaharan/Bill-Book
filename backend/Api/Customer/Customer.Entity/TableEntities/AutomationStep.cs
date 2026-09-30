using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class AutomationStep : OrgScopedEntity
{
    public long AutomationStepId { get; set; }

    public long AutomationId { get; set; }

    public int Sequence { get; set; }

    public AutomationStepKind Kind { get; set; }

    public int WaitDays { get; set; }

    public long? EmailTemplateId { get; set; }
}
