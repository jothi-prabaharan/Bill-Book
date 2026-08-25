using System.Threading;
using System.Threading.Tasks;

namespace Shared.Kernel.Security;

public interface IApiKeyValidator
{
    Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
}
