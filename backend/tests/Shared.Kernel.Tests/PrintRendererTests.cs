using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Acceptance tests 6, 7, 8 and 11 — the renderer's guarantees.
///
/// These need no database on purpose. The Master suite skips itself wholesale
/// when no PostgreSQL answers, and the rules about where a page breaks are far
/// too easy to regress to sit behind a skip.
/// </summary>
public class PrintRendererTests
{
    private static readonly PrintRenderer Renderer = new();

    /// <summary>An invoice with as many lines as the bug that named this file had.</summary>
    private static PrintPayload Invoice(int lineCount)
    {
        var items = new List<IReadOnlyDictionary<string, object?>>();

        for (int i = 1; i <= lineCount; i++)
        {
            items.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item.SlNo"] = i,
                ["Item.ItemName"] = $"Product {i}",
                ["Item.HsnSac"] = "7113",
                ["Item.Quantity"] = 2m,
                ["Item.Rate"] = 1500.50m,
                ["Item.Amount"] = 3001m,
            });
        }

        return new PrintPayload
        {
            Singles =
            {
                ["Organization.Name"] = "Kanaka Jewellers",
                ["Organization.Gstin"] = "33AABCU9603R1ZM",
                ["Party.Name"] = "Ravi Kumar",
                ["Document.No"] = "INV-0042",
                ["Document.Date"] = new DateOnly(2026, 9, 4),
                ["Totals.SubTotal"] = 126042m,
                ["Totals.Tax"] = 3781.26m,
                ["Totals.GrandTotal"] = 129823.26m,
                ["Totals.AmountInWords"] = "One lakh twenty nine thousand eight hundred twenty three and paise twenty six only",
            },
            Lists =
            {
                ["Item"] = items,
                ["Tax"] =
                [
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Tax.Component"] = "CGST", ["Tax.Rate"] = 1.5m,
                        ["Tax.TaxableValue"] = 126042m, ["Tax.Amount"] = 1890.63m,
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Tax.Component"] = "SGST", ["Tax.Rate"] = 1.5m,
                        ["Tax.TaxableValue"] = 126042m, ["Tax.Amount"] = 1890.63m,
                    },
                ],
            },
        };
    }

    private static PrintRenderRequest Request(int lines, Action<PrintSettings>? configure = null)
    {
        var settings = new PrintSettings();
        configure?.Invoke(settings);
        PrintSettingsValidator.Normalize(settings);

        return new PrintRenderRequest
        {
            Settings = settings,
            Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find("INV")!),
            DocumentTypeCode = "INV",
            Payload = Invoice(lines),
        };
    }

    private static IDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    private static int Occurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static IReadOnlyList<IElement> Pages(IDocument document) =>
        [.. document.QuerySelectorAll("section.pt-page")];

    // ---- 6. Pagination -------------------------------------------------

    [Fact]
    public void A_forty_two_line_invoice_runs_to_more_than_one_page()
    {
        PrintRenderResult result = Renderer.Render(Request(42));

        Assert.True(result.PageCount > 1, "42 lines should not fit on one A4 page.");
        Assert.Equal(result.PageCount, Pages(Parse(result.Html)).Count);
    }

    [Fact]
    public void The_fixed_header_appears_on_every_page()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        foreach (IElement page in Pages(document).Skip(1))
        {
            // Page one carries it in the flow; every page after gets the band.
            Assert.NotNull(page.QuerySelector(".pt-fh"));
        }

        Assert.Contains("Kanaka Jewellers", Pages(document)[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_appears_once_and_only_on_the_last_page()
    {
        PrintRenderResult result = Renderer.Render(Request(42, s => s.FooterPos = SegmentPosition.Bottom));
        IReadOnlyList<IElement> pages = Pages(Parse(result.Html));

        for (int i = 0; i < pages.Count; i++)
        {
            bool isLast = i == pages.Count - 1;
            Assert.Equal(isLast, pages[i].QuerySelector(".pt-f") is not null);
        }
    }

    [Fact]
    public void The_fixed_footer_appears_on_every_page()
    {
        PrintRenderResult result = Renderer.Render(Request(42, s =>
        {
            s.FooterPos = SegmentPosition.Bottom;
            s.FixedFooterPref = SegmentPosition.Bottom;
        }));

        foreach (IElement page in Pages(Parse(result.Html)))
        {
            Assert.NotNull(page.QuerySelector(".pt-ff"));
        }
    }

    [Fact]
    public void A_pinned_footer_sits_above_a_pinned_fixed_footer_rather_than_on_top_of_it()
    {
        PrintRenderResult result = Renderer.Render(Request(42, s => s.FooterPos = SegmentPosition.Bottom));
        IElement last = Pages(Parse(result.Html))[^1];

        IElement pinned = last.QuerySelector(".pt-pinned")!;
        IReadOnlyList<string> order = [.. pinned.Children.Select(c => c.ClassName ?? string.Empty)];

        // One block holding both, in segment order — not two blocks at bottom:0.
        Assert.Equal(2, order.Count);
        Assert.Contains("pt-f", order[0], StringComparison.Ordinal);
        Assert.Contains("pt-ff", order[1], StringComparison.Ordinal);
    }

    [Fact]
    public void No_table_row_is_split_across_a_page_break()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        int rendered = document.QuerySelectorAll("section.pt-page td")
            .Count(td => td.TextContent.StartsWith("Product ", StringComparison.Ordinal));

        // Every one of the 42 item cells is present exactly once and whole. A
        // sliced row would show up as a cell missing or duplicated.
        Assert.Equal(42, rendered);

        foreach (IElement row in document.QuerySelectorAll("section.pt-page tbody tr"))
        {
            Assert.NotEmpty(row.Children);
        }
    }

    [Fact]
    public void The_page_count_is_the_same_across_two_runs()
    {
        // Nothing in the pagination may depend on iteration order or a hash.
        PrintRenderResult first = Renderer.Render(Request(42));
        PrintRenderResult second = Renderer.Render(Request(42));

        Assert.Equal(first.PageCount, second.PageCount);
        Assert.Equal(first.Html, second.Html);
    }

    [Fact]
    public void A_cut_table_is_ruled_at_both_ends_of_the_cut()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        Assert.NotEmpty(document.QuerySelectorAll(".pt-cut"));
    }

    // ---- 7. The nested-table rule --------------------------------------

    [Fact]
    public void A_totals_block_nested_in_a_wrapper_row_renders_once_not_once_per_item()
    {
        PrintRenderResult result = Renderer.Render(Request(42));

        // Counted in the markup rather than over elements: a cell's TextContent
        // includes its descendants, so the wrapper cell would count the nested
        // one's text too and the assertion would be about the DOM rather than
        // about what is printed.
        int grandTotals = Occurrences(result.Html, "1,29,823.26");

        // The generated footer puts the tax summary and the totals in two cells
        // of one outer row. That outer row holds list chips — but only inside a
        // nested table, so it is not a repeat candidate. Reading it the other
        // way printed this block 42 times.
        Assert.Equal(1, grandTotals);
    }

    [Fact]
    public void The_inner_row_of_a_nested_table_still_repeats()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        int taxRows = document.QuerySelectorAll("td")
            .Count(td => td.TextContent is "CGST" or "SGST");
        Assert.Equal(2, Occurrences(result.Html, "1,890.63"));

        // Two tax rows, from the two rows of the Tax list — the nested table's
        // own row does repeat, which is the other half of the rule.
        Assert.Equal(2, taxRows);
    }

    // ---- 8. Unknown tags ------------------------------------------------

    [Fact]
    public void An_unknown_placeholder_renders_empty_and_never_as_a_raw_tag()
    {
        var content = new PrintContent
        {
            HeaderHtml = "<div>Ref: " + DefaultLayoutGenerator.Chip("Document.NotAThing") + "</div>",
        };

        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = content,
            DocumentTypeCode = "INV",
            Payload = Invoice(1),
        });

        Assert.Contains("Document.NotAThing", result.UnknownTags);
        Assert.DoesNotContain("Document.NotAThing", result.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("«", result.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_known_tag_with_no_value_also_prints_nothing()
    {
        var content = new PrintContent
        {
            HeaderHtml = "<div>Due: " + DefaultLayoutGenerator.Chip("Document.DueDate") + "</div>",
        };

        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = content,
            DocumentTypeCode = "INV",
            Payload = Invoice(1),
        });

        // Known but absent is not an error — the document simply has no due date.
        Assert.Empty(result.UnknownTags);
        Assert.DoesNotContain("«", result.Html, StringComparison.Ordinal);
    }

    // ---- Found by looking at a rendered page, not by a test ---------------

    [Fact]
    public void A_tables_column_headings_are_printed_once_per_page()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        foreach (IElement page in Pages(document))
        {
            int headings = page.QuerySelectorAll("th")
                .Count(th => th.TextContent == "Description");

            // The heading row lives in thead and is re-emitted from the table's
            // opening markup. It was also being packed as a body row, so every
            // table printed its headings twice, one line apart — visible on the
            // page and invisible to every structural assertion above.
            Assert.True(headings <= 1, $"A page printed the headings {headings} times.");
        }
    }

    [Fact]
    public void A_heading_row_is_never_packed_as_a_body_row()
    {
        PrintRenderResult result = Renderer.Render(Request(42));
        IDocument document = Parse(result.Html);

        Assert.DoesNotContain(
            document.QuerySelectorAll("tbody tr"),
            tr => tr.TextContent.Contains("HSN/SAC", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_number_prints_as_a_whole_number_not_as_money()
    {
        PrintRenderResult result = Renderer.Render(Request(3));
        IDocument document = Parse(result.Html);

        IReadOnlyList<string> serials = [.. document.QuerySelectorAll("tbody tr")
            .Where(tr => tr.TextContent.Contains("Product ", StringComparison.Ordinal))
            .Select(tr => tr.Children[0].TextContent.Trim())];

        // Number and Amount had one fallback mask between them, so an
        // unformatted line number took the Amount mask's two fixed decimals and
        // a serial number printed as 1.00.
        Assert.Equal(["1", "2", "3"], serials);
    }

    [Fact]
    public void An_amount_still_keeps_its_two_decimals()
    {
        PrintRenderResult result = Renderer.Render(Request(1));

        // The other half of the same fix: splitting the fallback must not have
        // cost Amount its fixed decimals.
        Assert.Contains("1,500.50", result.Html, StringComparison.Ordinal);
    }

    // ---- 11. Thermal -----------------------------------------------------

    [Fact]
    public void A_thermal_roll_is_one_continuous_page_with_no_repeated_bands()
    {
        PrintRenderResult result = Renderer.Render(Request(42, s =>
        {
            s.PrinterType = PrinterType.Thermal;
            s.RollWidthMm = 80;
        }));

        IDocument document = Parse(result.Html);

        Assert.Equal(1, result.PageCount);
        Assert.Single(Pages(document));
        Assert.Empty(document.QuerySelectorAll(".pt-band.pt-fh"));
        Assert.Empty(document.QuerySelectorAll(".pt-pinned"));
    }

    [Fact]
    public void A_thermal_page_is_as_wide_as_the_roll()
    {
        foreach (double width in new[] { 58d, 80d, 112d })
        {
            PrintRenderResult result = Renderer.Render(Request(3, s =>
            {
                s.PrinterType = PrinterType.Thermal;
                s.RollWidthMm = width;
            }));

            string expected = $"{PrintGeometry.ToPx(width):0.##}px";
            Assert.Contains($"width:{expected}", result.Html, StringComparison.Ordinal);
        }
    }

    // ---- Substitution and formatting -------------------------------------

    [Fact]
    public void Amounts_are_grouped_the_Indian_way_and_dates_use_the_masks_format()
    {
        PrintRenderResult result = Renderer.Render(Request(1));

        Assert.Contains("1,29,823.26", result.Html, StringComparison.Ordinal);
        Assert.Contains("04-Sep-2026", result.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quantity_drops_the_decimals_its_mask_makes_optional()
    {
        PrintRenderResult result = Renderer.Render(Request(1));

        // Item.Quantity is 2 under "##,##,##0.0##", so it prints as 2 — not
        // 2.000, and not 2.0.
        Assert.Contains(">2<", result.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_list_prints_no_row_at_all()
    {
        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find("INV")!),
            DocumentTypeCode = "INV",
            Payload = new PrintPayload(),
        });

        IDocument document = Parse(result.Html);

        // A blank line item is a line somebody has to account for.
        Assert.DoesNotContain(
            document.QuerySelectorAll("section.pt-page tbody tr"),
            tr => tr.TextContent.Contains("Product", StringComparison.Ordinal));
    }

    [Fact]
    public void A_value_may_be_keyed_by_the_field_alone_as_well_as_by_the_whole_tag()
    {
        var payload = new PrintPayload
        {
            Lists =
            {
                ["Item"] =
                [
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["ItemName"] = "Short key" },
                ],
            },
        };

        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = new PrintContent
            {
                DetailsHtml = "<table><tbody><tr><td>"
                    + DefaultLayoutGenerator.Chip("Item.ItemName", PlaceholderKind.List)
                    + "</td></tr></tbody></table>",
            },
            DocumentTypeCode = "INV",
            Payload = payload,
        });

        Assert.Contains("Short key", result.Html, StringComparison.Ordinal);
    }
}
