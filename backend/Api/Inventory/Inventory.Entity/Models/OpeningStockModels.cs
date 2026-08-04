using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

/// <summary>
/// Stock on the shelf at go-live, sent by Accounting when a migration is
/// finalized. One call for the whole document, because a migration is judged as a
/// whole and a caller that had to make three hundred calls would have three
/// hundred places to fail halfway.
/// </summary>
public class RecordOpeningStockRequest
{
    /// <summary>Which database. In the body because this door takes a key, not a token.</summary>
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>The opening balance document these lines belong to. Their source id.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Opening balance is required.")]
    public long OpeningBalanceId { get; set; }

    /// <summary>Go-live. Every movement is dated on it.</summary>
    public DateOnly AsOfDate { get; set; }

    public List<OpeningStockLine> Lines { get; set; } = [];
}

public class OpeningStockLine
{
    /// <summary>
    /// The line's position on the opening balance document, used as the movement's
    /// source line — which is what makes a second attempt at the same finalize
    /// land on the same key rather than doubling the stock.
    /// </summary>
    public long LineNumber { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    [Range(typeof(decimal), "0.000001", "999999999999.999999",
        ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>What the stock cost. This is what seeds the weighted average.</summary>
    [Range(typeof(decimal), "0", "999999999999.999999",
        ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    /// <summary>Where it sits. A location only — stock is one pool per branch.</summary>
    public long? WarehouseId { get; set; }
}

public class RecordOpeningStockResponse
{
    /// <summary>What the recorded lines came to. What Accounting checks its Inventory account against.</summary>
    public decimal Value { get; set; }

    public List<OpeningStockLineResult> Lines { get; set; } = [];
}

public class OpeningStockLineResult
{
    public long LineNumber { get; set; }

    public long ItemId { get; set; }

    public bool Recorded { get; set; }

    /// <summary>An earlier attempt at this finalize already recorded it. Still success.</summary>
    public bool AlreadyRecorded { get; set; }

    public string Outcome { get; set; } = null!;

    public decimal Value { get; set; }
}
