using Microsoft.AspNetCore.Authentication;

namespace Shared.Kernel.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
}
