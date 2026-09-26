namespace Accounting.Entity.Enums;

/// <summary>
/// Which payment gateway took an online payment (TK-98). Only the sandbox until
/// D-25 names the real provider.
/// </summary>
public enum PaymentGatewayKind
{
    Sandbox = 1,
}

/// <summary>Where an online payment has got to.</summary>
public enum OnlinePaymentStatus
{
    /// <summary>Checkout opened; nothing heard from the gateway yet.</summary>
    Created = 0,

    /// <summary>The gateway's verified callback said the money was taken.</summary>
    Paid = 1,

    /// <summary>The gateway said the payment failed. Nothing was received.</summary>
    Failed = 2,

    Refunded = 3,
}
