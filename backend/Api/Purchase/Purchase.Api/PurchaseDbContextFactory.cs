using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Purchase.Repository;
using Shared.Kernel.Tenancy;

namespace Purchase.Api;

/// <summary>
/// Builds a <see cref="PurchaseDbContext"/> for <c>dotnet ef</c> and for nothing
/// else. Never used at run time.
///
/// <b>Why a factory here when Sales gets its design-time context from
/// <c>Program.cs</c>.</b> Sales has a host: controllers, tenancy, a tenant
/// connection resolver, HTTP clients to Master and Accounting. Purchase has a
/// placeholder that returns "not implemented", and its own <c>Program.cs</c>
/// says to replace it wholesale rather than extend it when the service is built.
/// Wiring a full host now — purely so a migration can be generated — would be
/// exactly the "stub of the real shape" that comment warns against, and would
/// commit the service to a shape nobody has decided on yet.
///
/// So the schema arrives without a host, and T4.3 brings the host when it brings
/// the first endpoint. At that point this file goes and Purchase resolves its
/// context from DI like every implemented service.
///
/// The connection string is read from <c>PURCHASE_DESIGNTIME_DB</c>, falling back
/// to a local default. It is only ever used to generate SQL: <c>migrations add</c>
/// never opens it.
///
/// It lives in <c>Purchase.Api</c> rather than beside the context in
/// <c>Purchase.Repository</c> for a build reason, not a design one:
/// <c>Microsoft.EntityFrameworkCore.Design</c> is what supplies
/// <see cref="IDesignTimeDbContextFactory{TContext}"/>, and adding it to the
/// class library drags in <c>System.Security.Cryptography.Xml</c> 9.0.0, which
/// carries eight advisories and fails a build that treats warnings as errors.
/// The web SDK resolves that dependency from the shared framework instead, so
/// the API project already carries the package cleanly.
/// </summary>
public class PurchaseDbContextFactory : IDesignTimeDbContextFactory<PurchaseDbContext>
{
    public PurchaseDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("PURCHASE_DESIGNTIME_DB")
            ?? "Host=localhost;Database=billbook_designtime;Username=postgres;Password=postgres";

        DbContextOptions<PurchaseDbContext> options =
            new DbContextOptionsBuilder<PurchaseDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        // No tenant at design time. The query filter compares OrgId against
        // Guid.Empty, which is what TenantDbContext already does for an
        // unauthenticated context, and the model it builds is the same either
        // way — the filter is part of the model, not of the connection.
        return new PurchaseDbContext(options, new TenantContext());
    }
}
