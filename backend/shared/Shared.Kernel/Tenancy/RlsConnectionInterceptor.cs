using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Sets <c>app.current_customer_id</c> and <c>app.current_org_id</c> for
/// Postgres row-level security, <b>transaction-locally</b>, on every command.
///
/// <b>How.</b> Each command's text is prefixed with two <c>SET LOCAL</c>
/// statements carrying the tenant as it stands when the command runs. Npgsql
/// sends the prefix and the command as one batch, and Postgres runs a batch as
/// one transaction — the explicit one when the command is inside it, an implicit
/// one of its own when it is not — so the setting holds for exactly that
/// transaction and is gone at its end. <c>SET</c> returns no result set and no
/// row count, so readers, row counts and <c>RETURNING</c> are untouched.
///
/// <b>Why not once per connection</b> (the previous version, and what the
/// Tenancy rule in <c>CLAUDE.md</c> forbids): a session setting outlives the
/// request that made it, and a pooled connection carries it to the next one. It
/// was mitigated by overwriting on every open, which still left the setting
/// fixed at the moment the connection opened.
///
/// <b>Why not once per transaction, at <c>BEGIN</c>.</b> Decided transaction-local
/// on 24 September 2026 (TK-08), and this is the form that covers every path:
/// <list type="bullet">
/// <item>Reads outside an explicit transaction get a transaction of their own
/// for free, rather than each read path having to open one — workers, seeders,
/// startup, GET requests, an error log written after a rollback.</item>
/// <item>The tenant as it is <i>now</i> is what applies. An internal route
/// takes the branch from its request after the reliability filter has already
/// opened the transaction; a setting taken at <c>BEGIN</c> would hold no branch,
/// and every internal write would be refused.</item>
/// <item>A <c>ROLLBACK TO SAVEPOINT</c>, which undoes a <c>SET LOCAL</c> made
/// after the savepoint, cannot leave later commands without a tenant: the next
/// command sets it again.</item>
/// </list>
///
/// <b>With no tenant at all (null or <see cref="Guid.Empty"/>), commands go out
/// untouched.</b> Nothing is set, so
/// the policy's <c>NULLIF</c> sees no tenant and every tenant row is hidden —
/// which is what startup, migrations and anonymous requests should see. It also
/// keeps the prefix off statements that may not run in a transaction block.
///
/// The values are inlined because <c>SET</c> takes no parameters. They are
/// <see cref="Guid"/>s, formatted here, so the text is hex digits and hyphens
/// and nothing a caller can influence.
/// </summary>
public sealed class RlsConnectionInterceptor : DbCommandInterceptor
{
    private readonly ITenantContext _tenant;

    public RlsConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

    /// <summary>
    /// The prefix for a tenant, or null when there is none. Public so the text
    /// the database receives can be asserted without one.
    /// </summary>
    public static string? PrefixFor(Guid? customerId, Guid? orgId)
    {
        // Guid.Empty means "none" here as it does in TenantDbContext, whose
        // filter compares against it: Master's bootstrap migrates under an
        // all-zero tenant, and that is no tenant.
        string customer = customerId is Guid c && c != Guid.Empty ? c.ToString() : "";
        string org = orgId is Guid o && o != Guid.Empty ? o.ToString() : "";

        if (customer.Length == 0 && org.Length == 0)
        {
            return null;
        }

        return $"SET LOCAL app.current_customer_id = '{customer}'; "
            + $"SET LOCAL app.current_org_id = '{org}';\n";
    }

    private void Apply(DbCommand command)
    {
        string? prefix = PrefixFor(_tenant.CustomerId, _tenant.OrgId);

        if (prefix is not null)
        {
            command.CommandText = prefix + command.CommandText;
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Apply(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Apply(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Apply(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Apply(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Apply(command);
        return ValueTask.FromResult(result);
    }
}
