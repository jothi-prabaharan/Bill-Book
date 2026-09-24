using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Printing.Entity.Models;

/// <summary>
/// A document to print, pushed by the service that owns it.
///
/// <b>The caller builds the payload and sends it under the user's own token.</b>
/// That is the decision stage P turns on: Sales reads its own invoice, keys the
/// values the way <c>PlaceholderCatalog</c> names them, and posts them here.
/// Printing resolves the branch's template from the token and renders. It reads
/// no document and no other service's table, so no internal endpoint has to
/// invent a branch.
/// </summary>
public class RenderPrintRequest
{
    [Required(ErrorMessage = "Document type is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Document type must be exactly 3 characters.")]
    public string DocumentTypeCode { get; set; } = null!;

    /// <summary>
    /// The template the document names, if it names one. A deleted or unknown
    /// id falls through to the branch default, then to the platform layout, so
    /// a document stays printable after its template is gone.
    /// </summary>
    public long? PrintTemplateId { get; set; }

    [Required(ErrorMessage = "Payload is required.")]
    public RenderPayload Payload { get; set; } = new();

    /// <summary>
    /// The branch's currency symbol, for amount placeholders. The caller knows
    /// its own branch's currency; Printing does not read Master to find it.
    /// </summary>
    [MaxLength(8, ErrorMessage = "Currency symbol cannot exceed 8 characters.")]
    public string? CurrencySymbol { get; set; }
}

/// <summary>
/// <c>PrintPayload</c> as it arrives on the wire.
///
/// Values are raw JSON here because the renderer's formatter reads CLR values —
/// a decimal, a string — and a <see cref="JsonElement"/> is neither. A number
/// left as an element prints unformatted and an image URL prints as nothing, so
/// the service converts before it renders rather than trusting the binder.
/// </summary>
public class RenderPayload
{
    public Dictionary<string, JsonElement> Singles { get; set; } = [];

    /// <summary>Keyed by collection — Item, Tax, Payment, Alloc, Line.</summary>
    public Dictionary<string, List<Dictionary<string, JsonElement>>> Lists { get; set; } = [];
}

/// <summary>The printed document, as HTML ready to hand to a browser's print.</summary>
public class RenderPrintResponse
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    /// <summary>Tags the template holds that nothing in the payload resolved.</summary>
    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    /// <summary>Null when the layout was the platform's generated one, not a stored row.</summary>
    public long? PrintTemplateId { get; set; }

    /// <summary>Which step of the resolution chain supplied the layout.</summary>
    public PrintTemplateSource Source { get; set; }
}
