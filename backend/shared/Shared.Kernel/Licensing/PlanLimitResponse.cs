using System.Text.Json.Serialization;

namespace Shared.Kernel.Licensing;

/// <summary>Which limit of a plan stopped a request (TK-209, TK-210).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanLimitKind
{
    /// <summary>The branch already holds as many users as its plan allows, active or not.</summary>
    Users = 1,

    /// <summary>The branch has created as many Sales documents this month as its plan allows.</summary>
    SalesTransactions = 2,

    /// <summary>The branch has created as many Purchase documents this month as its plan allows.</summary>
    PurchaseTransactions = 3,
}

/// <summary>
/// What every service answers, with a 409, when a rule of the customer's plan
/// stops a request (TK-209). It is a <c>{ message }</c> refusal like any other,
/// so a form that only shows the server's words keeps working, with a
/// <see cref="Code"/> a caller can branch on: the screen opens its upgrade
/// dialog on <see cref="ReachedCode"/> and nothing else.
///
/// Not an <c>ApiErrorCode</c>: those name database failures. This is a business
/// rule, and the figures belong in it — the user is told what the plan allows
/// and how much of it is used.
/// </summary>
public sealed class PlanLimitResponse
{
    /// <summary>The value of <see cref="Code"/>, for a caller that has to compare.</summary>
    public const string ReachedCode = "PlanLimitReached";

    public string Message { get; set; } = null!;

    public string Code { get; set; } = ReachedCode;

    public PlanLimitKind Kind { get; set; }

    /// <summary>The plan's display name: Trial, Professional or Elite.</summary>
    public string Plan { get; set; } = null!;

    /// <summary>What the plan allows.</summary>
    public int Limit { get; set; }

    /// <summary>What is already used.</summary>
    public int Used { get; set; }
}
