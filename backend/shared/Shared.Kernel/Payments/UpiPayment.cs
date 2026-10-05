using System.Globalization;
using System.Text.RegularExpressions;

namespace Shared.Kernel.Payments;

/// <summary>
/// A UPI payment request (TK-158): the <c>upi://pay</c> link a customer's
/// payments app opens from a QR code, with the branch's UPI id, its name, the
/// amount and the invoice number filled in. Pure, so Sales prints it and the
/// till's receipt can carry the same string.
///
/// The parameters are NPCI's: <c>pa</c> the payee's virtual payment address,
/// <c>pn</c> the payee's name, <c>am</c> the amount in rupees to two places,
/// <c>cu</c> the currency (INR only), <c>tn</c> a note the payer sees and
/// <c>tr</c> a reference the payee sees on its statement.
/// </summary>
public static partial class UpiPayment
{
    /// <summary>The longest a UPI id may be, as NPCI sets it.</summary>
    public const int MaxIdLength = 255;

    [GeneratedRegex(@"^[A-Za-z0-9._-]{2,256}@[A-Za-z][A-Za-z0-9.-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>Whether the text looks like a UPI id: a handle, an <c>@</c>, and a bank's code (<c>shop@okhdfcbank</c>).</summary>
    public static bool IsValidId(string? upiId) =>
        upiId is { Length: > 0 and <= MaxIdLength } && IdPattern().IsMatch(upiId.Trim());

    /// <summary>
    /// The link for paying <paramref name="amountDue"/> against an invoice, or
    /// null when there is nothing to ask for: no valid UPI id, nothing due, or a
    /// currency other than rupees, which UPI cannot carry.
    /// </summary>
    public static string? PayUri(string? upiId, string payeeName, decimal amountDue, string invoiceNo, string currencyCode = "INR")
    {
        if (!IsValidId(upiId) || amountDue <= 0m
            || !string.Equals(currencyCode?.Trim(), "INR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string amount = Math.Round(amountDue, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
        string name = Clip(payeeName, 50);
        string reference = Clip(invoiceNo, 35);

        return "upi://pay"
            + $"?pa={Uri.EscapeDataString(upiId!.Trim())}"
            + $"&pn={Uri.EscapeDataString(name)}"
            + $"&am={amount}"
            + "&cu=INR"
            + $"&tn={Uri.EscapeDataString(Clip($"Invoice {reference}", 50))}"
            + $"&tr={Uri.EscapeDataString(reference)}";
    }

    private static string Clip(string? text, int length)
    {
        string trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}
