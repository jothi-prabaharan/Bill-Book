namespace Shared.Kernel.Tenancy;

public interface ITenantDatabaseResolver
{
    string GetConnectionString(Guid? customerId);
}
