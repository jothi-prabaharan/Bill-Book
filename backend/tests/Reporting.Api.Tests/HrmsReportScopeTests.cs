using System.Runtime.CompilerServices;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// HRMS and Payroll reports follow their add-on (TK-136): with the App enum
/// gone, a report names the add-on it needs, and a branch without it is not
/// offered the report.
/// </summary>
public sealed class HrmsReportScopeTests
{
    /// <summary>
    /// Every HRMS or Payroll report source, found by the permission it asks
    /// for. Their scope properties are constants, so no constructor runs.
    /// </summary>
    public static TheoryData<string> HrmsSources()
    {
        var data = new TheoryData<string>();
        foreach (Type type in HrmsSourceTypes())
        {
            data.Add(type.FullName!);
        }

        return data;
    }

    private static readonly string[] HrmsPermissions =
        ["payroll.", "employee.", "leave.", "attendance.", "claims.", "recruitment.", "performance."];

    private static IEnumerable<Type> HrmsSourceTypes() =>
        typeof(IReportSourceRepository).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && typeof(IReportSourceRepository).IsAssignableFrom(t)
                && PermissionOf(t) is string permission
                && HrmsPermissions.Any(p => permission.StartsWith(p, StringComparison.Ordinal)));

    /// <summary>A source whose permission is not a constant is not an HRMS one, and reads as none.</summary>
    private static string? PermissionOf(Type type)
    {
        try
        {
            return ((IReportSourceRepository)RuntimeHelpers.GetUninitializedObject(type)).RequiredPermission;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReportSourceRepository Create(string typeName) =>
        (IReportSourceRepository)RuntimeHelpers.GetUninitializedObject(
            typeof(IReportSourceRepository).Assembly.GetType(typeName)!);

    [Fact]
    public void There_are_hrms_reports_to_check() => Assert.NotEmpty(HrmsSourceTypes());

    [Theory]
    [MemberData(nameof(HrmsSources))]
    public void Each_hrms_report_needs_hrms_or_payroll(string typeName)
    {
        AddOn needed = Create(typeName).RequiredAddOn;

        Assert.NotEqual(AddOn.None, needed);
        Assert.Equal(AddOn.None, needed & ~(AddOn.Hrms | AddOn.Payroll));
    }

    [Theory]
    [MemberData(nameof(HrmsSources))]
    public void A_branch_without_the_add_on_is_not_offered_it(string typeName)
    {
        IReportSourceRepository source = Create(typeName);

        Assert.False(new ReportAudience(AddOn.None, BusinessType.Retail, null).Allows(source));
        Assert.True(new ReportAudience(source.RequiredAddOn, BusinessType.Retail, null).Allows(source));
        Assert.True(new ReportAudience(source.RequiredAddOn, null, null).Allows(source));
    }
}
