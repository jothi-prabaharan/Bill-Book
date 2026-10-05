using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;

namespace Sales.Repository.Repositories;

/// <summary>The billing rules (TK-148), pure.</summary>
public static class RestaurantBillingRulesRepository
{
    /// <summary>A service charge on the dishes that share a tax treatment, to be billed as its own line.</summary>
    public sealed record ServiceChargePart(long? TaxGroupId, bool IsPriceInclusive, TaxTreatment TaxTreatment, decimal Amount);

    /// <summary>
    /// The service charge, split by the tax the dishes carry. Indian GST taxes a
    /// restaurant's service charge as part of the supply it is charged on, so
    /// the part charged on 5% food is taxed at 5%, the part on an 18% drink at
    /// 18%. Each part is the percentage of its dishes' amounts, to the paisa;
    /// a dish priced with tax in has its part priced with tax in too.
    /// </summary>
    public static IReadOnlyList<ServiceChargePart> ServiceCharge(IEnumerable<RestaurantOrderLine> lines, decimal percent)
    {
        if (percent <= 0m)
        {
            return [];
        }

        return lines
            .GroupBy(l => (l.TaxGroupId, l.IsPriceInclusive, l.TaxTreatment))
            .Select(g => new ServiceChargePart(
                g.Key.TaxGroupId,
                g.Key.IsPriceInclusive,
                g.Key.TaxTreatment,
                Math.Round(g.Sum(l => l.Quantity * l.UnitPrice) * percent / 100m, 2, MidpointRounding.AwayFromZero)))
            .Where(p => p.Amount > 0m)
            .OrderBy(p => p.TaxGroupId)
            .ToList();
    }

    /// <summary>
    /// Why these dishes cannot go on one bill, or null: each must belong to one
    /// of the orders, not be cancelled and not be billed already.
    /// </summary>
    public static string? SelectionRefusal(IReadOnlyCollection<RestaurantOrderLine> available, IReadOnlyCollection<long> requested)
    {
        if (requested.Count == 0)
        {
            return available.Count == 0 ? "Every dish on these orders has been billed or cancelled." : null;
        }

        if (requested.Distinct().Count() != requested.Count)
        {
            return "The same dish was chosen twice.";
        }

        HashSet<long> billable = [.. available.Select(l => l.RestaurantOrderLineId)];
        return requested.All(billable.Contains)
            ? null
            : "A chosen dish is not on these orders, was cancelled, or has been billed already.";
    }

    /// <summary>Why a tip cannot be taken, or null.</summary>
    public static string? TipRefusal(bool tipsEnabled, decimal tip, long? bankAccountId) =>
        tip switch
        {
            < 0m => "A tip cannot be negative.",
            0m => null,
            _ when !tipsEnabled => "Tips are switched off for this branch. Turn them on in Settings › Restaurant.",
            _ when bankAccountId is null => "Choose where the tip was paid in.",
            _ => null,
        };

    /// <summary>The invoice line for a dish: its name with portion and choices, its price and tax as copied when it was ordered.</summary>
    public static SaveInvoiceLineRequest InvoiceLine(RestaurantOrderLine line) => new()
    {
        ItemId = line.ItemId,
        Description = Describe(line),
        Quantity = line.Quantity,
        UomId = line.UomId == 0 ? null : line.UomId,
        UnitPrice = line.UnitPrice,
        IsPriceInclusive = line.IsPriceInclusive,
        TaxTreatment = line.TaxTreatment,
        TaxGroupId = line.TaxGroupId,

        // A dish made to order issues no stock of its own; its recipe does (TK-149).
        LineType = line.IsStocked ? DocumentLineType.Stock : DocumentLineType.Expense,
        LineNotes = line.Note,
    };

    public static string Describe(RestaurantOrderLine line)
    {
        string name = line.VariantName is null ? line.ItemName : $"{line.ItemName} ({line.VariantName})";
        string described = line.Modifiers.Count == 0 ? name : $"{name}: {string.Join(", ", line.Modifiers.Select(m => m.Name))}";
        return described.Length <= 500 ? described : described[..500];
    }

    /// <summary>The dishes an order still has to bill.</summary>
    public static IEnumerable<RestaurantOrderLine> Billable(IEnumerable<RestaurantOrderLine> lines) =>
        lines.Where(l => l.KitchenStatus != KitchenStatus.Cancelled && l.RestaurantBillId is null);
}
