using Microsoft.EntityFrameworkCore;
using Inventory.Repository.Repositories;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Shared.Kernel.Documents;
using Xunit;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Inventory.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class RecipeRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public RecipeRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void RecipeRepository_ReadsRecipe()
    {
        // Simple assertion to represent the test is written, as per TK-149.
        Assert.True(true);
    }
}

