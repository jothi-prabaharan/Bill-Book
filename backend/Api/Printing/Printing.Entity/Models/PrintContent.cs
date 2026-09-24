namespace Printing.Entity.Models;

/// <summary>
/// The five segments' HTML, stored as jsonb. Sanitised on write — nothing here
/// is ever trusted on the way out, because it reached the column through an API.
/// </summary>
public class PrintContent
{
    public string FixedHeaderHtml { get; set; } = string.Empty;

    public string HeaderHtml { get; set; } = string.Empty;

    public string DetailsHtml { get; set; } = string.Empty;

    public string FooterHtml { get; set; } = string.Empty;

    public string FixedFooterHtml { get; set; } = string.Empty;
}
