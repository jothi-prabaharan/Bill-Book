using System.Collections.Generic;
using System;

namespace Shared.Kernel.Security;

public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public Guid ApiClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// The <c>{module}.{action}</c> codes of the client's role (D-07, TK-29).
    /// Never a <c>platform.*</c> code — Master leaves those out, and the handler
    /// drops any that arrive regardless.
    /// </summary>
    public List<string> Permissions { get; set; } = [];
}
