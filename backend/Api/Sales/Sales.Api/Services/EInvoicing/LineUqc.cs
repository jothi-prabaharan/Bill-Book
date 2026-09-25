using Shared.Kernel.Stock;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// Copies each line's GST unit (UQC) onto the line when a document posts (TK-91),
/// so the sales register, GSTR-1's HSN summary and the IRP all read it from the
/// document rather than from Inventory's tables, which Sales cannot read.
/// </summary>
public static class LineUqc
{
    /// <summary>
    /// Stamps every line that has no UQC yet. A line with no unit, or with a unit
    /// Inventory no longer knows, reports as <c>OTH</c>. When Inventory cannot be
    /// asked the lines are left empty rather than guessed: the posting goes
    /// through, and an e-invoice for the document is refused for the missing
    /// unit, which is the honest answer.
    /// </summary>
    public static async Task StampAsync<TLine>(
        IUqcLookup lookup,
        IReadOnlyCollection<TLine> lines,
        Func<TLine, long?> uomOf,
        Func<TLine, string?> read,
        Action<TLine, string> write,
        CancellationToken ct)
    {
        List<TLine> open = [.. lines.Where(l => string.IsNullOrEmpty(read(l)))];
        if (open.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, string> codes;
        try
        {
            codes = await lookup.FindAsync(open.Select(uomOf).OfType<long>(), ct);
        }
        catch (HttpRequestException)
        {
            return;
        }

        foreach (TLine line in open)
        {
            write(line, uomOf(line) is long uomId && codes.TryGetValue(uomId, out string? code)
                ? code
                : HttpUqcLookup.Other);
        }
    }
}
