namespace Inventory.Entity.Enums;

/// <summary>The days a timed menu runs on (TK-144). Monday first, the Indian working week.</summary>
[Flags]
public enum MenuDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Everyday = Weekdays | Weekend,
}
