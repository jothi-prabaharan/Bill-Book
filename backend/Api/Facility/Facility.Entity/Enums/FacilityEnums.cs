namespace Facility.Entity.Enums;

// The fac schema's fixed sets (S5, TK-65), stored by name.

public enum SpaceKind
{
    Classroom = 1,
    Laboratory = 2,
    Office = 3,
    Toilet = 4,
    Hall = 5,
    Playground = 6,
    Store = 7,
    Other = 8,
}

public enum AssetCategory
{
    Electrical = 1,
    Plumbing = 2,
    Hvac = 3,
    Furniture = 4,
    It = 5,
    Lab = 6,
    Vehicle = 7,
    Civil = 8,
    Other = 9,
}

/// <summary>Where an asset stands. A disposed asset is kept, and takes no work orders.</summary>
public enum AssetStatus
{
    InUse = 1,
    UnderRepair = 2,
    Idle = 3,
    Disposed = 4,
}
