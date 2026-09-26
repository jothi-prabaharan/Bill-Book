namespace Sales.Api.Controllers;

public class MessageResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// A save refused at the credit or discount limit (TK-102). The screen offers
/// "Request approval", which saves again with <c>requestApproval</c>.
/// </summary>
public sealed class LimitRefusalResponse : MessageResponse
{
    public bool CanRequestApproval { get; set; } = true;
}
