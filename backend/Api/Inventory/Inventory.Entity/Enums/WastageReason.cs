namespace Inventory.Entity.Enums;

/// <summary>Why food was thrown away (TK-149). Stored by name.</summary>
public enum WastageReason
{
    Spoiled = 1,
    Expired = 2,
    Burnt = 3,
    Dropped = 4,
    Overproduced = 5,
    ReturnedByGuest = 6,
    Other = 99,
}
