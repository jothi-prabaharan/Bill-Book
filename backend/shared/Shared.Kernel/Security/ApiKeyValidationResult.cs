using System;

namespace Shared.Kernel.Security;

public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public Guid ApiClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}
