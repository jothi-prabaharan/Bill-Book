using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Acceptance test 4, and the geometry rules around it.
///
/// The footer pairing is the one settings rule that is a correctness
/// constraint rather than a preference: a pinned footer leaves normal flow, so
/// an inline fixed footer collapses into the slot it vacated and prints above
/// it — inverting the fh → h → d → f → ff order the format guarantees.
/// </summary>
public class PrintSettingsTests
{
    [Fact]
    public void Pinning_the_footer_forces_the_fixed_footer_to_pin_with_it()
    {
        var settings = new PrintSettings
        {
            FooterPos = SegmentPosition.Bottom,
            FixedFooterPos = SegmentPosition.Inline,
            FixedFooterPref = SegmentPosition.Inline,
        };

        PrintSettingsValidator.Normalize(settings);

        Assert.Equal(SegmentPosition.Bottom, settings.FixedFooterPos);
    }

    [Fact]
    public void The_users_own_fixed_footer_choice_survives_being_overridden()
    {
        var settings = new PrintSettings
        {
            FooterPos = SegmentPosition.Bottom,
            FixedFooterPref = SegmentPosition.Inline,
        };

        PrintSettingsValidator.Normalize(settings);

        // Forced, but not forgotten — otherwise pinning the footer would
        // silently destroy a setting the user had made.
        Assert.Equal(SegmentPosition.Bottom, settings.FixedFooterPos);
        Assert.Equal(SegmentPosition.Inline, settings.FixedFooterPref);
    }

    [Fact]
    public void Returning_the_footer_to_inline_restores_the_remembered_preference()
    {
        var settings = new PrintSettings
        {
            FooterPos = SegmentPosition.Bottom,
            FixedFooterPref = SegmentPosition.Inline,
        };

        PrintSettingsValidator.Normalize(settings);
        Assert.Equal(SegmentPosition.Bottom, settings.FixedFooterPos);

        settings.FooterPos = SegmentPosition.Inline;
        PrintSettingsValidator.Normalize(settings);

        Assert.Equal(SegmentPosition.Inline, settings.FixedFooterPos);
    }

    [Fact]
    public void A_deliberately_pinned_fixed_footer_stays_pinned_when_the_footer_is_inline()
    {
        var settings = new PrintSettings
        {
            FooterPos = SegmentPosition.Inline,
            FixedFooterPref = SegmentPosition.Bottom,
        };

        PrintSettingsValidator.Normalize(settings);

        // The rule constrains only the pinned-footer case. Pinning the fixed
        // footer alone keeps the order, so it is allowed.
        Assert.Equal(SegmentPosition.Bottom, settings.FixedFooterPos);
    }

    [Fact]
    public void No_combination_of_positions_can_invert_the_segment_order()
    {
        foreach (SegmentPosition footer in Enum.GetValues<SegmentPosition>())
        {
            foreach (SegmentPosition preference in Enum.GetValues<SegmentPosition>())
            {
                var settings = new PrintSettings { FooterPos = footer, FixedFooterPref = preference };
                PrintSettingsValidator.Normalize(settings);

                bool inverted = settings.FooterPos == SegmentPosition.Bottom
                    && settings.FixedFooterPos == SegmentPosition.Inline;

                Assert.False(inverted, $"footer={footer}, pref={preference} produced an inverted page.");
            }
        }
    }

    [Fact]
    public void Segments_are_ordered_by_the_enum_so_nothing_can_reorder_them()
    {
        Assert.Equal(
            [
                PrintSegment.FixedHeader, PrintSegment.Header, PrintSegment.Details,
                PrintSegment.Footer, PrintSegment.FixedFooter,
            ],
            PrintSegments.InOrder.OrderBy(s => (int)s).ToArray());
    }

    [Fact]
    public void A_negative_margin_is_refused()
    {
        var settings = new PrintSettings { MarginTopMm = -1 };

        Assert.Contains(
            PrintSettingsValidator.Validate(settings),
            f => f.Field == nameof(PrintSettings.MarginTopMm));
    }

    [Fact]
    public void Margins_that_swallow_the_sheet_are_refused()
    {
        // Fine on A4, fatal on A5: 80 + 80 leaves 50 mm of a 210 mm sheet
        // and overruns a 148 mm one.
        var wide = new PrintSettings { PaperSize = PaperSize.A4, MarginLeftMm = 80, MarginRightMm = 80 };
        var narrow = new PrintSettings { PaperSize = PaperSize.A5, MarginLeftMm = 80, MarginRightMm = 80 };

        Assert.DoesNotContain(PrintSettingsValidator.Validate(wide), f => f.Field == nameof(PrintSettings.MarginLeftMm));
        Assert.Contains(PrintSettingsValidator.Validate(narrow), f => f.Field == nameof(PrintSettings.MarginLeftMm));
    }

    [Fact]
    public void A_thermal_roll_has_no_height_for_the_vertical_margins_to_exhaust()
    {
        var settings = new PrintSettings
        {
            PrinterType = PrinterType.Thermal,
            RollWidthMm = 80,
            MarginTopMm = 500,
            MarginBottomMm = 500,
        };

        Assert.Empty(PrintSettingsValidator.Validate(settings));
    }

    [Fact]
    public void A_thermal_roll_still_needs_a_width()
    {
        var settings = new PrintSettings { PrinterType = PrinterType.Thermal, RollWidthMm = 0 };

        Assert.Contains(
            PrintSettingsValidator.Validate(settings),
            f => f.Field == nameof(PrintSettings.RollWidthMm));
    }

    [Fact]
    public void Paper_size_is_ignored_for_thermal_and_roll_width_for_laser()
    {
        // A roll width that would be refused on a thermal printer is simply
        // unread on a laser one, and vice versa for paper size.
        var laser = new PrintSettings { PrinterType = PrinterType.Laser, RollWidthMm = 0 };
        Assert.Empty(PrintSettingsValidator.Validate(laser));

        var thermal = new PrintSettings { PrinterType = PrinterType.Thermal, RollWidthMm = 58, PaperSize = PaperSize.Legal };
        Assert.Equal(58d, PrintGeometry.Paper(thermal).WidthMm);
    }
}
