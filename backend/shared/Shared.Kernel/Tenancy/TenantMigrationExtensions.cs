using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Seed;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Extension methods for configuring and registering <see cref="TenantMigrationMiddleware{TContext}"/>.
/// </summary>
public static class TenantMigrationExtensions
{
    /// <summary>
    /// Registers the <see cref="IJsonSeedLoader"/> and services required for tenant auto-migrations.
    /// </summary>
    public static IServiceCollection AddTenantMigration(
        this IServiceCollection services,
        Action<TenantMigrationOptions>? configure = null)
    {
        services.AddSingleton<IJsonSeedLoader, JsonSeedLoader>();

        var options = new TenantMigrationOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        return services;
    }

    /// <summary>
    /// Adds <see cref="TenantMigrationMiddleware{TContext}"/> to the application request pipeline.
    /// Ensures individual tenant/trial databases are migrated on-demand before endpoint processing.
    /// </summary>
    /// <typeparam name="TContext">The DbContext to validate and migrate.</typeparam>
    public static IApplicationBuilder UseTenantMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        return app.UseMiddleware<TenantMigrationMiddleware<TContext>>();
    }
}
