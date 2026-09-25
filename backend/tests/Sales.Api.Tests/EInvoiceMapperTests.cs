using System.Text.Json;
using Sales.Api.Services.EInvoicing;
using Sales.Entity.Enums;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The INV-01 mapper (TK-91), against a recorded sample in the schema's own
/// shape. Pure: no database, no gateway.
/// </summary>
public sealed class EInvoiceMapperTests
{
    internal const string SellerGstin = "33AAACH7409R1Z8";
    internal const string IntraBuyerGstin = "33AABCT1332L1ZL";
    internal const string InterBuyerGstin = "29AAGCB7383J1Z4";

    /// <summary>
    /// A B2B intra-state invoice as the IRP expects it: a stock line in KGS and a
    /// service line (SAC 99…) with a discount, CGST and SGST at 9% each, rounded
    /// up by 50 paise. Written by hand from the notified schema.
    /// </summary>
    private const string RecordedIntraStateSample = """
        {
          "Version": "1.1",
          "TranDtls": { "TaxSch": "GST", "SupTyp": "B2B", "RegRev": "N", "IgstOnIntra": "N" },
          "DocDtls": { "Typ": "INV", "No": "INV/26-27/0042", "Dt": "20/09/2026" },
          "SellerDtls": {
            "Gstin": "33AAACH7409R1Z8", "LglNm": "Test Traders", "Addr1": "1 Test Street",
            "Loc": "Chennai", "Pin": 600001, "Stcd": "33", "Ph": "9840012345", "Em": "accounts@test.example"
          },
          "BuyerDtls": {
            "Gstin": "33AABCT1332L1ZL", "LglNm": "Kaveri Constructions Pvt Ltd", "Pos": "33",
            "Addr1": "12 Anna Salai", "Addr2": "Teynampet", "Loc": "Chennai", "Pin": 600018, "Stcd": "33"
          },
          "ItemList": [
            {
              "SlNo": "1", "PrdDesc": "Steel rod 12mm", "IsServc": "N", "HsnCd": "72142090",
              "Qty": 100.5, "Unit": "KGS", "UnitPrice": 50, "TotAmt": 5025.00, "Discount": 0,
              "AssAmt": 5025.00, "GstRt": 18, "IgstAmt": 0, "CgstAmt": 452.25, "SgstAmt": 452.25,
              "CesRt": 0, "CesAmt": 0, "TotItemVal": 5929.50
            },
            {
              "SlNo": "2", "PrdDesc": "Installation", "IsServc": "Y", "HsnCd": "998719",
              "Qty": 1, "UnitPrice": 1000, "TotAmt": 1000.00, "Discount": 100.00,
              "AssAmt": 900.00, "GstRt": 18, "IgstAmt": 0, "CgstAmt": 81.00, "SgstAmt": 81.00,
              "CesRt": 0, "CesAmt": 0, "TotItemVal": 1062.00
            }
          ],
          "ValDtls": {
            "AssVal": 5925.00, "CgstVal": 533.25, "SgstVal": 533.25, "IgstVal": 0, "CesVal": 0,
            "Discount": 0, "OthChrg": 0, "RndOffAmt": 0.50, "TotInvVal": 6992.00
          }
        }
        """;

    [Fact]
    public void A_b2b_intra_state_invoice_maps_to_the_recorded_sample()
    {
        Inv01Document document = Inv01Mapper.Map(IntraStateInvoice());

        AssertJsonEquivalent(RecordedIntraStateSample, Inv01Json.Serialize(document));
    }

