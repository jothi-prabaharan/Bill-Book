using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;

namespace Master.Repository.SeedData;

/// <summary>
/// Imports the full HSN/SAC list from a CSV.
///
/// The detailed codes are not hard-coded in the seed on purpose: there are
/// roughly thirteen thousand of them, they change with each CBIC notification,
/// and a wrong code on a return is a filing error. So they are loaded from the
/// authoritative file — export the master list from the GST portal
/// (Services → User Services → Search HSN Code) or the CBIC tariff, and import it.
///
/// Expected columns, header row required:
///     Code,CodeType,Description,DefaultGstRate
///     84713010,HSN,"Portable digital automatic data processing machines",18
///     998313,SAC,"Information technology consulting services",18
///
/// Import is idempotent: an existing code is updated, a new one inserted.
/// Nothing is deleted, because items already reference these rows.
/// </summary>
public static class HsnSacCsvLoader
{
    public static async Task<HsnImportResult> ImportAsync(
        AdminDbContext db, Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv);
        string? header = await reader.ReadLineAsync(ct);
        if (header is null)
        {
            return new HsnImportResult(0, 0, ["The file is empty."]);
        }

        Dictionary<string, HsnSacCode> existing = await db.HsnSacCodes
            .ToDictionaryAsync(c => c.Code, ct);

        int nextId = existing.Count == 0 ? 1 : existing.Values.Max(c => c.HsnSacCodeId) + 1;
        int inserted = 0;
        int updated = 0;
        var errors = new List<string>();
        int lineNumber = 1;

        while (await reader.ReadLineAsync(ct) is string line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = SplitCsvLine(line);
            if (fields.Length < 3)
            {
                errors.Add($"Line {lineNumber}: expected at least 3 columns.");
                continue;
            }

            string code = fields[0].Trim();
            if (code.Length is < 2 or > 8 || !code.All(char.IsDigit))
            {
                errors.Add($"Line {lineNumber}: '{code}' is not a 2 to 8 digit code.");
                continue;
            }

            HsnSacCodeType type = fields[1].Trim().Equals("SAC", StringComparison.OrdinalIgnoreCase)
                ? HsnSacCodeType.Sac
                : HsnSacCodeType.Hsn;

            string description = fields[2].Trim();
            decimal? rate = fields.Length > 3 && decimal.TryParse(fields[3].Trim(), out decimal parsed)
                ? parsed
                : null;

            if (existing.TryGetValue(code, out HsnSacCode? row))
            {
                row.Description = description;
                row.CodeType = type;
                row.DefaultGstRate = rate;
                row.DigitLength = (byte)code.Length;
                row.ChapterCode = code[..2];
                updated++;
            }
            else
            {
                var created = new HsnSacCode
                {
                    HsnSacCodeId = nextId++,
                    Code = code,
                    CodeType = type,
                    Description = description,
                    ChapterCode = code[..2],
                    DigitLength = (byte)code.Length,
                    DefaultGstRate = rate,
                    IsActive = true,
                };
                db.HsnSacCodes.Add(created);
                existing[code] = created;
                inserted++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new HsnImportResult(inserted, updated, errors);
    }

    /// <summary>Minimal CSV split honouring double-quoted fields — descriptions contain commas.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // A doubled quote inside a quoted field is a literal quote.
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}

public sealed record HsnImportResult(int Inserted, int Updated, IReadOnlyList<string> Errors);
