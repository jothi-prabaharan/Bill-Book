using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Kernel.Errors;

namespace Shared.Kernel.Persistence;

/// <summary>
/// The two lines every service adds, so neither can be half-wired.
///
/// Transactions and error handling are registered together on purpose. They are
/// one mechanism read from two ends: the filter guarantees a failed request
/// changes nothing, and the handler guarantees the caller is told what happened
/// without being told the schema. Registering one without the other gives you
/// either silent partial writes or exact SQL in a production response, and both
/// were possible in this codebase before they were made a pair.
/// </summary>
public static class ReliabilityRegistration
{
    /// <summary>
    /// Wraps this service's write endpoints in a transaction, and routes its
    /// failures through the catalogue and the error log.
    ///
    /// Call once per DbContext. Master calls it twice — once for
    /// <c>AdminDbContext</c> and once for <c>ContactsDbContext</c> — and the
    /// filter and the handler are registered on the first call only.
    /// </summary>
    /// <typeparam name="TContext">
    /// The context to wrap. It must map <see cref="ErrorLog"/> if it is to be
    /// the one holding the error log; pass <paramref name="writesErrorLog"/>
    /// false for a context that does not.
    /// </typeparam>
    public static IServiceCollection AddBillBookReliability<TContext>(
        this IServiceCollection services,
        bool writesErrorLog = true)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUnitOfWork, DbContextUnitOfWork<TContext>>();

        if (writesErrorLog)
        {
            // TryAdd, not Add: the second context in a two-context service must
            // not replace the first as the place errors are written. Master
            // writes them to con, which is the tenant-scoped one of its two —
            // mst has no CustomerId to scope a row by.
            services.TryAddScoped<IErrorLogStore, ErrorLogStore<TContext>>();
        }

        if (services.Any(d => d.ServiceType == typeof(TransactionFilter)))
        {
            return services;
        }

        services.AddScoped<TransactionFilter>();
        services.Configure<MvcOptions>(options =>
            options.Filters.Add<TransactionFilter>());

        services.AddBillBookErrorHandling();

        return services;
    }

    /// <summary>
    /// Error auditing for a background worker.
    ///
    /// No transaction filter and no exception handler — a worker serves no
    /// requests, and the transactions it does need it already owns. What it
    /// gets is somewhere to record a failure nobody was watching, and the
    /// follow-up status that makes those failures a list rather than a log.
    /// </summary>
    public static IServiceCollection AddBillBookWorkerErrorAudit<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IErrorLogStore, ErrorLogStore<TContext>>();
        services.TryAddScoped<IWorkerErrorAuditor, WorkerErrorAuditor>();

        return services;
    }

    /// <summary>
    /// Error handling with no database behind it — the Gateway. Failures are
    /// translated and logged; there is no ErrorLogs table to write, so the
    /// response carries a trace id and no reference.
    /// </summary>
    public static IServiceCollection AddBillBookErrorHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Without this, UseExceptionHandler() with no arguments has no fallback
        // to fall through to and throws at startup.
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Puts the handler in the pipeline. Must be the first thing added, before
    /// authentication and before the tenant middleware, so a failure in either
    /// of those is answered in the product's shape rather than by Kestrel.
    /// </summary>
    public static IApplicationBuilder UseBillBookErrorHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();

        return app;
    }
}
