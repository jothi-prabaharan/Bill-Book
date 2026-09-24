using Hrm.Entity.TableEntities;
using Shared.Kernel.Numbering;

namespace Hrm.Repository.SeedData;

/// <summary>
/// What a new branch starts with in <c>hrm</c> (H1, TK-48): one department,
/// designation, grade and work location, so an employee can be entered the day
/// the branch opens, and the <c>EMP</c> series every employee code comes from.
/// </summary>
public static class HrmSeed
{
    public const string EmployeeSeriesCode = "EMP";

    public static Department Department(Guid orgId) => new() { OrgId = orgId, Code = "GEN", Name = "General" };

    public static Designation Designation(Guid orgId) => new() { OrgId = orgId, Code = "STAFF", Name = "Staff" };

    public static Grade Grade(Guid orgId) => new() { OrgId = orgId, Code = "G1", Name = "Grade 1", SortOrder = 1, NoticePeriodDays = 30 };

    public static WorkLocation WorkLocation(Guid orgId) => new() { OrgId = orgId, Code = "HO", Name = "Head office" };

    /// <summary>The relations an approval level can name, to start with (TK-49).</summary>
    public static IReadOnlyList<RelationshipType> RelationshipTypes(Guid orgId) =>
    [
        new() { OrgId = orgId, Code = "LEAD", Name = "Lead" },
        new() { OrgId = orgId, Code = "PROJLEAD", Name = "Project Lead" },
    ];

    /// <summary>A master series, like item and contact codes: EMP-00001, never reset.</summary>
    public static NumberingSeries EmployeeSeries(Guid orgId) => new()
    {
        OrgId = orgId,
        SeriesSystemName = EmployeeSeriesCode,
        SeriesCode = EmployeeSeriesCode,
        SeriesName = "Employee Code",
        SeriesFor = SeriesFor.Master,
        Prefix = EmployeeSeriesCode,
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
        DisplayOrder = 900,
    };
}
