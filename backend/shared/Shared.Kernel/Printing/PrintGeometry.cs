namespace Shared.Kernel.Printing;

/// <summary>
/// Paper dimensions and the one millimetre-to-pixel constant.
///
/// <b>There is exactly one scale in this product and this is it.</b> The
/// renderer and the editor both read <see cref="MmToPx"/>; every length that
/// reaches CSS is a pixel derived from it, and no output ever carries the
/// browser's native <c>mm</c> unit. Mixing the two is what put page margins out
/// of proportion to the page around them once already — the constant and the
/// CSS unit disagree the moment anything is scaled.
/// </summary>
public static class PrintGeometry
{
    /// <summary>
    /// Millimetres to CSS pixels. 96 dpi, rounded, as the specification names it.
    /// The exact value would be 96 / 25.4 = 3.779527…; the difference is about
    /// one part in 130,000, under a tenth of a millimetre across an A4 page.
    /// </summary>
    public const double MmToPx = 3.7795;

    public static double ToPx(double millimetres) => millimetres * MmToPx;

    /// <summary>Sheet size in millimetres, width by height, portrait.</summary>
    public static (double WidthMm, double HeightMm) Sheet(PaperSize size) => size switch
    {
        PaperSize.A4 => (210d, 297d),
        PaperSize.A5 => (148d, 210d),
        PaperSize.Letter => (216d, 279d),
        PaperSize.Legal => (216d, 356d),
        _ => (210d, 297d),
    };

    /// <summary>
    /// The paper the settings describe. A thermal roll has a width and no
    /// height — it is continuous, so the caller must not paginate against the
    /// height returned here.
    /// </summary>
    public static (double WidthMm, double HeightMm) Paper(PrintSettings settings) =>
        settings.PrinterType == PrinterType.Thermal
            ? (settings.RollWidthMm, double.PositiveInfinity)
            : Sheet(settings.PaperSize);
}
