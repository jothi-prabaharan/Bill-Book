using Master.Entity.Enums;

namespace Master.Repository.Repositories;

/// <summary>
/// What a customer's kind changes (D-44, TK-152). Pure.
///
/// A <see cref="CustomerKind.Standard"/> customer is the paying one: a trial,
/// then a licence that can lapse. An <see cref="CustomerKind.Internal"/>
/// customer is our own organisation and a <see cref="CustomerKind.Demo"/> one
/// exists for testing and showing the product; neither is billed, so neither
/// ever lapses, and neither is held to the branch cap a licence sets.
/// </summary>
public static class CustomerKindRepository
{
    /// <summary>
    /// The expiry stored on a licence that never lapses. Far enough out that
    /// nobody meets it, and far enough short of <see cref="DateOnly.MaxValue"/>
    /// that adding grace days to it cannot overflow.
    /// </summary>
    public static readonly DateOnly NoExpiry = new(9999, 1, 1);

    /// <summary>
    /// The user and branch limits on an unbilled customer's licence. They are
    /// what a paying customer buys, so an unbilled one is not held to them;
    /// a number rather than "no limit" keeps the columns and the checks as
    /// they are.
    /// </summary>
    public const int UnbilledMaxUsers = 10_000;

    /// <inheritdoc cref="UnbilledMaxUsers"/>
    public const int UnbilledMaxOrganizations = 1_000;

    /// <summary>Whether a customer of this kind can lapse, be stamped Expired, or be reminded to pay.</summary>
    public static bool NeverLapses(CustomerKind kind) => kind is CustomerKind.Internal or CustomerKind.Demo;

    /// <summary>
    /// The kind a request may create. Public signup is always Standard,
    /// whatever it asks for; only a platform operator may make the other two.
    /// </summary>
    public static CustomerKind KindToCreate(CustomerKind? requested, bool byOperator) =>
        byOperator && requested is CustomerKind kind && Enum.IsDefined(kind) ? kind : CustomerKind.Standard;

    /// <summary>The licence a new customer of this kind starts on: a 14-day trial, or Elite with no expiry.</summary>
    public static (LicenseType Type, DateOnly Expiry) FirstLicence(CustomerKind kind, DateOnly today, int trialDays) =>
        NeverLapses(kind) ? (LicenseType.Elite, NoExpiry) : (LicenseType.Trial, today.AddDays(trialDays));

    /// <summary>The plan tier a new customer of this kind starts on.</summary>
    public static Shared.Kernel.Tenancy.PlanTier FirstPlanTier(CustomerKind kind) =>
        NeverLapses(kind) ? Shared.Kernel.Tenancy.PlanTier.Elite : Shared.Kernel.Tenancy.PlanTier.Trial;
}
