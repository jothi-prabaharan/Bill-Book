using Ganss.Xss;

namespace Customer.Repository.Sanitization;

/// <summary>
/// Allow-list sanitiser for campaign email templates, applied on write.
/// Strips scripts, event handlers, and unsafe styling while preserving
/// markup, links, images, and merge tags ({{tag}}).
/// </summary>
public static class TemplateSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");

        sanitizer.AllowedCssProperties.Remove("position");
        sanitizer.AllowDataAttributes = false;

        return sanitizer;
    }

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return Sanitizer.Sanitize(html);
    }
}
