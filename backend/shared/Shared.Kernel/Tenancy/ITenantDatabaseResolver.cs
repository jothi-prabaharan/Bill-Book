namespace Shared.Kernel.Tenancy;

public interface ITenantDatabaseResolver
{
    string GetConnectionString(Guid? customerId);
    string GetConnectionString(Guid? customerId, string? databaseName);
}
