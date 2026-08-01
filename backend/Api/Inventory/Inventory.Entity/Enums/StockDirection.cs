namespace Inventory.Entity.Enums;

/// <summary>
/// Which way a movement runs. Stored rather than left as a sign on the quantity,
/// for the same reason a journal line is debit <b>xor</b> credit and never a
/// negative amount: a signed column invites a bug that silently reverses a
/// movement, and a summed column cannot be read without knowing the convention.
/// </summary>
public enum StockDirection
{
    In = 0,
    Out = 1,
}
