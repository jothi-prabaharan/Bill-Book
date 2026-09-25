using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sales.Api.Services.EInvoicing;
using Sales.Entity.Enums;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The in-memory IRP (TK-91), and the rule that chooses it: a B2B invoice maps
/// to INV-01 and a sandbox call returns its IRN, which is the card's
/// "done when".
/// </summary>
public sealed class SandboxEInvoiceGatewayTests
{
    private const string Seller = EInvoiceMapperTests.SellerGstin;

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero));

    private SandboxEInvoiceGateway Gateway() => new(_clock);

    private static Inv01Document Invoice() => Inv01Mapper.Map(EInvoiceMapperTests.IntraStateInvoice());

    [Fact]
    public async Task A_b2b_invoice_maps_and_the_sandbox_returns_an_irn()
    {
        Inv01Document document = Invoice();
        Assert.Empty(EInvoiceValidator.Validate(document, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20)));

        IrpResult<IrnDetails> result = await Gateway().GenerateIrnAsync(Seller, document, default);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Matches("^[0-9a-f]{64}$", result.Value!.Irn);
        Assert.False(string.IsNullOrEmpty(result.Value.AckNo));
        Assert.Equal(_clock.GetUtcNow(), result.Value.AckDate);
        Assert.Contains("SANDBOX", result.Value.SignedQrCode);
    }

    [Fact]
    public void The_irn_is_the_hash_of_gstin_type_number_and_financial_year()
    {
        string september = SandboxEInvoiceGateway.IrnFor(Seller, "INV", "INV/26-27/0042", new DateOnly(2026, 9, 20));
        string march = SandboxEInvoiceGateway.IrnFor(Seller, "INV", "INV/26-27/0042", new DateOnly(2027, 3, 31));
        string april = SandboxEInvoiceGateway.IrnFor(Seller, "INV", "INV/26-27/0042", new DateOnly(2027, 4, 1));

        Assert.Equal(september, march);
        Assert.NotEqual(september, april);
        Assert.NotEqual(september, SandboxEInvoiceGateway.IrnFor(Seller, "CRN", "INV/26-27/0042", new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public async Task Registering_twice_is_a_duplicate_and_the_first_irn_can_be_fetched_by_document()
    {
        SandboxEInvoiceGateway gateway = Gateway();
        IrpResult<IrnDetails> first = await gateway.GenerateIrnAsync(Seller, Invoice(), default);

        IrpResult<IrnDetails> second = await gateway.GenerateIrnAsync(Seller, Invoice(), default);
        IrpResult<IrnDetails> fetched = await gateway.GetIrnByDocumentAsync(
            Seller, "INV", "INV/26-27/0042", new DateOnly(2026, 9, 20), default);

        Assert.False(second.Ok);
        Assert.Equal(IrpErrorCodes.DuplicateIrn, second.ErrorCode);
        Assert.False(second.Transient);
        Assert.True(fetched.Ok);
        Assert.Equal(first.Value!.Irn, fetched.Value!.Irn);
        Assert.Equal(first.Value.AckNo, fetched.Value.AckNo);
    }

    [Fact]
    public async Task An_irn_cancels_within_24_hours_once_and_not_after()
    {
        SandboxEInvoiceGateway gateway = Gateway();
        string irn = (await gateway.GenerateIrnAsync(Seller, Invoice(), default)).Value!.Irn;

        _clock.Advance(TimeSpan.FromHours(23));
        IrpResult<IrnCancellation> cancelled = await gateway.CancelIrnAsync(Seller, irn, EInvoiceCancelReason.DataEntryMistake, "Wrong rate", default);
        IrpResult<IrnCancellation> again = await gateway.CancelIrnAsync(Seller, irn, EInvoiceCancelReason.DataEntryMistake, "Wrong rate", default);

        Assert.True(cancelled.Ok);
        Assert.Equal(IrpErrorCodes.AlreadyCancelled, again.ErrorCode);
    }

    [Fact]
    public async Task An_irn_older_than_24_hours_is_refused_a_cancel()
    {
        SandboxEInvoiceGateway gateway = Gateway();
        string irn = (await gateway.GenerateIrnAsync(Seller, Invoice(), default)).Value!.Irn;

        _clock.Advance(TimeSpan.FromHours(25));
        IrpResult<IrnCancellation> result = await gateway.CancelIrnAsync(Seller, irn, EInvoiceCancelReason.OrderCancelled, "Late", default);

        Assert.False(result.Ok);
        Assert.Equal(IrpErrorCodes.CancelWindowPassed, result.ErrorCode);
    }

    [Fact]
    public async Task An_e_way_bill_asked_for_with_the_irn_comes_back_with_it()
    {
        Inv01Document document = Invoice() with
        {
            EwbDtls = new Inv01EwayBill { Distance = 450, TransMode = "1", VehNo = "TN01AB1234", VehType = "R" },
        };

        IrpResult<IrnDetails> result = await Gateway().GenerateIrnAsync(Seller, document, default);

        Assert.NotNull(result.Value!.EwayBill);
        Assert.Matches("^[0-9]{12}$", result.Value.EwayBill!.EwbNo);
        // 450 km by road is three days: one for each full or part 200 km.
        Assert.Equal(_clock.GetUtcNow().AddDays(3), result.Value.EwayBill.ValidUntil);
    }

    [Fact]
    public async Task The_unconfigured_gateway_refuses_everything_and_issues_nothing()
    {
        var gateway = new UnconfiguredEInvoiceGateway();

        IrpResult<IrnDetails> result = await gateway.GenerateIrnAsync(Seller, Invoice(), default);

        Assert.False(result.Ok);
        Assert.Equal(IrpErrorCodes.NotConfigured, result.ErrorCode);
        Assert.False(result.Transient);
    }

    [Theory]
    [InlineData("Development", null, "Sandbox")]
    [InlineData("Development", "Sandbox", "Sandbox")]
    [InlineData("Staging", null, "None")]
    [InlineData("Production", null, "None")]
    [InlineData("Staging", "Sandbox", "Sandbox")]
    public void The_gateway_is_chosen_by_setting_then_by_environment(string environment, string? setting, string expected)
    {
        IServiceProvider provider = Register(environment, setting).BuildServiceProvider();

        Assert.Equal(expected, provider.GetRequiredService<IEInvoiceGateway>().Name);
    }

    [Fact]
    public void The_sandbox_is_refused_in_production()
    {
        Assert.Throws<InvalidOperationException>(() => Register("Production", "Sandbox"));
    }

    [Fact]
    public void An_unknown_gateway_name_is_refused_at_startup()
    {
        Assert.Throws<InvalidOperationException>(() => Register("Development", "Cleartax"));
    }

    [Fact]
    public void Credential_names_are_keyed_by_gstin_and_vault_safe()
    {
        string key = EInvoiceCredentials.PasswordKey("33aaach7409r1z8");

        Assert.Equal("einvoice-33AAACH7409R1Z8-password", key);
        Assert.Matches("^[A-Za-z0-9-]+$", key);
    }

    private static ServiceCollection Register(string environment, string? setting)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(setting is null ? [] : [new KeyValuePair<string, string?>("EInvoicing:Gateway", setting)])
            .Build();
        services.AddEInvoiceGateway(configuration, new Env(environment));
        return services;
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Sales.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>A clock that moves only when told to.</summary>
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
