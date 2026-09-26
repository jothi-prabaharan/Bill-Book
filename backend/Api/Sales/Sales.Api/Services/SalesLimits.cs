using Shared.Kernel.Approvals;
using Shared.Kernel.Contacts;
using Shared.Kernel.Tax;

namespace Sales.Api.Services;

/// <summary>What a sale's save found against the two limits (TK-102).</summary>
public sealed record SalesLimitCheck(string? CreditBreach, string? DiscountBreach, bool Unavailable)
{
    public bool Breached => CreditBreach is not null || DiscountBreach is not null;

    /// <summary>The overrides the breaches call for, credit first.</summary>
    public IEnumerable<ApprovalRequestKind> Kinds()
    {
        if (CreditBreach is not null)
        {
            yield return ApprovalRequestKind.CreditLimitOverride;
        }

        if (DiscountBreach is not null)
        {
            yield return ApprovalRequestKind.SalesDiscountOverride;
        }
    }

    /// <summary>Why approval cannot be asked for when no workflow covers the override.</summary>
    public static string NoWorkflow(ApprovalRequestKind kind) =>
        (kind == ApprovalRequestKind.CreditLimitOverride
            ? "No approval workflow covers going past a credit limit"
            : "No approval workflow covers going past the discount limit")
        + ", so nobody can approve it. Nothing was saved. Ask an administrator to set one up.";

    /// <summary>The refusal a save without <c>requestApproval</c> gets.</summary>
    public string Refusal() =>
        string.Join(" ", new[] { CreditBreach, DiscountBreach }.Where(b => b is not null))
            + " Save it again asking for approval to send it to an approver.";
}

/// <summary>
/// The two limits a sale is held to at save (TK-102): the customer's credit
/// limit, from Reporting, and the line discount limit (D-29), from Master —
/// the contact's own when set, else the branch's.
/// </summary>
public static class SalesLimits
{
    public static async Task<SalesLimitCheck> CheckAsync(
        ICreditCheckClient credit,
        IDiscountLimitClient? discounts,
        long contactId,
        decimal totalBase,
        IReadOnlyList<TaxLineResult> lines,
        CancellationToken ct)
    {
        CreditEvaluateResponse evaluated = await credit.EvaluateAsync(contactId, totalBase, ct);
        string? creditBreach = evaluated.Allowed
            ? null
            : evaluated.Reason ?? "This sale takes the customer past their credit limit.";

        if (discounts is null || lines.All(l => l.DiscountAmount <= 0m))
        {
            return new SalesLimitCheck(creditBreach, null, false);
        }

        DiscountLimitResponse? limit = await discounts.LimitForAsync(contactId, ct);
        if (limit is null)
        {
            return new SalesLimitCheck(creditBreach, null, true);
        }

        int? worst = WorstLine(lines, limit.LimitPercent);
        string? discountBreach = worst is int line
            ? $"Line {line} is discounted past the {limit.LimitPercent:0.##}% allowed "
                + (limit.Source == "Contact" ? "for this customer." : "in this branch.")
            : null;

        return new SalesLimitCheck(creditBreach, discountBreach, false);
    }

    /// <summary>The first line (1-based) discounted past <paramref name="limitPercent"/> of its gross value, or null.</summary>
    public static int? WorstLine(IReadOnlyList<TaxLineResult> lines, decimal limitPercent)
    {
        if (limitPercent >= 100m)
        {
            return null;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            TaxLineResult line = lines[i];
            if (line.GrossAmount > 0m && line.DiscountAmount * 100m > line.GrossAmount * limitPercent)
            {
                return i + 1;
            }
        }

        return null;
    }
}
