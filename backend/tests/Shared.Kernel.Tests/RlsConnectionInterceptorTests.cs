using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The text <see cref="RlsConnectionInterceptor"/> puts in front of every
/// command (TK-08). What the database does with it is asserted against a real
/// server in <c>Accounting.Api.Tests.TenantInterceptorTests</c>.
/// </summary>
public sealed class RlsConnectionInterceptorTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Org = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void The_prefix_is_transaction_local_and_names_both_ids()
    {
        string prefix = RlsConnectionInterceptor.PrefixFor(Customer, Org)!;

        Assert.Equal(
            $"SET LOCAL app.current_customer_id = '{Customer}'; SET LOCAL app.current_org_id = '{Org}';\n",
            prefix);

        // Never the session-level form the interceptor used to use.
        Assert.DoesNotContain("set_config", prefix, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET SESSION", prefix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Half_a_tenant_clears_the_missing_half()
    {
        Assert.Equal(
            $"SET LOCAL app.current_customer_id = '{Customer}'; SET LOCAL app.current_org_id = '';\n",
            RlsConnectionInterceptor.PrefixFor(Customer, null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_tenant_leaves_the_command_alone(bool empty)
    {
        Guid? none = empty ? Guid.Empty : null;

        Assert.Null(RlsConnectionInterceptor.PrefixFor(none, none));
    }
}
