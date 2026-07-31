namespace Accounting.Entity.Enums;

/// <summary>What a sub-account hangs off. Drives which master owns its lifecycle.</summary>
public enum SubAccountReferenceType
{
    Contact = 1,
    Item = 2,
    Tax = 3,
}
