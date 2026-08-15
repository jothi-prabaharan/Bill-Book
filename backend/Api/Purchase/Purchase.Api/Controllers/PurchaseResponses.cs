namespace Purchase.Api.Controllers;

/// <summary>
/// A refusal the user can read. One per service by existing convention — Sales,
/// Inventory, Accounting and Master each declare their own; consolidating them
/// into Shared.Kernel would touch four services and is not this stage's work.
/// </summary>
public class MessageResponse
{
    public string Message { get; set; } = string.Empty;
}
