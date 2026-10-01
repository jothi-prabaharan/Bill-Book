using Microsoft.Extensions.DependencyInjection;

namespace BillBook.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        // Jobs will be registered here (e.g. from TK-138)
        return services;
    }
}
