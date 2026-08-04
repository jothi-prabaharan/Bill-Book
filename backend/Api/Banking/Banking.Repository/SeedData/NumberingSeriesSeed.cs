using Shared.Kernel.Numbering;

namespace Banking.Repository.SeedData;

/// <summary>
/// Banking's own document series, written when an organization is created.
///
/// Each service seeds the series for the transaction types it owns — Accounting
/// seeds <c>JRN</c>, Sales and Purchase will seed theirs. The table itself lives
/// in <c>Shared.Kernel</c> and is migrated by Accounting; Banking maps it with
/// <c>ExcludeFromMigrations</c> so a number can still be allocated inside
/// Banking's own transaction.
///
/// <b>No manual override, and a yearly reset.</b> These are document series: they
/// have to run consecutively within the financial year, so a hand-typed number
/// is refused by the database rather than merely discouraged.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Document(orgId, 200, "SPM", "Payment Number", "PAY"),
        Document(orgId, 210, "RCM", "Receipt Number", "REC"),
        Document(orgId, 220, "TRM", "Transfer Number", "TRF"),
    ];

    private static NumberingSeries Document(
        Guid orgId, int displayOrder, string code, string name, string prefix) =>
        new()
        {
            OrgId = orgId,
            SeriesSystemName = code,
            SeriesCode = code,
            SeriesName = name,
            SeriesFor = SeriesFor.Document,
            Prefix = prefix,
            Separator = "/",
            IncludeFinancialYear = true,
            FinancialYearFormat = FinancialYearFormat.Compact,
            NumberLength = 5,
            StartNumber = 1,
            NextNumber = 1,
            ResetFrequency = NumberResetFrequency.Yearly,
            AllowManualOverride = false,
            IsDefault = true,
            IsSystem = true,
            IsActive = true,
            DisplayOrder = displayOrder,
        };
}
