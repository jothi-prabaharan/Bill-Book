namespace Shared.Kernel.Printing;

/// <summary>Space above and below one segment, in millimetres.</summary>
public class SegmentMargin
{
    public double AboveMm { get; set; }

    public double BelowMm { get; set; }
}

/// <summary>
/// Per-segment spacing, one property per segment rather than a dictionary.
///
/// The specification asks that segMargins "covers all five segments"; naming
/// them makes that structural instead of a validation rule that could be
/// skipped — there is no way to express a settings object missing one.
/// </summary>
public class SegmentMargins
{
    public SegmentMargin FixedHeader { get; set; } = new();

    public SegmentMargin Header { get; set; } = new();

    public SegmentMargin Details { get; set; } = new();

    public SegmentMargin Footer { get; set; } = new();

    public SegmentMargin FixedFooter { get; set; } = new();
}

/// <summary>
/// The printer and page geometry of one template, stored as jsonb.
///
/// A value object rather than a table entity: it has no key, no audit columns
/// and no life of its own, and it travels to three other services through the
/// renderer, which is why it lives in Shared.Kernel rather than in Master.
/// </summary>
public class PrintSettings
{
    public PrinterType PrinterType { get; set; } = PrinterType.Laser;

    /// <summary>Ignored when <see cref="PrinterType"/> is thermal.</summary>
    public PaperSize PaperSize { get; set; } = PaperSize.A4;

    /// <summary>Ignored when <see cref="PrinterType"/> is laser. Always millimetres, whatever unit the screen shows.</summary>
    public double RollWidthMm { get; set; } = 80d;

    public double MarginTopMm { get; set; } = 16d;

    public double MarginBottomMm { get; set; } = 16d;

    public double MarginLeftMm { get; set; } = 18d;

    public double MarginRightMm { get; set; } = 18d;

    public SegmentPosition FooterPos { get; set; } = SegmentPosition.Inline;

    /// <summary>
    /// Effective position. Coerced to <see cref="SegmentPosition.Bottom"/>
    /// whenever <see cref="FooterPos"/> is Bottom — see
    /// <see cref="PrintSettingsValidator"/> for why that is not a preference
    /// but a correctness rule.
    /// </summary>
    public SegmentPosition FixedFooterPos { get; set; } = SegmentPosition.Inline;

    /// <summary>
    /// What the user actually chose for the fixed footer, kept while
    /// <see cref="FixedFooterPos"/> is being forced, and restored when the
    /// footer goes back to inline. Without it, pinning the footer would
    /// silently destroy the fixed footer's own setting.
    /// </summary>
    public SegmentPosition FixedFooterPref { get; set; } = SegmentPosition.Inline;

    public SegmentMargins SegMargins { get; set; } = new();
}
