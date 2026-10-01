using Microsoft.EntityFrameworkCore;
using SavingsScheme.Api.Controllers;
using SavingsScheme.Entity.TableEntities;
using SavingsScheme.Entity.Enums;
using SavingsScheme.Repository;
using Shared.Kernel.Tenancy;
using Moq;
using Microsoft.AspNetCore.Mvc;

namespace SavingsScheme.Tests;

public class SavingsSchemeFlowTests
{
    private SavingsSchemeDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<SavingsSchemeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
            
        var tenantMock = new Mock<ITenantContext>();
        tenantMock.Setup(t => t.OrgId).Returns(Guid.NewGuid());
        tenantMock.Setup(t => t.CustomerId).Returns(Guid.NewGuid());

        var db = new SavingsSchemeDbContext(options, tenantMock.Object);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Enrol_Pay_Redeem_Flow()
    {
        var db = GetDbContext();
        var defController = new SchemeDefinitionsController(db);
        var encController = new SchemeEnrolmentsController(db);

        // 1. Definition
        var def = new SchemeDefinition
        {
            Name = "Gold Scheme",
            DurationMonths = 2,
            BonusMultiple = 1.0m
        };
        await defController.Create(def);

        // 2. Enrolment
        var enc = new SchemeEnrolment
        {
            SchemeDefinitionId = def.SchemeDefinitionId,
            ContactId = 1,
            StartDate = new DateOnly(2026, 1, 1),
            InstalmentAmount = 1000m
        };
        var enrolRes = await encController.Enrol(enc) as OkObjectResult;
        Assert.NotNull(enrolRes);
        var createdEnc = enrolRes.Value as SchemeEnrolment;
        Assert.NotNull(createdEnc);
        Assert.Equal(EnrolmentStatus.Active, createdEnc.Status);

        // Verify instalments were created
        var instalments = await db.SchemeInstalments.Where(i => i.SchemeEnrolmentId == createdEnc.SchemeEnrolmentId).ToListAsync();
        Assert.Equal(2, instalments.Count);

        // 3. Pay first instalment
        var payRes1 = await encController.PayInstalment(createdEnc.SchemeEnrolmentId) as OkObjectResult;
        Assert.NotNull(payRes1);
        Assert.Equal(1000m, createdEnc.TotalPrincipalPaid);
        Assert.Equal(EnrolmentStatus.Active, createdEnc.Status);

        // 4. Pay second instalment (matures it)
        var payRes2 = await encController.PayInstalment(createdEnc.SchemeEnrolmentId) as OkObjectResult;
        Assert.NotNull(payRes2);
        Assert.Equal(2000m, createdEnc.TotalPrincipalPaid);
        Assert.Equal(EnrolmentStatus.Matured, createdEnc.Status);
        Assert.Equal(1000m, createdEnc.BonusAccrued); // 1.0 * 1000

        // 5. Redeem
        var redeemRes = await encController.Redeem(createdEnc.SchemeEnrolmentId, 3000m) as OkObjectResult;
        Assert.NotNull(redeemRes);
        Assert.Equal(EnrolmentStatus.Redeemed, createdEnc.Status);
    }
}
