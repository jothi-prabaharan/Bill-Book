using System.Globalization;
using System.Text.Json.Nodes;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tax;

namespace Reporting.Repository.Repositories;

/// <summary>One series of documents issued in the period, for the documents-issued table.</summary>
public sealed record IssuedDocuments(int Nature, string First, string Last, int Total, int Cancelled);

/// <summary>What an export produced: the JSON, or the reasons it was refused.</summary>
public sealed record Gstr1ExportResult(JsonObject? Payload, IReadOnlyList<string> Problems, decimal SectionTaxable, decimal HsnTaxable)
{
    public bool IsValid => Problems.Count == 0;
}

/// <summary>
/// GSTR-1 as the GST offline tool imports it (TK-115): B2B, B2CL, B2CS, CDNR,
/// EXP, the HSN summary and the documents issued, from <c>sal.SalesRegister</c>.
/// Pure, so the rules are tested without a database.
///
/// <b>Validated before anything is handed over</b>: the branch has a GSTIN,
/// every B2B and CDNR row a valid one, every line an HSN code, and the HSN
/// summary adds up to the sections within a rupee. A file that fails any of
/// these is refused with the list, because the portal would refuse it anyway
/// and say less.
///
/// Where the design is simplified, it says so here rather than in a surprise:
/// a credit note to an unregistered buyer is netted into B2CS rather than
/// reported under CDNUR, since the original invoice's value is not in the
/// register; and an export is recognised by its place of supply being "96".
/// </summary>
public static class Gstr1ExportRepository
{
    /// <summary>An inter-state invoice to an unregistered buyer above this is B2CL (from 1 August 2024).</summary>
    public const decimal B2clThreshold = 100_000m;

    /// <summary>Documents-issued natures: invoices for outward supply, and credit notes.</summary>
    public const int InvoicesNature = 1;
    public const int CreditNotesNature = 5;

    public static Gstr1ExportResult Build(
        string? branchGstin,
        DateOnly from,
        DateOnly to,
        IEnumerable<SalesRegisterRead> register,
        IReadOnlyDictionary<int, string> stateCodes,
        IReadOnlyList<IssuedDocuments> issued)
    {
        List<SalesRegisterRead> rows =
        [
            .. register
                .Where(r => r.DocumentDate >= from && r.DocumentDate <= to && Gstr3bRepository.Counts(r.TransactionTypeCode))
                .OrderBy(r => r.DocumentDate)
                .ThenBy(r => r.DocumentNo, StringComparer.Ordinal),
        ];

        List<string> problems = [];

        if (!Gstin.IsValid(branchGstin))
        {
            problems.Add("The branch has no valid GSTIN. Set it in Settings › Organization before exporting.");
        }

        string Pos(SalesRegisterRead r) => stateCodes.TryGetValue(r.PlaceOfSupplyStateId, out string? code) ? code : string.Empty;

        foreach (SalesRegisterRead r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.HsnSacCode))
            {
                problems.Add($"{r.DocumentNo} has a line with no HSN or SAC code.");
            }

            if (!string.IsNullOrWhiteSpace(r.ContactGstin) && !Gstin.IsValid(r.ContactGstin))
            {
                problems.Add($"{r.DocumentNo} names the GSTIN {r.ContactGstin}, which is not valid.");
            }

