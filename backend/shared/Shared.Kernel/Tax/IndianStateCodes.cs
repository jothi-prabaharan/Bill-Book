namespace Shared.Kernel.Tax;

/// <summary>
/// Mapping and normalization for Indian GST state codes.
/// Indian GSTINs begin with a two-digit numeric state code (01 to 38, plus 97).
/// This maps two-letter ISO-3166-2 alpha codes and state names to their official GST codes.
/// </summary>
public static class IndianStateCodes
{
    private static readonly Dictionary<string, string> IsoToGst = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JK"] = "01",
        ["HP"] = "02",
        ["PB"] = "03",
        ["CH"] = "04",
        ["UK"] = "05",
        ["UT"] = "05",
        ["UA"] = "05",
        ["HR"] = "06",
        ["DL"] = "07",
        ["RJ"] = "08",
        ["UP"] = "09",
        ["BR"] = "10",
        ["SK"] = "11",
        ["AR"] = "12",
        ["NL"] = "13",
        ["MN"] = "14",
        ["MZ"] = "15",
        ["TR"] = "16",
        ["ML"] = "17",
        ["AS"] = "18",
        ["WB"] = "19",
        ["JH"] = "20",
        ["OR"] = "21",
        ["OD"] = "21",
        ["CT"] = "22",
        ["CG"] = "22",
        ["MP"] = "23",
        ["GJ"] = "24",
        ["DH"] = "26",
        ["DD"] = "26",
        ["DN"] = "26",
        ["MH"] = "27",
        ["KA"] = "29",
        ["GA"] = "30",
        ["LD"] = "31",
        ["KL"] = "32",
        ["TN"] = "33",
        ["PY"] = "34",
        ["AN"] = "35",
        ["TG"] = "36",
        ["TS"] = "36",
        ["AP"] = "37",
        ["LA"] = "38",
        ["OT"] = "97",
    };

    private static readonly Dictionary<string, string> NameToGst = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jammu and Kashmir"] = "01",
        ["Jammu & Kashmir"] = "01",
        ["Himachal Pradesh"] = "02",
        ["Punjab"] = "03",
        ["Chandigarh"] = "04",
        ["Uttarakhand"] = "05",
        ["Uttaranchal"] = "05",
        ["Haryana"] = "06",
        ["Delhi"] = "07",
        ["National Capital Territory of Delhi"] = "07",
        ["Rajasthan"] = "08",
        ["Uttar Pradesh"] = "09",
        ["Bihar"] = "10",
        ["Sikkim"] = "11",
        ["Arunachal Pradesh"] = "12",
        ["Nagaland"] = "13",
        ["Manipur"] = "14",
        ["Mizoram"] = "15",
        ["Tripura"] = "16",
        ["Meghalaya"] = "17",
        ["Assam"] = "18",
        ["West Bengal"] = "19",
        ["Jharkhand"] = "20",
        ["Odisha"] = "21",
        ["Orissa"] = "21",
        ["Chhattisgarh"] = "22",
        ["Madhya Pradesh"] = "23",
        ["Gujarat"] = "24",
        ["Dadra and Nagar Haveli and Daman and Diu"] = "26",
        ["Daman and Diu"] = "26",
        ["Dadra and Nagar Haveli"] = "26",
        ["Maharashtra"] = "27",
        ["Karnataka"] = "29",
        ["Goa"] = "30",
        ["Lakshadweep"] = "31",
        ["Kerala"] = "32",
        ["Tamil Nadu"] = "33",
        ["Puducherry"] = "34",
        ["Pondicherry"] = "34",
        ["Andaman and Nicobar Islands"] = "35",
        ["Andaman & Nicobar Islands"] = "35",
        ["Telangana"] = "36",
        ["Andhra Pradesh"] = "37",
        ["Ladakh"] = "38",
        ["Other Territory"] = "97",
    };

    /// <summary>
    /// Resolves any Indian state representation (ISO code like "TN", full name like "Tamil Nadu",
    /// or numeric code like "33" or "9") to its official 2-digit GST state code.
    /// Returns null if not recognised as an Indian state code or name.
    /// </summary>
    public static string? ToGstCode(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName))
        {
            return null;
        }

        string trimmed = codeOrName.Trim();

        if (trimmed.Length == 2 && char.IsAsciiDigit(trimmed[0]) && char.IsAsciiDigit(trimmed[1]))
        {
            return trimmed;
        }

        if (trimmed.Length == 1 && char.IsAsciiDigit(trimmed[0]))
        {
            return $"0{trimmed}";
        }

        if (IsoToGst.TryGetValue(trimmed, out string? gstFromIso))
        {
            return gstFromIso;
        }

        if (NameToGst.TryGetValue(trimmed, out string? gstFromName))
        {
            return gstFromName;
        }

        return null;
    }
}
