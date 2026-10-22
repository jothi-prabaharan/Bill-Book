using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Seed;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

public sealed class TenantMigrationMiddlewareTests
{
    private sealed class DummyDatabaseResolver : ITenantDatabaseResolver
    {
        public string GetAdminConnectionString() => "Host=localhost;Database=Admin";
        public string GetConnectionString(Guid? customerId) => "Host=localhost;Database=Tenant";
        public string GetConnectionString(Guid? customerId, string? databaseName) => $"Host=localhost;Database={databaseName ?? "Tenant"}";
    }

    private sealed class DummySeedLoader : IJsonSeedLoader
    {
        public Task<int> SeedFromDirectoryAsync(DbContext dbContext, string seedDirectoryPath, Guid? orgId = null, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> SeedFileAsync<TEntity>(DbContext dbContext, string jsonFilePath, Func<TEntity, object> keySelector, Guid? orgId = null, CancellationToken ct = default) where TEntity : class => Task.FromResult(0);
        public Task<int> SeedEntitiesAsync<TEntity>(DbContext dbContext, IEnumerable<TEntity> entities, Func<TEntity, object> keySelector, CancellationToken ct = default) where TEntity : class => Task.FromResult(0);
    }

    public class MockDbContext : DbContext
    {
        public MockDbContext(DbContextOptions<MockDbContext> options) : base(options) { }
    }

    [Fact]
    public void DefaultTrialDatabaseName_DefaultsTo_IN000001()
    {
        var options = new TenantMigrationOptions();
        Assert.Equal("IN000001", options.DefaultTrialDatabaseName);
        Assert.True(options.AutoRunJsonSeeder);
    }

    [Fact]
    public void InvalidateCache_DoesNotThrow()
    {
        TenantMigrationMiddleware<MockDbContext>.InvalidateCache();
    }
}
