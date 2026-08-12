namespace Master.Entity.Enums;

/// <summary>Provisioning state of a customer's physical database.</summary>
public enum ProvisioningStatus
{
    Provisioning = 1,
    Ready = 2,
    Failed = 3,
}
