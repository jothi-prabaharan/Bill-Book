using System.Linq.Expressions;
using System.Text.Json;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Shared.Kernel.CustomFields;

namespace Reporting.Api.Services;

/// <summary>
/// A dataset whose columns are only known once the branch is (TK-113): its
/// custom fields. The catalog prepares them before it reads
/// <see cref="IReportSource.Columns"/>, since a source cannot await in a property.
/// </summary>
public interface IDatasetSource
{
    /// <summary>Loads the branch's custom fields. Once per request; later calls do nothing.</summary>
    Task PrepareColumnsAsync(CancellationToken ct);

    /// <summary>
    /// Heading per custom column key, for <b>every</b> field of the kind,
    /// deactivated ones included, so a notice can name a column it dropped.
    /// </summary>
    IReadOnlyDictionary<string, string> CustomLabels { get; }
}

/// <summary>
/// The base of the report builder's datasets (TK-113): a fixed set of columns
/// the catalog seeds, plus one text column per <b>active</b> custom field of
/// <see cref="Kind"/>, keyed <c>custom.{field}</c> and never seeded — they
/// change whenever an administrator adds or deactivates a field.
///
/// A deactivated field's column is not offered. A saved report that named it
/// loses it with a notice rather than failing: the values are still stored,
/// and reactivating the field brings the column back.
///
/// When Master cannot be asked the dataset still runs, with only its fixed
/// columns: a report missing its custom columns for five minutes is better
/// than a report that will not open.
/// </summary>
public abstract class DatasetSource<TRow> : ReportSource<TRow>, IDatasetSource
    where TRow : class
{
    private readonly ICustomFieldDefinitions? _definitions;
    private List<ReportColumn> _custom = [];
    private Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    private bool _prepared;

    protected DatasetSource(ICustomFieldDefinitions? definitions)
    {
        _definitions = definitions;
    }

    public override ReportModule Module => ReportModule.Custom;

    public sealed override bool IsDataset => true;

    /// <summary>The kind whose custom fields become columns, or null for a dataset with none.</summary>
    protected abstract CustomFieldEntityKind? Kind { get; }

    /// <summary>Where a row carries the record's custom field values. Required when <see cref="Kind"/> is set.</summary>
    protected virtual Expression<Func<TRow, JsonDocument?>>? CustomFieldsOf => null;

    /// <summary>The columns every branch has, each with a seeded <c>rpt.ReportDetails</c> row.</summary>
    protected abstract IReadOnlyList<ReportColumn> BaseColumns { get; }

    public sealed override IReadOnlyList<ReportColumn> Columns => [.. BaseColumns, .. _custom];

    public IReadOnlyDictionary<string, string> CustomLabels => _labels;

    public async Task PrepareColumnsAsync(CancellationToken ct)
    {
        if (_prepared)
        {
            return;
        }

        _prepared = true;

        if (Kind is not CustomFieldEntityKind kind || CustomFieldsOf is not { } document || _definitions is null)
        {
            return;
        }

        IReadOnlyList<CustomFieldDefinitionDto>? fields = await _definitions.GetAsync(kind, ct);
        (_custom, _labels) = Build(fields, document);
    }

    /// <summary>The custom columns and every field's heading. Pure, so the rules can be tested without Master.</summary>
    public static (List<ReportColumn> Columns, Dictionary<string, string> Labels) Build(
        IReadOnlyList<CustomFieldDefinitionDto>? fields, Expression<Func<TRow, JsonDocument?>> document)
    {
        Dictionary<string, string> labels = new(StringComparer.Ordinal);
        List<ReportColumn> columns = [];

        foreach (CustomFieldDefinitionDto field in (fields ?? []).OrderBy(f => f.DisplayOrder).ThenBy(f => f.Label))
        {
            labels[ReportColumn.CustomKeyPrefix + field.Key] = field.Label;

            if (field.IsActive)
            {
                columns.Add(ReportColumn.CustomField(field.Key, document));
            }
        }

        return (columns, labels);
    }

    protected override Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        PrepareColumnsAsync(ct);
}
