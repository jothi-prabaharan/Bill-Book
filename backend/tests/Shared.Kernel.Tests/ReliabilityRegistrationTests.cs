using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Kernel.Errors;
using Shared.Kernel.Persistence;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// That the one call a service makes actually installs everything.
///
/// The behaviour of the filter is asserted in TransactionFilterTests. What is
/// asserted here is the wiring, because a filter that works perfectly and is
/// never added to the pipeline looks exactly like one that is — and this
/// codebase has the scar: every endpoint was "behind a credential and a
/// permission" twice over, and both times the gap was a registration nobody
/// had looked at rather than logic anybody had got wrong.
/// </summary>
public class ReliabilityRegistrationTests
{
    private static MvcOptions Mvc(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IOptions<MvcOptions>>().Value;

    [Fact]
    public void One_call_adds_the_filter_the_unit_of_work_and_the_error_log()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FirstContext>(o => o.UseInMemoryDatabaseStub());

        services.AddBillBookReliability<FirstContext>();

        Assert.Contains(services, d => d.ServiceType == typeof(IUnitOfWork));
        Assert.Contains(services, d => d.ServiceType == typeof(IErrorLogStore));
        Assert.Contains(
            Mvc(services).Filters,
            f => f is TypeFilterAttribute t && t.ImplementationType == typeof(TransactionFilter));
    }

    [Fact]
    public void A_second_context_adds_a_second_unit_of_work_but_only_one_filter()
    {
        // Master's shape. Two transactions per write request, one filter to
        // drive both — a filter added twice would begin, commit and dispose
        // each unit of work twice.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBillBookReliability<FirstContext>();
        services.AddBillBookReliability<SecondContext>(writesErrorLog: false);

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IUnitOfWork)));

        Assert.Single(
            Mvc(services).Filters,
            f => f is TypeFilterAttribute t && t.ImplementationType == typeof(TransactionFilter));
    }

    [Fact]
    public void The_error_log_stays_with_the_first_context_that_claimed_it()
    {
        // Master registers ContactsDbContext first because it is the tenant
        // scoped one; mst has no CustomerId to scope an ErrorLog row by. A
        // second call must not take the registration over.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBillBookReliability<FirstContext>();
        services.AddBillBookReliability<SecondContext>();

        ServiceDescriptor store = Assert.Single(
            services, d => d.ServiceType == typeof(IErrorLogStore));

        Assert.Equal(typeof(ErrorLogStore<FirstContext>), store.ImplementationType);
    }

    [Fact]
    public void A_context_that_does_not_hold_the_error_log_registers_no_store()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBillBookReliability<FirstContext>(writesErrorLog: false);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IErrorLogStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IUnitOfWork));
    }

    [Fact]
    public void The_gateway_gets_the_handler_without_a_transaction_filter()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBillBookErrorHandling();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IUnitOfWork));
        Assert.DoesNotContain(
            Mvc(services).Filters,
            f => f is TypeFilterAttribute t && t.ImplementationType == typeof(TransactionFilter));
    }

    [Fact]
    public void A_worker_gets_the_audit_without_a_filter_or_a_unit_of_work()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBillBookWorkerErrorAudit<FirstContext>();

        Assert.Contains(services, d => d.ServiceType == typeof(IErrorLogStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IWorkerErrorAuditor));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IUnitOfWork));
    }

    private sealed class FirstContext : DbContext
    {
        public FirstContext(DbContextOptions<FirstContext> options) : base(options) { }
    }

    private sealed class SecondContext : DbContext
    {
        public SecondContext(DbContextOptions<SecondContext> options) : base(options) { }
    }
}

internal static class StubExtensions
{
    /// <summary>No provider is needed: nothing here resolves a context, only the registrations are read.</summary>
    public static DbContextOptionsBuilder UseInMemoryDatabaseStub(this DbContextOptionsBuilder builder) => builder;
}
