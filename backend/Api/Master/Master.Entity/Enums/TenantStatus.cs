namespace Master.Entity.Enums;

/// <summary>Lifecycle status shared by Customer and Organization. `Expired` applies to Customer only.</summary>
public enum TenantStatus
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    Trial = 4,
    Expired = 5,
}
