using System.Globalization;
using Reporting.Entity.Enums;

namespace Reporting.Api.Services;

/// <summary>
/// A report parameter — a date range, an as-at date, a warehouse.
///
/// <b>Distinct from a filter, and the difference is not cosmetic.</b> A filter
/// decides which rows survive; a parameter decides what the report means. Trial
/// Balance filtered to April is a trial balance of everything with the non-April
/// rows hidden; Trial Balance <i>for</i> April is a different report. Getting this
/// backwards produces numbers that are individually correct and collectively wrong.
/// </summary>
public sealed class ReportParameter
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    public required ColumnDataType DataType { get; init; }

    public bool IsRequired { get; init; }

    public string? DefaultValue { get; init; }
}

/// <summary>
/// The parameter values a request supplied, parsed on the way out rather than on
/// the way in — a report asks for what it needs and says what type it wants.
/// </summary>
public sealed class ReportParameters
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public ReportParameters(IReadOnlyDictionary<string, string?> values) => _values = values;

    public static ReportParameters Empty { get; } =
        new(new Dictionary<string, string?>());

    public DateOnly? Date(string name) =>
        _values.TryGetValue(name, out string? raw)
        && DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out DateOnly value)
            ? value
            : null;

    public long? Id(string name) =>
        _values.TryGetValue(name, out string? raw)
        && long.TryParse(raw, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;

    public bool Flag(string name, bool fallback = false) =>
        _values.TryGetValue(name, out string? raw) && bool.TryParse(raw, out bool value)
            ? value
            : fallback;

    public string? Text(string name) =>
        _values.TryGetValue(name, out string? raw) ? raw : null;
}
