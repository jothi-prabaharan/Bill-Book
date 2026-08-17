namespace Reporting.Entity.Enums;

/// <summary>
/// What an export produces.
///
/// <see cref="Pdf"/> is declared and refused. Nothing in the pinned package list
/// writes a PDF, and a hand-written writer would use the base-14 fonts, which
/// are WinAnsi — this product supports Tamil and Chinese, so a correct writer
/// needs TrueType embedding with Identity-H CID encoding. Declaring the value
/// now keeps the route's shape stable; returning a file full of wrong glyphs
/// would not. See REPORTS.md §5.8.
/// </summary>
public enum ExportFormat
{
    Xlsx = 1,
    Pdf = 2,
}
