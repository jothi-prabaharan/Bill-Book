using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Licensing;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace BillBook.Tests.Shared;

/// <summary>
/// A service's context wired the way its <c>Program.cs</c> wires it (TK-210): the
/// audit, row-level-security and quota interceptors on the context, a scope for
/// each request, and the plan's limits and the clock in the test's hands.
///
/// <b>Through the service container on purpose.</b> The quota interceptor makes
/// the month's first row in a scope of its own, so it needs a container that can
/// build the context for a tenant, exactly as the service has. A context newed up
/// beside the interceptor would exercise everything except that.
///
/// Written for TK-210 and not run by the AI that wrote it (docs/Task_Details.md 0.5).
/// </summary>
public sealed class QuotaHarness<TContext> : IAsyncDisposable
    where TContext : TenantDbContext
{
    private readonly ServiceProvider _provider;
    private readonly FakeLimits _limits;
    private readonly FakeClock _clock;

    public QuotaHarness(
        string connectionString,
        PlanLimitKind kind,
        IReadOnlyCollection<Type> counted,
        PlanLimits? limits,
        DateTimeOffset now)
    {
        _limits = new FakeLimits(limits);
        _clock = new FakeClock(now);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IPlanLimitProvider>(_limits);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, NoUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<RlsConnectionInterceptor>();
        services.AddScoped(sp => new DocumentQuotaInterceptor(
            kind,
            counted,
            sp.GetRequiredService<IPlanLimitProvider>(),
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<DocumentQuotaInterceptor>>()));
        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<RlsConnectionInterceptor>(),
                sp.GetRequiredService<DocumentQuotaInterceptor>());
        });

        _provider = services.BuildServiceProvider();
    }

    public IPlanLimitProvider Limits => _limits;

    public TimeProvider Clock => _clock;

    /// <summary>What the plan allows from now on.</summary>
    public void SetLimits(PlanLimits? limits) => _limits.Value = limits;

    /// <summary>The moment the clock reads from now on.</summary>
    public void SetNow(DateTimeOffset now) => _clock.Now = now;

    /// <summary>
    /// One request: a scope with the branch set, and by default a transaction that
    /// commits when the work returns, as the request filter does. Pass
    /// <paramref name="commit"/> false to roll it back, as a failed request does.
    /// </summary>
    public async Task<T> RequestAsync<T>(
        Guid customerId,
        Guid orgId,
        Func<TContext, Task<T>> work,
        bool inTransaction = true,
        bool commit = true)
    {
        await using AsyncServiceScope scope = _provider.CreateAsyncScope();

        TenantContext tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.CustomerId = customerId;
        tenant.OrgId = orgId;
        tenant.CustomerCode = "0000000042";

        TContext db = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var transaction = inTransaction ? await db.Database.BeginTransactionAsync() : null;

        T result = await work(db);

        if (transaction is not null)
        {
            if (commit)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
        }

        return result;
    }

    /// <summary>
    /// Runs <paramref name="insert"/> and saves. Returns <c>ok</c>, or
    /// <c>refused:used/limit</c> when the plan's limit stopped it.
    /// </summary>
    public async Task<string> TryAsync(
        Guid customerId, Guid orgId, Func<TContext, Task> insert, bool inTransaction = true)
    {
        try
        {
            await RequestAsync(
                customerId,
                orgId,
                async db =>
                {
                    await insert(db);
                    await db.SaveChangesAsync();
                    return 0;
                },
                inTransaction);
            return "ok";
        }
        catch (PlanLimitReachedException refusal)
        {
            return $"refused:{refusal.Limit.Used}/{refusal.Limit.Limit}";
        }
    }

    /// <summary>The month's count for a branch, or null when it has no row for the month.</summary>
    public Task<int?> UsedAsync(Guid customerId, Guid orgId, DateOnly period) =>
        RequestAsync<int?>(
            customerId,
            orgId,
            db => db.Set<MonthlyUsage>()
                .Where(u => u.PeriodStart == period)
                .Select(u => (int?)u.DocumentCount)
                .FirstOrDefaultAsync(),
            inTransaction: false);

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private sealed class FakeLimits(PlanLimits? value) : IPlanLimitProvider
    {
        public PlanLimits? Value { get; set; } = value;

        public Task<PlanLimits?> GetAsync(CancellationToken ct = default) => Task.FromResult(Value);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoUser : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => null;
    }
}
