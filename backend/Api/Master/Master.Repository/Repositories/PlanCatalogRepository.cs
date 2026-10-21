using Master.Entity.Enums;
using Master.Entity.TableEntities;

namespace Master.Repository.Repositories;

/// <summary>
/// What each plan allows (D-68, TK-209). Pure.
///
/// A customer buys one of two plans. Professional is stored as
/// <see cref="LicenseType.Pro"/>, and a <see cref="LicenseType.Standard"/>
/// licence, which is what Pro was called before, reads as the same plan. Elite
/// is <see cref="LicenseType.Elite"/>. A trial has Professional's transaction
/// limits and three users.
///
/// The limits are stored on the licence and read from there at each check, so
/// an operator can settle a custom deal without a release. This class is what a
/// licence is given when it is made or its plan changes, and what fills a limit
/// that was left empty: an empty limit means no limit, which only Elite has.
/// </summary>
public static class PlanCatalogRepository
{
    /// <summary>Users a branch may hold on a trial.</summary>
    public const int TrialMaxUsers = 3;

    /// <summary>Users a branch may hold on Professional, counting those who have been deactivated.</summary>
    public const int ProfessionalMaxUsers = 5;

    /// <summary>Sales documents, and separately Purchase documents, a branch may create in a calendar month on a trial or on Professional.</summary>
    public const int ProfessionalMonthlyTransactions = 1_000;

    /// <summary>The three limits of a plan; a null is no limit.</summary>
    public sealed record PlanLimits(int? MaxUsers, int? MonthlySalesTransactions, int? MonthlyPurchaseTransactions);

    /// <summary>The name a person reads: Trial, Professional or Elite.</summary>
    public static string Name(LicenseType type) => type switch
    {
        LicenseType.Trial => "Trial",
        LicenseType.Elite => "Elite",
        _ => "Professional",
    };

    /// <summary>What a licence of this type is given.</summary>
    public static PlanLimits DefaultsFor(LicenseType type) => type switch
    {
        LicenseType.Elite => new PlanLimits(null, null, null),
        LicenseType.Trial => new PlanLimits(TrialMaxUsers, ProfessionalMonthlyTransactions, ProfessionalMonthlyTransactions),
        _ => new PlanLimits(ProfessionalMaxUsers, ProfessionalMonthlyTransactions, ProfessionalMonthlyTransactions),
    };

    /// <summary>Sets the licence's three limits to those of its own type.</summary>
    public static void Apply(License licence)
    {
        PlanLimits limits = DefaultsFor(licence.LicenseType);
        licence.MaxUsers = limits.MaxUsers;
        licence.MonthlySalesTransactionLimit = limits.MonthlySalesTransactions;
        licence.MonthlyPurchaseTransactionLimit = limits.MonthlyPurchaseTransactions;
    }

    /// <summary>
    /// Whether moving from one plan to another is an upgrade a customer may ask
    /// for: to Professional from a trial, or to Elite from anything below it.
    /// </summary>
    public static bool IsUpgrade(LicenseType from, LicenseType to) =>
        to is LicenseType.Pro or LicenseType.Elite && Rank(to) > Rank(from);

    private static int Rank(LicenseType type) => type switch
    {
        LicenseType.Trial => 0,
        LicenseType.Standard or LicenseType.Pro => 1,
        _ => 2,
    };

    /// <summary>
    /// The sentence a refused invitation carries. A trial is pointed at either
    /// paid plan; Professional at Elite, which is the only plan above it.
    /// </summary>
    public static string UserLimitMessage(LicenseType type, int limit) => type == LicenseType.Trial
        ? $"Trial plan user limit reached (maximum {limit} users including inactive accounts). Upgrade to the Professional or Elite Plan to add more users."
        : $"{Name(type)} plan user limit reached (maximum {limit} users including inactive accounts). Upgrade to the Elite Plan to add more users.";
}
