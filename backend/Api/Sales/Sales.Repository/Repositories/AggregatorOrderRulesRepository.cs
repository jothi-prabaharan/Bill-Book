using Sales.Entity.Enums;

namespace Sales.Repository.Repositories;

/// <summary>
/// The pure rules of aggregator order intake (TK-167): how an aggregator's
/// order id and customer details are cleaned, and how they read on the order.
/// </summary>
public static class AggregatorOrderRulesRepository
{
    /// <summary>The longest an order's note can be (the column's limit).</summary>
    public const int NoteLimit = 250;

    /// <summary>An aggregator's order id with the whitespace a copy and paste leaves, or null if nothing is left.</summary>
    public static string? CleanExternalId(string? externalOrderId)
    {
        string trimmed = externalOrderId?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// What the order's note says, so the kitchen and the rider see where it
    /// came from without opening anything: "Zomato 4821 · Ravi · 98xxxxxx · address · note",
    /// each part present only when sent, cut to fit the note column.
    /// </summary>
    public static string OrderNote(OrderAggregator aggregator, string externalOrderId, string? customerName, string? customerPhone, string? deliveryAddress, string? notes)
    {
        List<string> parts = [$"{aggregator} {externalOrderId}"];
        foreach (string? part in new[] { customerName, customerPhone, deliveryAddress, notes })
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                parts.Add(part.Trim());
            }
        }

        string note = string.Join(" · ", parts);
        return note.Length <= NoteLimit ? note : note[..NoteLimit];
    }
}
