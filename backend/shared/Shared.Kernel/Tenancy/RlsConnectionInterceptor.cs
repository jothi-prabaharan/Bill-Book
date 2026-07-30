using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Sets app.current_org_id for Postgres row-level security.
///
/// Deliberately uses set_config(..., true) — the third argument makes it
/// transaction-local. Setting it at connection level would leak org context to
/// the next request that borrows the pooled connection, which is the specific
/// mistake this class exists to prevent.
///
/// This is one of the few places raw SQL is allowed: set_config has no LINQ
/// equivalent.
/// </summary>
public sealed class RlsConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;

    public RlsConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_tenant.OrgId is Guid orgId)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT set_config('app.current_org_id', $1, true)";

            DbParameter parameter = command.CreateParameter();
            parameter.Value = orgId.ToString();
            command.Parameters.Add(parameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
