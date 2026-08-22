namespace Master.Entity.Enums;

/// <summary>Where a discount is keyed on a sales or purchase document.</summary>
public enum DiscountLevel
{
    /// <summary>Per line only. The default.</summary>
    Line = 0,

    /// <summary>
    /// One figure on the header, apportioned across the lines by taxable value
    /// before tax. GST is charged per line, so a discount that never reaches a
    /// line cannot reduce it.
    /// </summary>
    Header = 1,

    /// <summary>Both, with the header figure apportioned on top of line discounts.</summary>
    Both = 2,
}
