using Purchase.Entity.Enums;
using Purchase.Entity.TableEntities;
using Purchase.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// What a purchase document prints as, tested without a service or a database: the payload the four print
/// routes (order, receipt, bill, debit note) send to Printing. Written by an AI and not run (section 0.5 of
/// docs/Task_Details.md).
/// </summary>
public class PurchasePrintPayloadTests
{
    private static readonly OrgIdentity Buyer = new(
        "Sample Retail", "33AAAAA0000A1Z5", "12 Mount Road", null, "Chennai", "33", "600002", "9876500000", "buy@example.test");

    private static Bill NewBill(params BillDetail[] lines) => new()
    {
        BillId = 5,
        TransactionTypeCode = "BIL",
        DocumentNo = "BIL-0005",
        DocumentDate = new DateOnly(2026, 10, 27),
        ContactId = 9,
        ContactGstin = "29BBBBB1111B1Z9",
        BillingAddress = "Vendor Lane\nBengaluru",
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        SubTotal = 300m,
        TaxableAmount = 300m,
        CgstAmount = 27m,
        SgstAmount = 27m,
        TotalAmount = 354m,
        Status = DocumentStatus.Posted,
        VendorBillNo = "V-77",
        Lines = [.. lines],
    };

    private static BillDetail Line(int number, long? itemId, decimal taxable, params BillDetailTax[] taxes) => new()
    {
        LineNumber = number,
        ItemId = itemId,
        Description = itemId is null ? "Freight" : null,
        HsnSacCode = "7308",
        Quantity = 2m,
        UnitPrice = taxable / 2m,
        TaxableAmount = taxable,
        LineTotal = taxable + taxes.Sum(t => t.Amount),
        Taxes = [.. taxes],
    };

    private static BillDetailTax Tax(TaxComponent component, decimal rate, decimal taxable) => new()
    {
        TaxComponent = component,
        Rate = rate,
        TaxableAmount = taxable,
        Amount = taxable * rate / 100m,
    };

    private static List<PurchasePrintLine> Entries(Bill bill) =>
        [.. bill.Lines.Select(l => new PurchasePrintLine(l, [.. l.Taxes]))];

    [Fact]
    public void The_organization_is_the_buyer_and_the_party_is_the_vendor()
    {
        Bill bill = NewBill();

        var payload = PurchasePrintPayloadRepository.Build(bill, Entries(bill), Buyer, "Steelco Supplies", new Dictionary<long, string>());

        Assert.Equal("Sample Retail", payload.Singles["Organization.Name"]);
        Assert.Equal("33AAAAA0000A1Z5", payload.Singles["Organization.Gstin"]);
        Assert.Equal("Steelco Supplies", payload.Singles["Party.Name"]);
        Assert.Equal("29BBBBB1111B1Z9", payload.Singles["Party.Gstin"]);
        Assert.Equal("BIL-0005", payload.Singles["Document.No"]);
        Assert.Equal(354m, payload.Singles["Totals.GrandTotal"]);
        Assert.Equal(54m, payload.Singles["Totals.Tax"]);
    }

    [Fact]
    public void Text_typed_into_an_address_is_escaped_and_keeps_its_line_breaks()
    {
        Bill bill = NewBill();
        bill.BillingAddress = "A <b>Lane</b>\nBengaluru";

        var payload = PurchasePrintPayloadRepository.Build(bill, Entries(bill), Buyer, "V", new Dictionary<long, string>());

        Assert.Equal("A &lt;b&gt;Lane&lt;/b&gt;<br>Bengaluru", payload.Singles["Party.Address"]);
    }

    [Fact]
    public void A_line_prints_its_item_name_or_falls_back_to_its_description()
    {
        Bill bill = NewBill(Line(1, 7, 100m), Line(2, null, 200m), Line(3, 8, 50m));

        var payload = PurchasePrintPayloadRepository.Build(
            bill, Entries(bill), Buyer, "V", new Dictionary<long, string> { [7] = "Steel bar" });

        var items = payload.Lists["Item"];
        Assert.Equal(["Steel bar", "Freight", null], items.Select(i => i["Item.ItemName"]).ToArray());
        Assert.Equal([1, 2, 3], items.Select(i => i["Item.SlNo"]).ToArray());
    }

    [Fact]
    public void Lines_come_out_in_line_number_order_whatever_order_they_were_loaded_in()
    {
        Bill bill = NewBill(Line(3, 7, 30m), Line(1, 7, 10m), Line(2, 7, 20m));

        var payload = PurchasePrintPayloadRepository.Build(bill, Entries(bill), Buyer, "V", new Dictionary<long, string>());

        Assert.Equal([1, 2, 3], payload.Lists["Item"].Select(i => i["Item.SlNo"]).ToArray());
    }

