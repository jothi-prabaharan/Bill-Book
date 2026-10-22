using System.Globalization;

namespace Shared.Kernel.Licensing;

/// <summary>
/// A document would take a branch past what its plan allows a month (D-69,
/// TK-210). Thrown by <see cref="DocumentQuotaInterceptor"/> from inside
/// <c>SaveChanges</c>, before the document is written, and answered as a 409
/// <see cref="PlanLimitResponse"/> by the exception handlers.
///
/// An exception rather than a result because the refusal has to come from one
/// place that sees every document insert. A result would need every create path,
/// and every path somebody writes later, to remember to return it. The request's
/// transaction rolls back on any exception, and the refusal is an expected
/// outcome, so it is not written to the error log.
/// </summary>
public sealed class PlanLimitReachedException : Exception
{
    public PlanLimitReachedException(PlanLimitResponse limit)
        : base(limit?.Message)
    {
        Limit = limit!;
    }

    public PlanLimitResponse Limit { get; }

    /// <summary>The refusal in <paramref name="exception"/> or anything it wraps, or null when it holds none.</summary>
    public static PlanLimitReachedException? Find(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PlanLimitReachedException refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence a refused document carries. A trial is told to go to Elite
    /// as well as a Professional branch, because a trial has the same limits and
    /// only Elite lifts them.
    /// </summary>
    public static string MessageFor(string plan, PlanLimitKind kind, int limit)
    {
        string documents = kind == PlanLimitKind.PurchaseTransactions ? "Purchase" : "Sales";
        string figure = limit.ToString("N0", CultureInfo.InvariantCulture);

        return $"{plan} plan {documents} transaction limit reached (maximum {figure} {documents} transactions this month). "
            + "Upgrade to the Elite Plan for unlimited transactions.";
    }
}
