namespace Shared.Kernel.Printing;

/// <summary>One rejected rule, ready to be returned as INVALID_GEOMETRY.</summary>
public sealed class GeometryFailure
{
    public string Field { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Normalises and checks page geometry. Two separate jobs, deliberately:
/// <see cref="Normalize"/> settles what the settings <i>mean</i> before anything
/// looks at them, and <see cref="Validate"/> rejects what cannot be drawn.
/// Running them the other way round would report a failure about a value
/// normalisation was about to overwrite.
/// </summary>
public static class PrintSettingsValidator
{
    /// <summary>
    /// Applies the footer rule, which is a correctness constraint rather than a
    /// preference.
    ///
    /// <b>A pinned footer forces the fixed footer to pin with it.</b> Pinning
    /// takes the footer out of the normal flow; an inline fixed footer would
    /// then collapse upward into the slot the footer vacated and print
    /// <i>above</i> it, inverting the one order the whole format guarantees.
    /// There is no combination of settings that may do that, so the pairing is
    /// resolved here rather than trusted to a screen.
    ///
    /// The user's own choice survives in
    /// <see cref="PrintSettings.FixedFooterPref"/> while it is being overridden,
    /// and is restored the moment the footer returns to inline — otherwise
    /// pinning the footer would quietly destroy a setting the user had made.
    /// </summary>
    public static void Normalize(PrintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.FooterPos == SegmentPosition.Bottom)
        {
            settings.FixedFooterPos = SegmentPosition.Bottom;
        }
        else
        {
            settings.FixedFooterPos = settings.FixedFooterPref;
        }
    }

    /// <summary>
    /// Everything that would make the page undrawable. Returns every failure
    /// rather than the first, so a caller fixing a form is not made to
    /// round-trip once per mistake.
    /// </summary>
    public static IReadOnlyList<GeometryFailure> Validate(PrintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var failures = new List<GeometryFailure>();

        void NonNegative(double value, string field, string label)
        {
            if (value < 0 || double.IsNaN(value))
            {
                failures.Add(new GeometryFailure { Field = field, Message = $"{label} cannot be negative." });
            }
        }

        NonNegative(settings.MarginTopMm, nameof(settings.MarginTopMm), "Top margin");
        NonNegative(settings.MarginBottomMm, nameof(settings.MarginBottomMm), "Bottom margin");
        NonNegative(settings.MarginLeftMm, nameof(settings.MarginLeftMm), "Left margin");
        NonNegative(settings.MarginRightMm, nameof(settings.MarginRightMm), "Right margin");

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            SegmentMargin margin = PrintSegments.Margin(settings.SegMargins, segment);
            string code = PrintSegments.Code(segment);
            NonNegative(margin.AboveMm, $"SegMargins.{code}.AboveMm", $"Space above the {code} segment");
            NonNegative(margin.BelowMm, $"SegMargins.{code}.BelowMm", $"Space below the {code} segment");
        }

        // Paper size is ignored for thermal and roll width for laser, so only
        // the one that will actually be used is checked. The unused value is
        // left alone rather than reset: switching printer type back should not
        // have silently lost the other setting.
        if (settings.PrinterType == PrinterType.Thermal)
        {
            if (settings.RollWidthMm <= 0 || double.IsNaN(settings.RollWidthMm))
            {
                failures.Add(new GeometryFailure
                {
                    Field = nameof(settings.RollWidthMm),
                    Message = "Roll width must be greater than zero.",
                });
            }
        }

        // A content box with no width or no height is a page that cannot hold a
        // single character. Checked against the paper actually in use, because
        // margins that are fine on A4 can swallow an A5 sheet whole.
        (double widthMm, double heightMm) = PrintGeometry.Paper(settings);

        double contentWidth = widthMm - settings.MarginLeftMm - settings.MarginRightMm;
        if (contentWidth <= 0)
        {
            failures.Add(new GeometryFailure
            {
                Field = nameof(settings.MarginLeftMm),
                Message = "The left and right margins leave no width to print in.",
            });
        }

        // A thermal roll is continuous, so it has no height for the top and
        // bottom margins to exhaust.
        if (!double.IsPositiveInfinity(heightMm))
        {
            double contentHeight = heightMm - settings.MarginTopMm - settings.MarginBottomMm;
            if (contentHeight <= 0)
            {
                failures.Add(new GeometryFailure
                {
                    Field = nameof(settings.MarginTopMm),
                    Message = "The top and bottom margins leave no height to print in.",
                });
            }
        }

        return failures;
    }
}
