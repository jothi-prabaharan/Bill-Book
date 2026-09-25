using Sales.Api.Services.EInvoicing;
using Shared.Kernel.Documents;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Each local refusal the design lists (TK-91): a document that fails any of
/// them goes to Failed with the problem named, and is never sent to the IRP.
/// </summary>
public sealed class EInvoiceValidatorTests
{
    private static readonly DateOnly Dated = new(2026, 9, 20);

    private static Inv01Document Good() => Inv01Mapper.Map(EInvoiceMapperTests.IntraStateInvoice());

    private static IReadOnlyList<string> Codes(Inv01Document document, DateOnly? today = null, int? window = null) =>
        [.. EInvoiceValidator.Validate(document, Dated, today ?? Dated, window).Select(p => p.Code)];

    [Fact]
    public void A_good_document_has_no_problems() => Assert.Empty(Codes(Good()));

    [Fact]
    public void An_invalid_seller_gstin_is_refused()
    {
        Inv01Document document = Good() with { SellerDtls = Good().SellerDtls with { Gstin = "33AAACH7409R1Z9" } };

        Assert.Contains("SELLER_GSTIN", Codes(document));
    }

    [Fact]
    public void A_missing_buyer_gstin_is_refused_on_b2b()
    {
        Inv01Document document = Good() with { BuyerDtls = Good().BuyerDtls with { Gstin = "" } };

        Assert.Contains("BUYER_GSTIN", Codes(document));
    }

    [Fact]
    public void A_state_that_disagrees_with_the_gstin_is_refused()
    {
        Inv01Document document = Good() with { BuyerDtls = Good().BuyerDtls with { Stcd = "29" } };

        Assert.Contains("BUYER_STATE", Codes(document));
    }

    [Fact]
    public void A_pin_that_is_not_six_digits_is_refused()
    {
        Inv01Document document = Good() with { BuyerDtls = Good().BuyerDtls with { Pin = 0 } };

        IReadOnlyList<EInvoiceProblem> problems = EInvoiceValidator.Validate(document, Dated, Dated);

        EInvoiceProblem pin = Assert.Single(problems);
        Assert.Equal("BUYER_PIN", pin.Code);
        Assert.Equal("The PIN code of the customer must be six digits.", pin.Message);
    }

    [Fact]
    public void An_address_without_a_city_is_refused()
    {
        Inv01Document document = Good() with { SellerDtls = Good().SellerDtls with { Loc = "" } };

        Assert.Contains("SELLER_ADDRESS", Codes(document));
    }

    [Fact]
    public void A_missing_place_of_supply_is_refused()
    {
        Inv01Document document = Good() with { BuyerDtls = Good().BuyerDtls with { Pos = "" } };

        Assert.Contains("POS", Codes(document));
    }

    [Theory]
    [InlineData("")]
    [InlineData("7214")]
    [InlineData("72142")]
    [InlineData("72A42090")]
    public void An_hsn_that_is_not_six_or_eight_digits_is_refused(string hsn)
    {
        Inv01Document document = Good() with
        {
            ItemList = [Good().ItemList[0] with { HsnCd = hsn }, Good().ItemList[1]],
        };

        EInvoiceProblem problem = Assert.Single(EInvoiceValidator.Validate(document, Dated, Dated));
        Assert.Equal("HSN", problem.Code);
        Assert.StartsWith("Line 1 ", problem.Message);
    }

    [Fact]
    public void Goods_without_a_uqc_are_refused_and_a_service_is_not()
    {
        Inv01Document goods = Good() with { ItemList = [Good().ItemList[0] with { Unit = null }, Good().ItemList[1]] };

        Assert.Equal(new[] { "UQC" }, Codes(goods));
        Assert.Null(Good().ItemList[1].Unit);
        Assert.Empty(Codes(Good()));
    }

    [Fact]
    public void Totals_that_do_not_add_up_are_refused()
    {
        Inv01Document document = Good() with { ValDtls = Good().ValDtls with { TotInvVal = 7100m } };

        Assert.Contains("TOTALS", Codes(document));
    }

