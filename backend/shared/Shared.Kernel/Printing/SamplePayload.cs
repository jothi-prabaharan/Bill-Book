namespace Shared.Kernel.Printing;

/// <summary>
/// Plausible data for previewing a template with no document behind it.
///
/// Built from the placeholder catalogue rather than written out per document
/// type, so a tag added to the catalogue is previewable the same day and cannot
/// be forgotten here. Values are typed the way a real payload types them —
/// decimals as decimals, dates as dates — so the preview exercises the same
/// formatting path a printed document does.
/// </summary>
public static class SamplePayload
{
    private const int SampleRowCount = 3;

    public static PrintPayload For(string documentTypeCode)
    {
        var payload = new PrintPayload();
        IReadOnlyList<PlaceholderDefinition> placeholders = PlaceholderCatalog.For(documentTypeCode);

        foreach (PlaceholderDefinition placeholder in placeholders.Where(p => p.Kind == PlaceholderKind.Single))
        {
            payload.Singles[placeholder.Tag] = Value(placeholder, 1);
        }

        foreach (IGrouping<string, PlaceholderDefinition> group in placeholders
            .Where(p => p.Kind == PlaceholderKind.List)
            .GroupBy(p => p.Group))
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            for (int row = 1; row <= SampleRowCount; row++)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PlaceholderDefinition placeholder in group)
                {
                    values[placeholder.Tag] = Value(placeholder, row);
                }

                rows.Add(values);
            }

            payload.Lists[group.Key] = rows;
        }

        return payload;
    }

    private static object? Value(PlaceholderDefinition placeholder, int row) => placeholder.Type switch
    {
        PlaceholderType.Amount => 1234.50m * row,
        PlaceholderType.Number => placeholder.Tag.EndsWith("SlNo", StringComparison.OrdinalIgnoreCase) ? row : 2m * row,
        PlaceholderType.Date => new DateOnly(2026, 9, 4).AddDays(row - 1),

        // An image with no source draws nothing, which is the honest preview of
        // a branch that has not uploaded a logo.
        PlaceholderType.Image => null,
        PlaceholderType.RichText => $"<div>{placeholder.Description}</div>",
        _ => Sample(placeholder, row),
    };

    private static string Sample(PlaceholderDefinition placeholder, int row) => placeholder.Tag switch
    {
        "Organization.Name" => "Sample Traders",
        "Organization.LegalName" => "Sample Traders Private Limited",
        "Organization.Gstin" => "33AABCU9603R1ZM",
        "Organization.StateName" => "Tamil Nadu",
        "Party.Name" => "Example Customer",
        "Party.Gstin" => "29AABCU9603R1ZX",
        "Party.StateName" => "Karnataka",
        "Document.No" => "SAMPLE-0001",
        "Document.PlaceOfSupply" => "Karnataka",
        "Document.Currency" => "INR",
        "Document.Status" => "Posted",
        "Totals.AmountInWords" => "Three thousand seven hundred three and paise fifty only",
        "Tax.Component" => row == 1 ? "CGST" : "SGST",
        _ => $"{placeholder.Description} {row}",
    };
}
