namespace Shared.Kernel.Printing;

/// <summary>
/// How tall a block is taken to be.
///
/// <b>An estimate, and deliberately a crude one.</b> Nothing in this process
/// lays out HTML — there is no browser here — so heights are derived from the
/// text a block holds and the width it has to hold it in. That is enough to
/// place page breaks between whole rows, which is what the pagination rules
/// actually require, and it is deterministic, which is what makes a page count
/// stable across two runs of the same document.
///
/// When a real rendering engine lands, these numbers should come from it
/// instead. The estimate is isolated here so that swap touches one file.
/// </summary>
public sealed class PrintMetrics
{
    public static readonly PrintMetrics Default = new();

    /// <summary>One line of text, including its leading.</summary>
    public double LineHeightMm { get; init; } = 5.0;

    /// <summary>Average glyph advance at the body size. Latin text at roughly 10pt.</summary>
    public double AverageCharWidthMm { get; init; } = 1.9;

    /// <summary>Space a block claims beyond its text — cell padding and borders.</summary>
    public double BlockPaddingMm { get; init; } = 1.2;

    /// <summary>How many characters fit on one line at this width.</summary>
    public int CharsPerLine(double widthMm) =>
        Math.Max(1, (int)Math.Floor(widthMm / AverageCharWidthMm));

    /// <summary>Height of a run of text laid into a column of the given width.</summary>
    public double TextHeightMm(string text, double widthMm)
    {
        int perLine = CharsPerLine(widthMm);
        int lines = Math.Max(1, (int)Math.Ceiling((double)text.Length / perLine));
        return (lines * LineHeightMm) + BlockPaddingMm;
    }
}
