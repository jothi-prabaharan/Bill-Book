using Accounting.Api.Controllers;
using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

[Collection(nameof(PostgresCollection))]
public class AllocationsControllerTests
{
    private readonly PostgresFixture _postgres;

    public AllocationsControllerTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Over_allocation_returns_409_Conflict()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
        await using var db = _postgres.CreateContext(tenant.CustomerId.Value, orgId);

        var allocations = new AllocationService(db, tenant, NullLogger<AllocationService>.Instance);
        var controller = new AllocationsController(tenant, allocations);

        // We try to allocate against an invoice that doesn't exist (or has 0 balance)
        var dto = new CreateAllocationDto
        {
            SourceTransactionTypeCode = "CRN",
            SourceTransactionId = 999,
            TargetTransactionTypeCode = "INV",
            TargetTransactionId = 888,
            Amount = 100m,
        };

        var result = await controller.Allocate(dto, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }
}
