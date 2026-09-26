using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Accounting.Entity.Enums;

namespace Accounting.Api.Services.Payments;

/// <summary>An order opened at the gateway, and where to send the payer. A null URL means the portal's own sandbox page.</summary>
public sealed record GatewayOrder(string OrderId, string? CheckoutUrl);

/// <summary>What a verified callback said (TK-98).</summary>
public sealed record GatewayCallback(string Reference, string OrderId, string PaymentId, bool Succeeded, decimal Amount);

/// <summary>
/// A payment gateway (TK-98, design "Client portal" → Online payment): open an
/// order for a payment, and read the gateway's server-to-server callback,
/// verifying its signature. Money is recorded only on a callback this answers
/// for; a browser's return proves nothing. The real provider is D-25's to name.
/// </summary>
public interface IPaymentGateway
{
    PaymentGatewayKind Kind { get; }

    /// <summary>False when no gateway is set up: the portal's Pay button is refused rather than half-working.</summary>
    bool IsConfigured { get; }

    Task<GatewayOrder> CreateOrderAsync(string reference, decimal amount, string currencyCode, CancellationToken ct);

    /// <summary>The callback, or null when it is not this gateway's or its signature does not verify.</summary>
    GatewayCallback? ReadCallback(string body);
}

/// <summary>
/// The sandbox (TK-98, D-25): order ids of its own, and callbacks signed with
/// HMAC-SHA256 over <c>{orderId}|{paymentId}|{status}|{amount}</c> under
/// <c>Payments:Sandbox:Secret</c>. It takes no money, which is why it is refused
/// in Production.
/// </summary>
public sealed class SandboxPaymentGateway : IPaymentGateway
{
    private readonly byte[] _secret;

    public SandboxPaymentGateway(IConfiguration configuration)
    {
        string secret = configuration["Payments:Sandbox:Secret"] is { Length: >= 16 } configured
            ? configured
            : "sandbox-signing-secret-for-development";
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public PaymentGatewayKind Kind => PaymentGatewayKind.Sandbox;

    public bool IsConfigured => true;

    public Task<GatewayOrder> CreateOrderAsync(string reference, decimal amount, string currencyCode, CancellationToken ct) =>
        Task.FromResult(new GatewayOrder($"sbx_order_{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}", null));

    public GatewayCallback? ReadCallback(string body)
    {
        SandboxCallback? callback;
        try
        {
            callback = JsonSerializer.Deserialize<SandboxCallback>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }

        if (callback is not { Reference: { Length: > 0 }, OrderId: { Length: > 0 }, PaymentId: { Length: > 0 }, Status: "captured" or "failed", Signature: { Length: > 0 } })
        {
            return null;
        }

        byte[] expected = Encoding.ASCII.GetBytes(Sign(callback.OrderId, callback.PaymentId, callback.Status, callback.Amount));
        byte[] presented = Encoding.ASCII.GetBytes(callback.Signature);

        return CryptographicOperations.FixedTimeEquals(expected, presented)
            ? new GatewayCallback(callback.Reference, callback.OrderId, callback.PaymentId, callback.Status == "captured", callback.Amount)
            : null;
    }

    /// <summary>The signature the sandbox puts on a callback. Public so the sandbox checkout can sign what it sends.</summary>
    public string Sign(string orderId, string paymentId, string status, decimal amount)
    {
        string payload = string.Join('|', orderId, paymentId, status, amount.ToString("0.00", CultureInfo.InvariantCulture));
        return Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>The callback body the sandbox sends, signed.</summary>
    public string Callback(string reference, string orderId, string paymentId, bool succeeded, decimal amount)
    {
        string status = succeeded ? "captured" : "failed";
        return JsonSerializer.Serialize(new SandboxCallback
        {
            Reference = reference,
            OrderId = orderId,
            PaymentId = paymentId,
            Status = status,
            Amount = amount,
            Signature = Sign(orderId, paymentId, status, amount),
        });
    }

    private sealed class SandboxCallback
    {
        public string Reference { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;

        public string PaymentId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Signature { get; set; } = string.Empty;
    }
}

/// <summary>No gateway: online payment is off, and says so.</summary>
public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    public PaymentGatewayKind Kind => PaymentGatewayKind.Sandbox;

    public bool IsConfigured => false;

    public Task<GatewayOrder> CreateOrderAsync(string reference, decimal amount, string currencyCode, CancellationToken ct) =>
        throw new InvalidOperationException("No payment gateway is configured.");

    public GatewayCallback? ReadCallback(string body) => null;
}

/// <summary>
/// Chooses the gateway (TK-98), the way e-invoicing chooses its IRP gateway.
/// <c>Payments:Gateway</c> names it; unset, it is the sandbox in Development and
/// none elsewhere. The sandbox is refused in Production: it takes no money, and
/// a receipt it made would record money nobody paid.
/// </summary>
public static class PaymentGatewayRegistration
{
    public static IServiceCollection AddPaymentGateway(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        string gateway = configuration["Payments:Gateway"] is { Length: > 0 } named
            ? named
            : environment.IsDevelopment() ? "Sandbox" : "None";

        switch (gateway)
        {
            case "Sandbox" when environment.IsProduction():
                throw new InvalidOperationException(
                    "Payments:Gateway is Sandbox in Production. The sandbox takes no money; configure a real "
                        + "gateway or leave the setting empty.");
            case "Sandbox":
                services.AddSingleton<SandboxPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
                break;
            case "None":
                services.AddSingleton<IPaymentGateway, UnconfiguredPaymentGateway>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Payments:Gateway is '{gateway}', which is not a gateway this build knows. Use Sandbox, or leave it empty.");
        }

        services.AddScoped<OnlinePaymentService>();
        return services;
    }
}
