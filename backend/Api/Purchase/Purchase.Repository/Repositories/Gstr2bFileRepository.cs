using System.Globalization;
using System.Text.Json;

namespace Purchase.Repository.Repositories;

/// <summary>One supplier invoice as GSTR-2B reports it.</summary>
public sealed record Gstr2bInvoice(
    string SupplierGstin,
    string? SupplierName,
    string InvoiceNo,
    DateOnly InvoiceDate,
    decimal Taxable,
    decimal Tax,
    bool ReverseCharge,
    bool ItcAvailable);

/// <summary>A parsed GSTR-2B file: whose it is, the month, and its B2B invoices.</summary>
public sealed record Gstr2bDocument(string? RecipientGstin, string ReturnPeriod, DateOnly From, DateOnly To, IReadOnlyList<Gstr2bInvoice> Invoices);

/// <summary>A file that is not a GSTR-2B the importer can read. The message is for the person importing.</summary>
public sealed class Gstr2bFormatException(string message) : Exception(message);

/// <summary>
/// Reads the GSTR-2B JSON the GST portal downloads (TK-116). Pure. Reads the
/// B2B section, the supplier invoices a claim rests on; credit and debit
/// notes (CDNR) and amendments (B2BA) are counted but not matched yet.
///
/// Lenient about the wrapper and field casing, since the portal and the
/// offline tool differ, and strict about what matching needs: a period, a
/// supplier GSTIN, a number and a date on every invoice.
/// </summary>
public static class Gstr2bFileRepository
{
    public static Gstr2bDocument Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new Gstr2bFormatException("That file is not JSON. Download GSTR-2B from the GST portal as JSON and import that file.");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (Find(root, "data") is { ValueKind: JsonValueKind.Object } data)
            {
                root = data;
            }

            string period = Text(root, "rtnprd")
                ?? throw new Gstr2bFormatException("The file names no return period (rtnprd), so it is not a GSTR-2B download.");

            if (period.Length != 6
                || !int.TryParse(period[..2], out int month) || month is < 1 or > 12
                || !int.TryParse(period[2..], out int year))
            {
                throw new Gstr2bFormatException($"The return period '{period}' is not a month (MMyyyy).");
            }

            DateOnly from = new(year, month, 1);
            DateOnly to = from.AddMonths(1).AddDays(-1);

            List<Gstr2bInvoice> invoices = [];
            JsonElement docs = Find(root, "docdata") ?? root;

            if (Find(docs, "b2b") is { ValueKind: JsonValueKind.Array } suppliers)
            {
                foreach (JsonElement supplier in suppliers.EnumerateArray())
                {
                    string gstin = (Text(supplier, "ctin") ?? throw new Gstr2bFormatException("A supplier in the file has no GSTIN (ctin).")).ToUpperInvariant();
                    string? name = Text(supplier, "trdnm");

                    if (Find(supplier, "inv") is not { ValueKind: JsonValueKind.Array } bills)
                    {
                        continue;
                    }

                    foreach (JsonElement invoice in bills.EnumerateArray())
                    {
                        string number = Text(invoice, "inum")
                            ?? throw new Gstr2bFormatException($"An invoice from {gstin} has no number (inum).");
                        string dateText = Text(invoice, "dt") ?? Text(invoice, "idt")
                            ?? throw new Gstr2bFormatException($"Invoice {number} from {gstin} has no date.");

                        if (!DateOnly.TryParseExact(dateText, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                        {
                            throw new Gstr2bFormatException($"Invoice {number} from {gstin} is dated '{dateText}', which is not dd-MM-yyyy.");
                        }

                        decimal taxable = 0m, tax = 0m;
                        if ((Find(invoice, "items") ?? Find(invoice, "itms")) is { ValueKind: JsonValueKind.Array } items)
                        {
                            foreach (JsonElement item in items.EnumerateArray())
                            {
                                JsonElement detail = Find(item, "itm_det") ?? item;
                                taxable += Number(detail, "txval");
                                tax += Number(detail, "igst") + Number(detail, "iamt")
                                    + Number(detail, "cgst") + Number(detail, "camt")
                                    + Number(detail, "sgst") + Number(detail, "samt")
                                    + Number(detail, "cess") + Number(detail, "csamt");
                            }
                        }

                        invoices.Add(new Gstr2bInvoice(
                            gstin, name, number, date, taxable, tax,
                            ReverseCharge: string.Equals(Text(invoice, "rev") ?? Text(invoice, "rchrg"), "Y", StringComparison.OrdinalIgnoreCase),
                            ItcAvailable: !string.Equals(Text(invoice, "itcavl"), "N", StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }

            return new Gstr2bDocument(Text(root, "gstin")?.ToUpperInvariant(), period, from, to, invoices);
        }
    }

    private static JsonElement? Find(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        Find(element, name) switch
        {
            { ValueKind: JsonValueKind.String } s when !string.IsNullOrWhiteSpace(s.GetString()) => s.GetString()!.Trim(),
            { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
            _ => null,
        };

    private static decimal Number(JsonElement element, string name) =>
        Find(element, name) switch
        {
            { ValueKind: JsonValueKind.Number } n when n.TryGetDecimal(out decimal value) => value,
            { ValueKind: JsonValueKind.String } s when decimal.TryParse(s.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) => value,
            _ => 0m,
        };
}
