using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Seed;
using Xunit;

namespace Shared.Kernel.Tests;

public sealed class JsonSeedLoaderTests
{
    public class TestEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Guid OrgId { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.HasIndex(e => new { e.OrgId, e.Code }).IsUnique();
            });
        }
    }

    [Fact]
    public void JsonSeedLoader_Instantiates_Successfully()
    {
        var loader = new JsonSeedLoader(NullLogger<JsonSeedLoader>.Instance);
        Assert.NotNull(loader);
    }

    [Fact]
    public void TestDbContext_Configures_KeyAndUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=unused;Database=unused")
            .Options;

        using var db = new TestDbContext(options);
        var entityType = db.Model.FindEntityType(typeof(TestEntity));
        Assert.NotNull(entityType);
        Assert.NotNull(entityType.FindPrimaryKey());
        Assert.Contains(entityType.GetIndexes(), i => i.IsUnique);
    }
}
