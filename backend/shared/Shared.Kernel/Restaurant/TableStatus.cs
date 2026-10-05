namespace Shared.Kernel.Restaurant;

/// <summary>Where a restaurant table is in its turn (TK-145): seated, billed, being cleared.</summary>
public enum TableStatus
{
    Free = 1,
    Occupied = 2,
    Billed = 3,
    Cleaning = 4,
}
