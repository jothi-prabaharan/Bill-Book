using System;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

public class DepreciationSchedule : OrgScopedEntity
{
    public long DepreciationScheduleId { get; set; }

    public long FixedAssetId { get; set; }

    public DepreciationScheduleType ScheduleType { get; set; }

    public DepreciationMethod DepreciationMethod { get; set; }

    public decimal Rate { get; set; }
    
    public int UsefulLifeYears { get; set; }

    public DateOnly DepreciationStartDate { get; set; }
    
    public decimal SalvageValue { get; set; }
}
