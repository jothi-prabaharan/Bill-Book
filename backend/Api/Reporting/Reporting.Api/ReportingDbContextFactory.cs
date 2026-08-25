using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Reporting.Repository;
using Shared.Kernel.Tenancy;

namespace Reporting.Api;

/// <summary>
/// Builds a <see cref="ReportingDbContext"/> for <c>dotnet ef</c> and nothing
/// else. Never registered in DI, never reached at run time.
///
/// <b>Why this exists when no other service has one.</b> Every other service
/// generates its migrations through its API host, which registers the context in
/// DI against the fixed <c>TenantDatabase</c> connection string. This
/// host is still the scaffold that answers "not implemented" — it is replaced in
/// R0.6, which depends on the engine, which depends on the contract, all of them
/// after this schema. Without this factory the schema could not be migrated
/// until most of R0 was done, and the schema is what the rest is built on.
///
/// It lives in the API project rather than beside the context because
/// <c>IDesignTimeDbContextFactory</c> comes from
/// <c>Microsoft.EntityFrameworkCore.Design</c>, and in a plain library that
/// package resolves a <c>System.Security.Cryptography.Xml</c> with live
/// advisories — which <c>TreatWarningsAsErrors</c> turns into a failed build. The
/// web SDK's framework reference supplies a patched one.
///
/// <b>Delete this when R0.6 lands</b> if the host's registration makes it
/// redundant; keeping both is harmless, keeping neither is not.
/// </summary>
public class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ReportingDbContext> options = new();

        // Same schema-scoped history table Program.cs configures for real runs
        // — matched here so `dotnet ef`, which prefers this factory over the
        // host, does not leave rpt's migration history in the public schema
        // while every other schema in the shared tenant database gets its own.
        options.UseNpgsql(
            "Host=localhost;Database=design_time_only;Username=postgres",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "rpt"));

        return new ReportingDbContext(options.Options, new DesignTimeTenantContext());
    }

    /// <summary>
    /// The query filter needs a tenant to read an org from. At design time there
    /// is no request and no organization, and an unresolved tenant yields the
    /// empty guid anyway — what goes into a migration is the filter's shape, never
    /// its value.
    /// </summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public IReadOnlySet<string> Permissions => new HashSet<string>();

        public (Guid CustomerId, Guid OrgId) Require() =>
            throw new InvalidOperationException(
                "The design-time tenant context has no organization. This factory exists " +
                "for 'dotnet ef' only and must never be reached at run time.");
    }
}
