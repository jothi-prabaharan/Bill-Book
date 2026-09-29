using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Contact segmentation by purchase history (TK-120).
/// Tests that filtering by last invoice date returns the right contacts.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactSegmentTests
{
    private readonly PostgresFixture _postgres;

    public ContactSegmentTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Segment_ByLastInvoiceDate_ReturnsRightContacts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using ReportingDbContext db = _postgres.CreateContext(customerId, orgId);

        long contactRecent = 101;
        long contactDormant = 102;
        long contactDraftOnly = 103;

        db.Invoices.AddRange(
            // Contact 101: bought on 2026-08-15 (recent)
            new InvoiceRead
            {
                ContactId = contactRecent,
                DocumentNo = "INV-001",
                TransactionTypeCode = "INV",
                CurrencyCode = "INR",
                DocumentDate = new DateOnly(2026, 8, 15),
                Status = DocumentStatus.Posted,
                TotalAmount = 5000m,
                TotalAmountBase = 5000m,
                ExchangeRate = 1.0m,
                CustomerId = customerId,
                OrgId = orgId
            },
            // Contact 102: bought on 2026-01-10 (dormant)
            new InvoiceRead
            {
                ContactId = contactDormant,
                DocumentNo = "INV-002",
                TransactionTypeCode = "INV",
                CurrencyCode = "INR",
                DocumentDate = new DateOnly(2026, 1, 10),
                Status = DocumentStatus.Posted,
                TotalAmount = 2000m,
                TotalAmountBase = 2000m,
                ExchangeRate = 1.0m,
                CustomerId = customerId,
                OrgId = orgId
            },
            // Contact 103: Draft invoice only (never posted)
            new InvoiceRead
            {
                ContactId = contactDraftOnly,
                DocumentNo = "INV-003",
                TransactionTypeCode = "INV",
                CurrencyCode = "INR",
                DocumentDate = new DateOnly(2026, 1, 1),
                Status = DocumentStatus.Draft,
                TotalAmount = 10000m,
                TotalAmountBase = 10000m,
                ExchangeRate = 1.0m,
                CustomerId = customerId,
                OrgId = orgId
            }
        );

        await db.SaveChangesAsync();

        var controller = new InternalContactSegmentController(tenant, db);

        // Filter: Last invoice before 2026-06-01 (target dormant customers)
        var dormantRequest = new ContactSegmentRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            LastInvoiceBefore = new DateOnly(2026, 6, 1)
        };

        var result = await controller.Segment(dormantRequest, default);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ContactSegmentResponse>(okResult.Value);

        Assert.Single(response.ContactIds);
        Assert.Contains(contactDormant, response.ContactIds);
        Assert.DoesNotContain(contactRecent, response.ContactIds);
        Assert.DoesNotContain(contactDraftOnly, response.ContactIds);

        // Filter: Last invoice after 2026-06-01 (target recent buyers)
        var recentRequest = new ContactSegmentRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            LastInvoiceAfter = new DateOnly(2026, 6, 1)
        };

        var recentResult = await controller.Segment(recentRequest, default);
        var recentOk = Assert.IsType<OkObjectResult>(recentResult);
        var recentResponse = Assert.IsType<ContactSegmentResponse>(recentOk.Value);

        Assert.Single(recentResponse.ContactIds);
        Assert.Contains(contactRecent, recentResponse.ContactIds);
        Assert.DoesNotContain(contactDormant, recentResponse.ContactIds);
    }
}
