using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Printing;

namespace Master.Entity.Models;

/// <summary>One row of the template list, and what the transaction-entry picker reads.</summary>
public class PrintTemplateListItem
{
    public long PrintTemplateId { get; set; }

    public string TemplateName { get; set; } = null!;

    public string DocumentTypeCode { get; set; } = null!;

    public bool IsDefault { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>The whole template, including the version a later save has to echo back.</summary>
public class PrintTemplateDetail
{
    public long PrintTemplateId { get; set; }

    public string DocumentTypeCode { get; set; } = null!;

    public string TemplateName { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public PrintSettings Settings { get; set; } = new();

    public PrintContent Content { get; set; } = new();

    public int TemplateVersion { get; set; }

    public int SeedVersion { get; set; }

    /// <summary>
    /// True when the platform's generated layout has moved on since this row was
    /// seeded, so the editor can offer a reset without guessing.
    /// </summary>
    public bool CanResetToLatest { get; set; }

    /// <summary>
    /// Tags in the content that this document type cannot resolve. Reported
    /// rather than refused: they print as nothing, and the editor shows them as
    /// a status pill so a half-finished template can still be saved.
    /// </summary>
    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CreatePrintTemplateRequest
{
    [Required(ErrorMessage = "Document type is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Document type must be exactly 3 characters.")]
    public string DocumentTypeCode { get; set; } = null!;

    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string TemplateName { get; set; } = null!;

    /// <summary>Omitted means the platform defaults.</summary>
    public PrintSettings? Settings { get; set; }

    /// <summary>Omitted means the generated layout for this document type.</summary>
    public PrintContent? Content { get; set; }

    /// <summary>Set to duplicate an existing template's settings and content.</summary>
    public long? CopyFromId { get; set; }
}

public class UpdatePrintTemplateRequest
{
    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string TemplateName { get; set; } = null!;

    [Required(ErrorMessage = "Settings are required.")]
    public PrintSettings Settings { get; set; } = null!;

    [Required(ErrorMessage = "Content is required.")]
    public PrintContent Content { get; set; } = null!;

    /// <summary>
    /// The version last read. A mismatch is TEMPLATE_STALE and nothing is
    /// written — somebody else saved while this editor was open.
    /// </summary>
    public int TemplateVersion { get; set; }
}

public class PreviewPrintTemplateRequest
{
    /// <summary>
    /// A real document to preview against.
    ///
    /// <b>Not served here.</b> Templates live in Master and documents live in
    /// Sales, Purchase and Accounting; no service may read another's tables.
    /// Passing it returns a refusal naming the endpoint that can — the
    /// document's own print route. Omitting it previews against sample data,
    /// which is what the editor uses.
    /// </summary>
    public long? SampleDocId { get; set; }
}

/// <summary>Rendered output, ready for the preview pane.</summary>
public class PrintPreviewResponse
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public IReadOnlyList<string> UnknownTags { get; set; } = [];
}

/// <summary>
/// A refusal, carrying the specification's code alongside the message.
///
/// It extends <c>MessageResponse</c> rather than replacing it, so the envelope
/// every other endpoint in this product returns is unchanged and a client that
/// only reads <c>message</c> still works. The repository has no error-code
/// convention of its own; the code is additive for these routes only.
/// </summary>
public class PrintTemplateError : MessageResponse
{
    public string Code { get; set; } = null!;
}

/// <summary>Every way a print template save can be refused.</summary>
public enum PrintTemplateOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>409 TEMPLATE_NAME_TAKEN.</summary>
    NameTaken = 2,

    /// <summary>409 TEMPLATE_STALE.</summary>
    Stale = 3,

    /// <summary>409 LAST_TEMPLATE — the only one left, or the default.</summary>
    LastTemplate = 4,

    /// <summary>422 INVALID_SEGMENT_HTML.</summary>
    InvalidSegmentHtml = 5,

    /// <summary>422 INVALID_GEOMETRY.</summary>
    InvalidGeometry = 6,

    /// <summary>422 INVALID_DOCUMENT_TYPE — not one of the printable twelve.</summary>
    InvalidDocumentType = 7,

    /// <summary>422 PREVIEW_NEEDS_DOCUMENT_SERVICE — see PreviewPrintTemplateRequest.SampleDocId.</summary>
    PreviewNotAvailableHere = 8,
}

/// <summary>An outcome and, when it failed, what to say about it.</summary>
public sealed class PrintTemplateResult<T>
{
    public PrintTemplateOutcome Outcome { get; init; }

    public T? Value { get; init; }

    /// <summary>Names the offending segments or fields, when the outcome has any.</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    public static PrintTemplateResult<T> Ok(T value) =>
        new() { Outcome = PrintTemplateOutcome.Ok, Value = value };

    public static PrintTemplateResult<T> Fail(
        PrintTemplateOutcome outcome, IReadOnlyList<string>? details = null) =>
        new() { Outcome = outcome, Details = details ?? [] };
}

/// <summary>Where a resolved layout came from, so a caller can say so if it matters.</summary>
public enum PrintTemplateSource
{
    /// <summary>The template the document itself names.</summary>
    Explicit = 0,

    /// <summary>The branch's default for this document type.</summary>
    BranchDefault = 1,

    /// <summary>The platform's generated layout, held in memory. Nothing was stored for this branch.</summary>
    PlatformSeed = 2,
}

/// <summary>The layout a document will print with, and where it came from.</summary>
public sealed class PrintTemplateResolution
{
    public PrintTemplateSource Source { get; init; }

    /// <summary>Null when the layout is the platform seed rather than a stored row.</summary>
    public long? PrintTemplateId { get; init; }

    public string DocumentTypeCode { get; init; } = string.Empty;

    public PrintSettings Settings { get; init; } = new();

    public PrintContent Content { get; init; } = new();
}
