namespace Reporting.Entity.Enums;

/// <summary>
/// What an export produces.
///
/// <b><see cref="Xlsx"/> and <see cref="Csv"/> are the supported formats</b>, and
/// the requirement is that both use the same query state as the grid with paging
/// removed. Reporting.md §5.
///
/// <see cref="Pdf"/> is declared and refused. Nothing in the pinned package list
/// writes a PDF, and a hand-written writer would use the base-14 fonts, which
/// are WinAnsi — this product supports Tamil and Chinese, so a correct writer
/// needs TrueType embedding with Identity-H CID encoding. Declaring the value
/// now keeps the route's shape stable; returning a file full of wrong glyphs
/// would not. PDF is explicitly outside the reporting requirement and must not
/// be offered in the export UI. See Reporting.md §5.8.
/// </summary>
public enum ExportFormat
{
    Xlsx = 1,
    Pdf = 2,
    Csv = 3,
}