            if (Pos(r).Length == 0)
            {
                problems.Add($"{r.DocumentNo} has a place of supply with no state code.");
            }
        }

        bool Registered(SalesRegisterRead r) => Gstin.IsValid(r.ContactGstin);
        bool Export(SalesRegisterRead r) => Pos(r) == Gstr3bRepository.ForeignStateCode;
        bool Credit(SalesRegisterRead r) => r.TransactionTypeCode == "CRN";

        // A document's value is its lines' taxable value and tax together.
        Dictionary<(string, long), decimal> documentValue = rows
            .GroupBy(r => (r.TransactionTypeCode, r.SourceId))
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(r => r.TaxableAmount + Tax(r))));

        bool LargeUnregistered(SalesRegisterRead r) =>
            !Registered(r) && !Credit(r) && !Export(r) && r.IsInterState
            && documentValue[(r.TransactionTypeCode, r.SourceId)] > B2clThreshold;

        List<SalesRegisterRead> b2bRows = [.. rows.Where(r => Registered(r) && !Credit(r) && !Export(r))];
        List<SalesRegisterRead> cdnrRows = [.. rows.Where(r => Registered(r) && Credit(r))];
        List<SalesRegisterRead> expRows = [.. rows.Where(r => Export(r) && !Credit(r))];
        List<SalesRegisterRead> b2clRows = [.. rows.Where(LargeUnregistered)];
        List<SalesRegisterRead> b2csRows = [.. rows.Where(r => !Registered(r) && !Export(r) && !LargeUnregistered(r))];

        JsonArray b2b = new(
            [.. b2bRows.GroupBy(r => r.ContactGstin!.ToUpperInvariant()).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["ctin"] = g.Key,
                    ["inv"] = new JsonArray([.. Invoices(g, documentValue, Pos, withReverseCharge: true)]),
                })]);

        JsonArray b2cl = new(
            [.. b2clRows.GroupBy(Pos).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["pos"] = g.Key,
                    ["inv"] = new JsonArray([.. Invoices(g, documentValue, Pos, withReverseCharge: false)]),
                })]);

        JsonArray b2cs = new(
            [.. b2csRows.GroupBy(r => (Inter: r.IsInterState, Pos: Pos(r), Rate: r.GstRate))
                .OrderBy(g => g.Key.Pos, StringComparer.Ordinal).ThenBy(g => g.Key.Rate)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["sply_ty"] = g.Key.Inter ? "INTER" : "INTRA",
                    ["pos"] = g.Key.Pos,
                    ["typ"] = "OE",
                    ["rt"] = g.Key.Rate,
                    ["txval"] = Money(g.Sum(r => r.TaxableAmount)),
                    ["iamt"] = Money(g.Sum(r => r.IgstAmount)),
                    ["camt"] = Money(g.Sum(r => r.CgstAmount)),
                    ["samt"] = Money(g.Sum(r => r.SgstAmount)),
                    ["csamt"] = Money(g.Sum(r => r.CessAmount)),
                })]);

        // Credit notes are negative in the register; the return wants them positive under "C".
        JsonArray cdnr = new(
            [.. cdnrRows.GroupBy(r => r.ContactGstin!.ToUpperInvariant()).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["ctin"] = g.Key,
                    ["nt"] = new JsonArray(
                        [.. g.GroupBy(r => r.SourceId).Select(n => (JsonNode)new JsonObject
                        {
                            ["ntty"] = "C",
                            ["nt_num"] = n.First().DocumentNo,
                            ["nt_dt"] = Date(n.First().DocumentDate),
                            ["val"] = Money(documentValue[("CRN", n.Key)]),
                            ["pos"] = Pos(n.First()),
                            ["rchrg"] = "N",
                            ["inv_typ"] = "R",
                            ["itms"] = new JsonArray([.. Items(n, negate: true)]),
                        })]),
                })]);

        JsonArray exp = new(
            [.. expRows.GroupBy(r => r.IgstAmount != 0 ? "WPAY" : "WOPAY").OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["exp_typ"] = g.Key,
                    ["inv"] = new JsonArray(
                        [.. g.GroupBy(r => (r.TransactionTypeCode, r.SourceId)).Select(i => (JsonNode)new JsonObject
                        {
                            ["inum"] = i.First().DocumentNo,
                            ["idt"] = Date(i.First().DocumentDate),
                            ["val"] = Money(documentValue[i.Key]),
                            ["itms"] = new JsonArray(
                                [.. i.GroupBy(r => r.GstRate).Select(x => (JsonNode)new JsonObject
                                {
                                    ["rt"] = x.Key,
                                    ["txval"] = Money(x.Sum(r => r.TaxableAmount)),
                                    ["iamt"] = Money(x.Sum(r => r.IgstAmount)),
                                    ["csamt"] = Money(x.Sum(r => r.CessAmount)),
                                })]),
                        })]),
                })]);

        List<IGrouping<(string, string, decimal), SalesRegisterRead>> hsnGroups =
        [
            .. rows.GroupBy(r => (Hsn: r.HsnSacCode ?? string.Empty, Uqc: r.UqcCode ?? "OTH", Rate: r.GstRate))
                .OrderBy(g => g.Key.Hsn, StringComparer.Ordinal).ThenBy(g => g.Key.Rate),
        ];

        JsonArray hsn = new(
            [.. hsnGroups.Select((g, i) => (JsonNode)new JsonObject
            {
                ["num"] = i + 1,
                ["hsn_sc"] = g.Key.Item1,
                ["uqc"] = g.Key.Item2,
                ["qty"] = Math.Round(g.Sum(r => r.Quantity), 3),
                ["rt"] = g.Key.Item3,
                ["txval"] = Money(g.Sum(r => r.TaxableAmount)),
                ["iamt"] = Money(g.Sum(r => r.IgstAmount)),
                ["camt"] = Money(g.Sum(r => r.CgstAmount)),
                ["samt"] = Money(g.Sum(r => r.SgstAmount)),
                ["csamt"] = Money(g.Sum(r => r.CessAmount)),
            })]);

        // The sections, credit notes subtracted, against the HSN summary.
        decimal sectionTaxable = Money(b2bRows.Sum(r => r.TaxableAmount) + b2clRows.Sum(r => r.TaxableAmount)
            + b2csRows.Sum(r => r.TaxableAmount) + expRows.Sum(r => r.TaxableAmount) + cdnrRows.Sum(r => r.TaxableAmount));
        decimal hsnTaxable = Money(hsnGroups.Sum(g => g.Sum(r => r.TaxableAmount)));

        if (Math.Abs(sectionTaxable - hsnTaxable) > 1m)
        {
            problems.Add($"The HSN summary's taxable value ({hsnTaxable:N2}) does not equal the sections' ({sectionTaxable:N2}).");
        }

        JsonArray docs = new(
            [.. issued.GroupBy(d => d.Nature).OrderBy(g => g.Key).Select(g => (JsonNode)new JsonObject
            {
                ["doc_num"] = g.Key,
                ["docs"] = new JsonArray(
                    [.. g.Select((d, i) => (JsonNode)new JsonObject
                    {
                        ["num"] = i + 1,
                        ["from"] = d.First,
                        ["to"] = d.Last,
                        ["totnum"] = d.Total,
                        ["cancel"] = d.Cancelled,
                        ["net_issue"] = d.Total - d.Cancelled,
                    })]),
            })]);

        if (problems.Count > 0)
        {
            return new Gstr1ExportResult(null, problems, sectionTaxable, hsnTaxable);
        }

        JsonObject payload = new()
        {
            ["gstin"] = branchGstin!.ToUpperInvariant(),
            ["fp"] = to.ToString("MMyyyy", CultureInfo.InvariantCulture),
            ["version"] = "GST3.2.1",
            ["hash"] = "hash",
            ["b2b"] = b2b,
            ["b2cl"] = b2cl,
            ["b2cs"] = b2cs,
            ["cdnr"] = cdnr,
            ["exp"] = exp,
            ["hsn"] = new JsonObject { ["data"] = hsn },
            ["doc_issue"] = new JsonObject { ["doc_det"] = docs },
        };

        return new Gstr1ExportResult(payload, [], sectionTaxable, hsnTaxable);
    }

    private static IEnumerable<JsonNode> Invoices(
        IEnumerable<SalesRegisterRead> rows,
        Dictionary<(string, long), decimal> values,
        Func<SalesRegisterRead, string> pos,
        bool withReverseCharge) =>
        rows.GroupBy(r => (r.TransactionTypeCode, r.SourceId)).Select(i =>
        {
            SalesRegisterRead first = i.First();
            JsonObject invoice = new()
            {
                ["inum"] = first.DocumentNo,
                ["idt"] = Date(first.DocumentDate),
                ["val"] = Money(values[i.Key]),
            };

            if (withReverseCharge)
            {
                invoice["pos"] = pos(first);
                invoice["rchrg"] = first.ReverseCharge ? "Y" : "N";
                invoice["inv_typ"] = "R";
            }

            invoice["itms"] = new JsonArray([.. Items(i, negate: false)]);
            return (JsonNode)invoice;
        });

    /// <summary>One item per rate, numbered as the portal expects (rate × 100 + serial).</summary>
    private static IEnumerable<JsonNode> Items(IEnumerable<SalesRegisterRead> rows, bool negate)
    {
        decimal sign = negate ? -1m : 1m;

        return rows.GroupBy(r => r.GstRate).OrderBy(g => g.Key).Select((g, i) => (JsonNode)new JsonObject
        {
            ["num"] = (int)(g.Key * 100) + i + 1,
            ["itm_det"] = new JsonObject
            {
                ["rt"] = g.Key,
                ["txval"] = Money(sign * g.Sum(r => r.TaxableAmount)),
                ["iamt"] = Money(sign * g.Sum(r => r.IgstAmount)),
                ["camt"] = Money(sign * g.Sum(r => r.CgstAmount)),
                ["samt"] = Money(sign * g.Sum(r => r.SgstAmount)),
                ["csamt"] = Money(sign * g.Sum(r => r.CessAmount)),
            },
        });
    }

    private static decimal Tax(SalesRegisterRead r) => r.IgstAmount + r.CgstAmount + r.SgstAmount + r.CessAmount;

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Date(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}