    [Fact]
    public void A_line_prints_the_sum_of_its_component_rates_as_its_tax_rate()
    {
        Bill bill = NewBill(Line(1, 7, 100m, Tax(TaxComponent.Cgst, 9m, 100m), Tax(TaxComponent.Sgst, 9m, 100m)));

        var payload = PurchasePrintPayloadRepository.Build(bill, Entries(bill), Buyer, "V", new Dictionary<long, string>());

        Assert.Equal(18m, payload.Lists["Item"].Single()["Item.TaxRate"]);
        Assert.Equal(118m, payload.Lists["Item"].Single()["Item.Amount"]);
    }

    [Fact]
    public void Tax_is_listed_per_component_and_per_rate_across_lines()
    {
        Bill bill = NewBill(
            Line(1, 7, 100m, Tax(TaxComponent.Cgst, 9m, 100m), Tax(TaxComponent.Sgst, 9m, 100m)),
            Line(2, 8, 200m, Tax(TaxComponent.Cgst, 9m, 200m), Tax(TaxComponent.Sgst, 9m, 200m)),
            Line(3, 9, 50m, Tax(TaxComponent.Cgst, 2.5m, 50m), Tax(TaxComponent.Sgst, 2.5m, 50m)));

        var payload = PurchasePrintPayloadRepository.Build(bill, Entries(bill), Buyer, "V", new Dictionary<long, string>());

        var rows = payload.Lists["Tax"];
        Assert.Equal(4, rows.Count);
        Assert.Equal(["CGST", "CGST", "SGST", "SGST"], rows.Select(r => r["Tax.Component"]).ToArray());

        var cgst9 = rows.Single(r => (string?)r["Tax.Component"] == "CGST" && (decimal?)r["Tax.Rate"] == 9m);
        Assert.Equal(300m, cgst9["Tax.TaxableValue"]);
        Assert.Equal(27m, cgst9["Tax.Amount"]);
    }

    [Fact]
    public void A_goods_receipt_line_says_how_much_of_it_was_kept()
    {
        var accepted = new GoodsReceiptDetail { Quantity = 10m, AcceptedQuantity = 8m, RejectedQuantity = 2m, RejectionReason = " Damaged " };
        var whole = new GoodsReceiptDetail { Quantity = 10m, AcceptedQuantity = 10m, RejectedQuantity = 0m };
        var noReason = new GoodsReceiptDetail { Quantity = 10m, AcceptedQuantity = 9m, RejectedQuantity = 1m };

        Assert.Equal("Accepted 8, rejected 2 (Damaged)", PurchasePrintRepository.RejectionNote(accepted));
        Assert.Null(PurchasePrintRepository.RejectionNote(whole));
        Assert.Equal("Accepted 9, rejected 1", PurchasePrintRepository.RejectionNote(noReason));
    }

    [Fact]
    public void A_description_suffix_is_added_to_the_line_description_with_a_dash()
    {
        Bill bill = NewBill(Line(1, null, 100m));
        var entries = new List<PurchasePrintLine> { new(bill.Lines[0], [], "Accepted 1, rejected 1") };

        var payload = PurchasePrintPayloadRepository.Build(bill, entries, Buyer, "V", new Dictionary<long, string>());

        Assert.Equal("Freight — Accepted 1, rejected 1", payload.Lists["Item"].Single()["Item.Description"]);
    }

    [Theory]
    [InlineData(29, "33", "29")]
    [InlineData(7, "33", "07")]
    [InlineData(0, "33", "33")]
    [InlineData(0, " 33 ", "33")]
    [InlineData(0, null, null)]
    [InlineData(0, "", null)]
    public void The_place_of_supply_is_the_documents_state_else_the_branchs(int stateId, string? branchState, string? expected) =>
        Assert.Equal(expected, PurchasePrintPayloadRepository.PlaceOfSupply(stateId, branchState));

    [Theory]
    [InlineData(DocumentStatus.Posted, null)]
    [InlineData(DocumentStatus.Void, "VOID")]
    [InlineData(DocumentStatus.Draft, "DRAFT")]
    [InlineData(DocumentStatus.ReadyToPost, "DRAFT")]
    public void Only_a_posted_document_prints_without_a_stamp(DocumentStatus status, string? expected) =>
        Assert.Equal(expected, PurchasePrintPayloadRepository.Watermark(status));

    [Fact]
    public void Every_debit_note_reason_has_a_label_the_vendor_can_read()
    {
        foreach (DebitNoteReason reason in Enum.GetValues<DebitNoteReason>())
        {
            string label = PurchasePrintRepository.ReasonLabel(reason);

            Assert.False(string.IsNullOrWhiteSpace(label));
            // A label that is just the enum name means a value was added and this was not updated.
            Assert.NotEqual(reason.ToString(), label);
        }
    }
}
