namespace Printing.Entity.Enums;

/// <summary>
/// The five bands every printed document is composed of.
///
/// <b>The numeric values are the print order and nothing may reorder them.</b>
/// Sorting by this enum is what guarantees fixed header → header → details →
/// footer → fixed footer, in every renderer path, whatever the position
/// settings say. A pinned segment leaves the normal flow but never changes
/// where it sits in this sequence.
/// </summary>
public enum PrintSegment
{
    /// <summary>Repeats on every page. <c>fh</c>.</summary>
    FixedHeader = 0,

    /// <summary>Flows. Page one only. <c>h</c>.</summary>
    Header = 1,

    /// <summary>Flows, and is what paginates. <c>d</c>.</summary>
    Details = 2,

    /// <summary>Flows, and appears once — on the last page. <c>f</c>.</summary>
    Footer = 3,

    /// <summary>Repeats on every page. <c>ff</c>.</summary>
    FixedFooter = 4,
}

/// <summary>
/// Whether a segment flows with the content or is pinned above the bottom
/// margin. Only the footer and the fixed footer have the choice.
/// </summary>
public enum SegmentPosition
{
    /// <summary>In the flow, immediately after the segment before it.</summary>
    Inline = 0,

    /// <summary>Out of the flow, pinned above the page's bottom margin.</summary>
    Bottom = 1,
}

/// <summary>
/// Laser and inkjet print onto a fixed sheet and paginate. A thermal roll is
/// continuous: one page, no repeats, and the paper size is a width only.
/// </summary>
public enum PrinterType
{
    Laser = 0,
    Thermal = 1,
}

/// <summary>Fixed sheet sizes. Ignored entirely when the printer is thermal.</summary>
public enum PaperSize
{
    A4 = 0,
    A5 = 1,
    Letter = 2,
    Legal = 3,
}