    [Fact]
    public void The_recorded_sample_passes_local_validation()
    {
        Inv01Document document = Inv01Mapper.Map(IntraStateInvoice());

        Assert.Empty(EInvoiceValidator.Validate(document, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 21)));
    }

    [Fact]
    public void Nulls_are_left_out_and_names_are_the_schemas_own()
    {
        string json = Inv01Json.Serialize(Inv01Mapper.Map(IntraStateInvoice()));

        Assert.DoesNotContain("null", json);
        Assert.Contains("\"SellerDtls\"", json);
        Assert.DoesNotContain("\"sellerDtls\"", json);
        Assert.DoesNotContain("\"RefDtls\"", json);
    }

    [Fact]
    public void An_inter_state_invoice_carries_igst_and_the_buyers_state_as_place_of_supply()
    {
        EInvoiceInput input = IntraStateInvoice() with
        {
            Buyer = Buyer(InterBuyerGstin, "29", "560001", "Bengaluru"),
            IsInterState = true,
            Lines =
            [
                new EInvoiceInputLine(1, "Steel rod 12mm", "72142090", 10m, "KGS", 0m, 1000m,
                    [new EInvoiceInputTax(TaxComponent.Igst, 18m, 180m)]),
            ],
            TotalAmount = 1180m,
            RoundOffAmount = 0m,
        };

        Inv01Document document = Inv01Mapper.Map(input);

        Assert.Equal("29", document.BuyerDtls.Pos);
        Assert.Equal(18m, document.ItemList[0].GstRt);
        Assert.Equal(180m, document.ItemList[0].IgstAmt);
        Assert.Equal(0m, document.ItemList[0].CgstAmt);
        Assert.Equal(180m, document.ValDtls.IgstVal);
    }

    [Fact]
    public void Utgst_is_reported_in_the_sgst_column()
    {
        EInvoiceInput input = IntraStateInvoice() with
        {
            Lines =
            [
                new EInvoiceInputLine(1, "Goods", "72142090", 1m, "NOS", 0m, 100m,
                [
                    new EInvoiceInputTax(TaxComponent.Cgst, 9m, 9m),
                    new EInvoiceInputTax(TaxComponent.Utgst, 9m, 9m),
                ]),
            ],
            TotalAmount = 118m,
            RoundOffAmount = 0m,
        };

        Inv01Item item = Inv01Mapper.Map(input).ItemList[0];

        Assert.Equal(9m, item.SgstAmt);
        Assert.Equal(18m, item.GstRt);
    }

    [Fact]
    public void A_settlement_discount_below_the_lines_goes_in_the_value_blocks_discount()
    {
        // 1180 by the lines, 1150 payable: 30 off that did not reduce the taxable value.
        EInvoiceInput input = IntraStateInvoice() with
        {
            Lines =
            [
                new EInvoiceInputLine(1, "Goods", "72142090", 1m, "NOS", 0m, 1000m,
                [
                    new EInvoiceInputTax(TaxComponent.Cgst, 9m, 90m),
                    new EInvoiceInputTax(TaxComponent.Sgst, 9m, 90m),
                ]),
            ],
            TotalAmount = 1150m,
            RoundOffAmount = 0m,
        };

        Inv01Document document = Inv01Mapper.Map(input);

        Assert.Equal(30m, document.ValDtls.Discount);
        Assert.Equal(1000m, document.ItemList[0].AssAmt);
        Assert.Empty(EInvoiceValidator.Validate(document, input.DocumentDate, input.DocumentDate));
    }

    [Fact]
    public void A_credit_note_is_a_crn_and_names_the_invoice_it_corrects()
    {
        EInvoiceInput input = IntraStateInvoice() with
        {
            Kind = EInvoiceSource.CreditNote,
            DocumentNo = "CRN/26-27/0003",
            PrecedingDocumentNo = "INV/26-27/0042",
            PrecedingDocumentDate = new DateOnly(2026, 9, 20),
        };

        Inv01Document document = Inv01Mapper.Map(input);

        Assert.Equal("CRN", document.DocDtls.Typ);
        Assert.NotNull(document.RefDtls);
        Inv01PrecedingDocument preceding = Assert.Single(document.RefDtls!.PrecDocDtls);
        Assert.Equal("INV/26-27/0042", preceding.InvNo);
        Assert.Equal("20/09/2026", preceding.InvDt);
    }

    [Fact]
    public void An_export_names_an_unregistered_buyer_in_state_96()
    {
        EInvoiceInput input = IntraStateInvoice() with
        {
            Buyer = Buyer(null, null, null, "Dubai"),
            BuyerRegistrationType = "Overseas",
            IsInterState = true,
            Lines =
            [
                new EInvoiceInputLine(1, "Goods", "72142090", 1m, "NOS", 0m, 1000m, []),
            ],
            TotalAmount = 1000m,
            RoundOffAmount = 0m,
        };

        Inv01Document document = Inv01Mapper.Map(input);

        Assert.Equal("EXPWOP", document.TranDtls.SupTyp);
        Assert.Equal("URP", document.BuyerDtls.Gstin);
        Assert.Equal("96", document.BuyerDtls.Pos);
        Assert.Equal("96", document.BuyerDtls.Stcd);
        Assert.Equal(999999, document.BuyerDtls.Pin);
    }

    [Theory]
    [InlineData("Regular", InterBuyerGstin, false, "B2B")]
    [InlineData("Composition", InterBuyerGstin, false, "B2B")]
    [InlineData("Sez", InterBuyerGstin, true, "SEZWP")]
    [InlineData("Sez", InterBuyerGstin, false, "SEZWOP")]
    [InlineData("Overseas", null, true, "EXPWP")]
    [InlineData("Consumer", null, false, null)]
    [InlineData("Unregistered", null, false, null)]
    [InlineData("Regular", "", false, null)]
    public void Which_supplies_need_an_irn(string registration, string? gstin, bool igst, string? expected)
    {
        Assert.Equal(expected, EInvoiceApplicability.SupplyTypeFor(registration, gstin, igst));
    }

    [Fact]
    public void A_branch_applies_from_its_start_date_on()
    {
        var from = new DateOnly(2026, 10, 1);

        Assert.False(EInvoiceApplicability.BranchApplies(null, new DateOnly(2026, 10, 5)));
        Assert.False(EInvoiceApplicability.BranchApplies(from, new DateOnly(2026, 9, 30)));
        Assert.True(EInvoiceApplicability.BranchApplies(from, from));
    }

    internal static EInvoiceInput IntraStateInvoice() => new(
        EInvoiceSource.Invoice,
        "INV/26-27/0042",
        new DateOnly(2026, 9, 20),
        new OrgIdentity("Test Traders", SellerGstin, "1 Test Street", null, "Chennai", "33", "600001",
            "98400 12345", "accounts@test.example"),
        new ContactPostalAddress
        {
            ContactId = 42,
            LegalName = "Kaveri Constructions Pvt Ltd",
            Gstin = IntraBuyerGstin,
            AddressLine1 = "12 Anna Salai",
            AddressLine2 = "Teynampet",
            City = "Chennai",
            PostalCode = "600018",
            StateCode = "33",
            RegistrationType = "Regular",
        },
        "Regular",
        IsInterState: false,
        [
            new EInvoiceInputLine(1, "Steel rod 12mm", "72142090", 100.5m, "KGS", 0m, 5025.00m,
            [
                new EInvoiceInputTax(TaxComponent.Cgst, 9m, 452.25m),
                new EInvoiceInputTax(TaxComponent.Sgst, 9m, 452.25m),
            ]),
            new EInvoiceInputLine(2, "Installation", "998719", 1m, null, 100.00m, 900.00m,
            [
                new EInvoiceInputTax(TaxComponent.Cgst, 9m, 81.00m),
                new EInvoiceInputTax(TaxComponent.Sgst, 9m, 81.00m),
            ]),
        ],
        TotalAmount: 6992.00m,
        RoundOffAmount: 0.50m);

    private static ContactPostalAddress Buyer(string? gstin, string? stateCode, string? pin, string city) => new()
    {
        ContactId = 42,
        LegalName = "Buyer Trading Co",
        Gstin = gstin,
        AddressLine1 = "5 Market Road",
        City = city,
        PostalCode = pin,
        StateCode = stateCode,
    };

    /// <summary>
    /// Structural equality, with numbers compared as values: <c>50</c> and
    /// <c>50.000</c> are the same amount, and the IRP reads them alike.
    /// </summary>
    internal static void AssertJsonEquivalent(string expected, string actual)
    {
        using JsonDocument e = JsonDocument.Parse(expected);
        using JsonDocument a = JsonDocument.Parse(actual);
        Compare(e.RootElement, a.RootElement, "$");
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind, $"{path}: expected {expected.ValueKind}, got {actual.ValueKind}");

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
                var actualNames = actual.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
                Assert.True(expectedNames.SequenceEqual(actualNames),
                    $"{path}: expected keys [{string.Join(", ", expectedNames)}], got [{string.Join(", ", actualNames)}]");
                foreach (JsonProperty property in expected.EnumerateObject())
                {
                    Compare(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                Assert.True(expected.GetArrayLength() == actual.GetArrayLength(), $"{path}: array lengths differ");
                for (int i = 0; i < expected.GetArrayLength(); i++)
                {
                    Compare(expected[i], actual[i], $"{path}[{i}]");
                }

                break;
            case JsonValueKind.Number:
                Assert.True(expected.GetDecimal() == actual.GetDecimal(), $"{path}: expected {expected}, got {actual}");
                break;
            default:
                Assert.True(expected.ToString() == actual.ToString(), $"{path}: expected '{expected}', got '{actual}'");
                break;
        }
    }
}
