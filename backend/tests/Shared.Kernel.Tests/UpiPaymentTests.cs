using Shared.Kernel.Payments;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>The UPI payment link an unpaid invoice prints as a QR code (TK-158).</summary>
public sealed class UpiPaymentTests
{
    [Theory]
    [InlineData("shop@okhdfcbank", true)]
    [InlineData("kumar.stores-1@ybl", true)]
    [InlineData(" shop@paytm ", true)]
    [InlineData("shop", false)]
    [InlineData("@okhdfcbank", false)]
    [InlineData("shop@", false)]
    [InlineData("shop@ok hdfc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_upi_id_is_a_handle_an_at_and_a_bank(string? id, bool valid) =>
        Assert.Equal(valid, UpiPayment.IsValidId(id));

    [Fact]
    public void The_link_carries_the_id_the_name_the_amount_due_and_the_invoice_number()
    {
        string? uri = UpiPayment.PayUri("shop@okhdfcbank", "Kumar & Sons", 1234.5m, "INV/2627/00042");

        Assert.Equal(
            "upi://pay?pa=shop%40okhdfcbank&pn=Kumar%20%26%20Sons&am=1234.50&cu=INR"
            + "&tn=Invoice%20INV%2F2627%2F00042&tr=INV%2F2627%2F00042",
            uri);
    }

    [Fact]
    public void The_amount_is_rounded_to_paise()
    {
        Assert.Contains("&am=100.01&", UpiPayment.PayUri("shop@ybl", "Shop", 100.005m, "INV-1"));
    }

    [Theory]
    [InlineData("not-a-upi-id", 100, "INR")]
    [InlineData("shop@ybl", 0, "INR")]
    [InlineData("shop@ybl", -5, "INR")]
    [InlineData("shop@ybl", 100, "USD")]
    [InlineData(null, 100, "INR")]
    public void Nothing_is_asked_for_without_an_id_an_amount_due_or_rupees(string? id, decimal due, string currency) =>
        Assert.Null(UpiPayment.PayUri(id, "Shop", due, "INV-1", currency));
}
