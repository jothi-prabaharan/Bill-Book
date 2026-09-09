using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Persistence;

/// <summary>
/// Wraps every write request in a transaction, and rolls it back unless the
/// action succeeded.
///
/// The alternative — a transaction in each service method that needs one — is
/// what the product had, and it left two kinds of hole. Operations that write
/// twice had no transaction at all, so a bank statement import could commit its
/// header and fail on its lines, and a new branch could be created and then
/// fail to seed. And operations that <i>did</i> open one could not be composed,
/// because the second <c>BeginTransactionAsync</c> throws.
///
/// What counts as success is the status the action produced, not merely the
/// absence of an exception. A controller that returns <c>Conflict()</c> after
/// its service wrote two of three rows must not keep those two — so anything
/// from 400 up rolls back, as does <c>Forbid()</c>, a cancelled action, and any
/// exception.
///
/// The costs, stated rather than discovered:
/// <list type="bullet">
/// <item>A write action holds a database transaction across any HTTP call it
/// makes. The document posting paths call Inventory and Accounting inside it.
/// At Read Committed that holds row locks on what this service has written,
/// which is the point, for as long as the remote call takes.</item>
/// <item>A service with two DbContexts opens two transactions per write. They
/// are two databases and are settled independently — there is no two-phase
/// commit here, so an operation that writes to both can still half-succeed.</item>
/// </list>
/// </summary>
public sealed class TransactionFilter : IAsyncActionFilter
{
    private readonly ILogger<TransactionFilter> _logger;

    public TransactionFilter(ILogger<TransactionFilter> logger) => _logger = logger;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!ShouldWrap(context))
        {
            await next();
            return;
        }

        IsolationLevel isolation = IsolationFor(context);

        var units = context.HttpContext.RequestServices
            .GetService(typeof(IEnumerable<IUnitOfWork>)) as IEnumerable<IUnitOfWork>
            ?? [];

        List<IUnitOfWork> started = [];
        CancellationToken ct = context.HttpContext.RequestAborted;

        try
        {
            foreach (IUnitOfWork unit in units)
            {
                await unit.BeginAsync(isolation, ct);
                started.Add(unit);
            }

            if (started.Count == 0)
            {
                await next();
                return;
            }

            ActionExecutedContext executed = await next();

            if (Succeeded(executed))
            {
                foreach (IUnitOfWork unit in started)
                {
                    await unit.CommitAsync(ct);
                }
            }
            else
            {
                await RollbackAsync(started, ct);

                if (executed.Exception is not null && !executed.ExceptionHandled)
                {
                    // Left for GlobalExceptionHandler to translate and record.
                    // Rolling back first is what lets the error log be written
                    // on a clean connection rather than inside a doomed
                    // transaction.
                    _logger.LogDebug(
                        "Rolled back {Count} unit(s) of work after an exception in {Action}.",
                        started.Count,
                        context.ActionDescriptor.DisplayName);
                }
            }
        }
        catch
        {
            await RollbackAsync(started, CancellationToken.None);
            throw;
        }
        finally
        {
            foreach (IUnitOfWork unit in started)
            {
                await unit.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Write methods only, and only where the action has not opted out. A GET
    /// takes no transaction: it would cost a connection and a snapshot to
    /// protect nothing.
    /// </summary>
    private static bool ShouldWrap(ActionExecutingContext context)
    {
        string method = context.HttpContext.Request.Method;

        if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method))
        {
            return false;
        }

        return !context.ActionDescriptor.EndpointMetadata.OfType<NoTransactionAttribute>().Any();
    }

    private static IsolationLevel IsolationFor(ActionExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<TransactionalAttribute>().LastOrDefault()
            is TransactionalAttribute attribute
            ? attribute.IsolationLevel
            : IsolationLevel.Unspecified;

    /// <summary>
    /// Commit only on an outcome the caller will read as success.
    ///
    /// <c>ForbidResult</c> and <c>ChallengeResult</c> are checked by type
    /// because they carry no status until they execute, which is after this
    /// filter has had to decide.
    /// </summary>
    private static bool Succeeded(ActionExecutedContext executed)
    {
        if (executed.Exception is not null || executed.Canceled)
        {
            return false;
        }

        return executed.Result switch
        {
            ForbidResult or ChallengeResult or UnauthorizedResult => false,
            IStatusCodeActionResult { StatusCode: int status } => status < 400,
            _ => true,
        };
    }

    private async Task RollbackAsync(List<IUnitOfWork> units, CancellationToken ct)
    {
        foreach (IUnitOfWork unit in units)
        {
            try
            {
                await unit.RollbackAsync(ct);
            }
            catch (Exception failure)
            {
                // A rollback that fails is nearly always a connection that has
                // already gone, in which case the server has rolled the
                // transaction back for us. Never let it mask the original
                // exception.
                _logger.LogWarning(
                    failure,
                    "Rolling back {UnitOfWork} failed. The transaction is abandoned.",
                    unit.Name);
            }
        }
    }
}
