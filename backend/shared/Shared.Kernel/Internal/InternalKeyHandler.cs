using Microsoft.Extensions.Configuration;

namespace Shared.Kernel.Internal;

/// <summary>
/// Attaches the shared internal key to every service-to-service request.
///
/// The other half of <see cref="InternalOnlyAttribute"/>, and the reason it is a
/// handler rather than a line at each call site: there are eight of those across
/// six services, and the ones that were missing the header were missing it
/// silently — the endpoints had no guard, so nothing failed. Adding the guard
/// without this would have broken login, tenant resolution and signup all at
/// once.
///
/// A blank key adds no header, which makes the endpoint answer 401 rather than
/// the caller throwing. That is the right way round: a misconfigured deployment
/// should fail at the door it is trying to open, with a status that says so,
/// not in the middle of the calling service.
/// </summary>
public sealed class InternalKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;

    public InternalKeyHandler(IConfiguration configuration) => _configuration = configuration;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? key = _configuration["Internal:ApiKey"];

        // Never overwrite one already set. TenantSeeder sets its own, and a
        // handler quietly replacing an explicit header is the kind of thing
        // nobody looks for.
        if (!string.IsNullOrWhiteSpace(key)
            && !request.Headers.Contains(InternalOnlyAttribute.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(InternalOnlyAttribute.HeaderName, key);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