    [Fact]
    public void Totals_within_a_rupee_pass()
    {
        Inv01Document document = Good() with { ValDtls = Good().ValDtls with { TotInvVal = 6992.90m } };

        Assert.DoesNotContain("TOTALS", Codes(document));
    }

    [Fact]
    public void A_line_whose_amounts_do_not_add_up_is_refused()
    {
        Inv01Document document = Good() with
        {
            ItemList = [Good().ItemList[0] with { TotItemVal = 6000m }, Good().ItemList[1]],
        };

        Assert.Contains("TOTALS", Codes(document));
    }

    [Fact]
    public void Igst_beside_cgst_on_one_line_is_refused()
    {
        Inv01Document document = Good() with
        {
            ItemList = [Good().ItemList[0] with { IgstAmt = 1m, TotItemVal = 5930.50m }, Good().ItemList[1]],
        };

        Assert.Contains("TAX_SPLIT", Codes(document));
    }

    [Fact]
    public void A_future_date_is_refused()
    {
        Assert.Contains("DOC_DATE", Codes(Good(), today: Dated.AddDays(-1)));
    }

    [Fact]
    public void A_document_older_than_the_reporting_window_is_refused_and_one_inside_it_is_not()
    {
        Assert.Contains("DOC_DATE", Codes(Good(), today: Dated.AddDays(31), window: 30));
        Assert.DoesNotContain("DOC_DATE", Codes(Good(), today: Dated.AddDays(30), window: 30));
        Assert.DoesNotContain("DOC_DATE", Codes(Good(), today: Dated.AddDays(400)));
    }

    [Theory]
    [InlineData("0INV/1")]
    [InlineData("/INV1")]
    [InlineData("INV/2026-27/000001")]
    [InlineData("INV 1")]
    public void A_document_number_the_irp_would_refuse_is_refused(string number)
    {
        Inv01Document document = Good() with { DocDtls = Good().DocDtls with { No = number } };

        Assert.Contains("DOC_NO", Codes(document));
    }

    [Fact]
    public void A_document_with_no_lines_is_refused()
    {
        Inv01Document document = Good() with { ItemList = [] };

        Assert.Contains("ITEMS", Codes(document));
    }

    [Fact]
    public void An_export_needs_no_buyer_gstin()
    {
        EInvoiceInput input = EInvoiceMapperTests.IntraStateInvoice() with
        {
            Buyer = new Shared.Kernel.Contacts.ContactPostalAddress
            {
                ContactId = 1, LegalName = "Gulf Imports LLC", AddressLine1 = "Port Road", City = "Dubai",
            },
            BuyerRegistrationType = "Overseas",
            IsInterState = true,
            Lines = [new EInvoiceInputLine(1, "Goods", "72142090", 1m, "NOS", 0m, 1000m, [])],
            TotalAmount = 1000m,
            RoundOffAmount = 0m,
        };

        Assert.Empty(EInvoiceValidator.Validate(Inv01Mapper.Map(input), Dated, Dated));
    }

    [Fact]
    public void A_tax_row_of_any_component_counts_toward_the_line()
    {
        EInvoiceInput input = EInvoiceMapperTests.IntraStateInvoice() with
        {
            Lines =
            [
                new EInvoiceInputLine(1, "Goods", "72142090", 1m, "NOS", 0m, 1000m,
                [
                    new EInvoiceInputTax(TaxComponent.Cgst, 9m, 90m),
                    new EInvoiceInputTax(TaxComponent.Sgst, 9m, 90m),
                    new EInvoiceInputTax(TaxComponent.Cess, 1m, 10m),
                ]),
            ],
            TotalAmount = 1190m,
            RoundOffAmount = 0m,
        };

        Inv01Document document = Inv01Mapper.Map(input);

        Assert.Equal(10m, document.ValDtls.CesVal);
        Assert.Empty(EInvoiceValidator.Validate(document, Dated, Dated));
    }
}
