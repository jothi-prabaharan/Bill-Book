using Shared.Kernel.Numbering;
using Student.Entity.TableEntities;

namespace Student.Repository.SeedData;

/// <summary>What a new School branch starts with (S1, TK-61): LKG to XII, and the ADM series.</summary>
public static class StudentSeed
{
    public const string AdmissionSeriesCode = "ADM";

    /// <summary>LKG to XII. The owner deactivates the classes they do not teach.</summary>
    public static IReadOnlyList<SchoolClass> Classes(Guid orgId)
    {
        string[] codes = ["LKG", "UKG", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];
        string[] names =
        [
            "Lower kindergarten", "Upper kindergarten", "Class I", "Class II", "Class III", "Class IV", "Class V",
            "Class VI", "Class VII", "Class VIII", "Class IX", "Class X", "Class XI", "Class XII",
        ];

        return [.. codes.Select((code, i) => new SchoolClass { OrgId = orgId, Code = code, Name = names[i], SortOrder = i + 1 })];
    }

    /// <summary>A master series like the employee code: ADM-00001, never reset, never reused.</summary>
    public static NumberingSeries AdmissionSeries(Guid orgId) => new()
    {
        OrgId = orgId,
        SeriesSystemName = AdmissionSeriesCode,
        SeriesCode = AdmissionSeriesCode,
        SeriesName = "Admission Number",
        SeriesFor = SeriesFor.Master,
        Prefix = AdmissionSeriesCode,
        Separator = "-",
        IncludeFinancialYear = false,
        FinancialYearFormat = FinancialYearFormat.Compact,
        NumberLength = 5,
        StartNumber = 1,
        NextNumber = 1,
        ResetFrequency = NumberResetFrequency.Never,
        AllowManualOverride = false,
        IsDefault = true,
        IsSystem = true,
        IsActive = true,
        DisplayOrder = 910,
    };
}
