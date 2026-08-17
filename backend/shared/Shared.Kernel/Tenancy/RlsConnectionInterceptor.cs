using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Sets app.current_org_id for Postgres row-level security.
///
/// Uses set_config(..., false) to set the config at the session level.
/// To prevent connection pool leaks, it ALWAYS overwrites the value when the connection
/// is taken from the pool and opened. If the current request has no OrgId, it is explicitly cleared.
///
/// This avoids the "cannot be used outside a transaction block" error that occurs when
/// using set_config(..., true) outside an explicit EF Core transaction.
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
        await using DbCommand command = connection.CreateCommand();
        // Use is_local = false (session level). To prevent connection pool leaks,
        // we ALWAYS overwrite the value. If there is no OrgId, we clear it.
        // This avoids Postgres errors when calling set_config(..., true) outside a transaction.
        command.CommandText = "SELECT set_config('app.current_org_id', $1, false)";

        DbParameter parameter = command.CreateParameter();
        parameter.Value = _tenant.OrgId?.ToString() ?? "";
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
