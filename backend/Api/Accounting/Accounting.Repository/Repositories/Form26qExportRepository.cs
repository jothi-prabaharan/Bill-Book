using System.Globalization;
using System.IO.Compression;
using System.Text;
using Accounting.Entity.Enums;

namespace Accounting.Repository.Repositories;

/// <summary>A challan as the 26Q challan sheet lists it.</summary>
public sealed record Form26qChallan(
    long SpendMoneyId,
    string SectionCode,
    decimal Tds,
    string? BsrCode,
    string? ChallanNo,
    DateOnly DepositDate);

/// <summary>A deduction as the 26Q deductee sheet lists it.</summary>
public sealed record Form26qDeduction(
    long? ChallanSpendMoneyId,
    string SectionCode,
    string? Pan,
    string DeducteeName,
    DateOnly CreditDate,
    decimal Amount,
    decimal TdsAmount,
    decimal Rate,
    string? CertificateNo,
    TdsDeducteeKind? DeducteeKind);

/// <summary>What a quarter's export holds, and what is wrong with it.</summary>
public sealed record Form26qExport(byte[] Zip, int Challans, int Deductions, IReadOnlyList<string> Warnings);

/// <summary>
/// The quarterly TDS return's data (TK-117): a zip of two CSV sheets in the
/// column order of the return preparation utility's challan and deductee
/// tables, for pasting into it. Filing and Form 16A stay on the government's
/// tools. Pure.
///
/// Each deduction names the serial number of the challan that paid it; an
/// undeposited deduction names none, and is listed in the warnings because
/// the utility will refuse it. Deductee code 01 is a company (PAN holder C),
/// 02 everyone else. The lower-deduction reason is A for a certificate and C
/// for the higher no-PAN rate.
/// </summary>
public static class Form26qExportRepository
{
    public static (DateOnly From, DateOnly To) QuarterOf(int financialYear, int quarter)
    {
        DateOnly from = quarter switch
        {
            1 => new DateOnly(financialYear, 4, 1),
            2 => new DateOnly(financialYear, 7, 1),
            3 => new DateOnly(financialYear, 10, 1),
            4 => new DateOnly(financialYear + 1, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "A quarter is 1 to 4."),
        };

        return (from, from.AddMonths(3).AddDays(-1));
    }

    public static Form26qExport Build(IReadOnlyList<Form26qChallan> challans, IReadOnlyList<Form26qDeduction> deductions)
    {
        List<string> warnings = [];
        List<Form26qChallan> ordered = [.. challans.OrderBy(c => c.DepositDate).ThenBy(c => c.SpendMoneyId)];
        Dictionary<long, int> serials = ordered.Select((c, i) => (c.SpendMoneyId, Serial: i + 1)).ToDictionary(x => x.SpendMoneyId, x => x.Serial);

        StringBuilder challanSheet = new();
        challanSheet.AppendLine(Row("Sr No", "Section Code", "TDS", "Surcharge", "Education Cess", "Interest", "Fee",
            "Others", "Total Tax Deposited", "BSR Code", "Challan Serial No", "Date of Deposit", "Minor Head"));

        foreach (Form26qChallan c in ordered)
        {
            if (string.IsNullOrWhiteSpace(c.BsrCode) || string.IsNullOrWhiteSpace(c.ChallanNo))
            {
                warnings.Add($"The challan paid on {c.DepositDate:dd-MM-yyyy} for {c.SectionCode} has no BSR code or challan number.");
            }

            challanSheet.AppendLine(Row(
                serials[c.SpendMoneyId].ToString(CultureInfo.InvariantCulture), c.SectionCode, Money(c.Tds), "0.00", "0.00", "0.00", "0.00",
                "0.00", Money(c.Tds), c.BsrCode ?? string.Empty, c.ChallanNo ?? string.Empty, c.DepositDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), "200"));
        }

        StringBuilder deducteeSheet = new();
        deducteeSheet.AppendLine(Row("Sr No", "Challan Sr No", "Section Code", "Deductee Code", "PAN", "Deductee Name",
            "Date of Payment or Credit", "Amount Paid or Credited", "TDS", "Surcharge", "Education Cess", "Total Tax Deducted",
            "Total Tax Deposited", "Date of Deduction", "Rate", "Reason for Lower or Higher Deduction", "Certificate Number"));

        int serial = 0;
        foreach (Form26qDeduction d in deductions.OrderBy(d => d.CreditDate).ThenBy(d => d.DeducteeName, StringComparer.Ordinal))
        {
            serial++;
            string challanSerial = d.ChallanSpendMoneyId is long paid && serials.TryGetValue(paid, out int s) ? s.ToString(CultureInfo.InvariantCulture) : string.Empty;
            if (challanSerial.Length == 0)
            {
                warnings.Add($"{d.DeducteeName}'s deduction of {Money(d.TdsAmount)} on {d.CreditDate:dd-MM-yyyy} ({d.SectionCode}) has not been deposited on a challan.");
            }

            bool noPan = d.DeducteeKind == TdsDeducteeKind.NoPan || string.IsNullOrWhiteSpace(d.Pan);
            string code = !noPan && d.Pan!.Length >= 4 && char.ToUpperInvariant(d.Pan[3]) == 'C' ? "01" : "02";
            string reason = d.CertificateNo is { Length: > 0 } ? "A" : noPan ? "C" : string.Empty;

            deducteeSheet.AppendLine(Row(
                serial.ToString(CultureInfo.InvariantCulture), challanSerial, d.SectionCode, code,
                noPan ? "PANNOTAVBL" : d.Pan!, d.DeducteeName,
                d.CreditDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), Money(d.Amount), Money(d.TdsAmount), "0.00", "0.00",
                Money(d.TdsAmount), challanSerial.Length == 0 ? "0.00" : Money(d.TdsAmount),
                d.CreditDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                d.Rate.ToString("0.###", CultureInfo.InvariantCulture), reason, d.CertificateNo ?? string.Empty));
        }

        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "challans.csv", challanSheet.ToString());
            Write(zip, "deductees.csv", deducteeSheet.ToString());
        }

        return new Form26qExport(buffer.ToArray(), ordered.Count, serial, warnings);
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();

        // With a BOM, so Excel reads names in any script correctly.
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        stream.Write(bytes);
    }

    private static string Row(params string[] cells) => string.Join(",", cells.Select(Cell));

    private static string Cell(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
