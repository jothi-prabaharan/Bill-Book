using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Controllers;

/// <summary>
/// Segment contacts by purchase history for Customer service's campaign audiences (TK-120).
/// Answers over posted invoices, totals in base currency, and purchased item categories.
/// </summary>
[Route("internal/contacts")]
[ApiController]
[AllowAnonymous]
[InternalOnly]
public sealed class InternalContactSegmentController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly ReportingDbContext _db;

    public InternalContactSegmentController(TenantContext tenant, ReportingDbContext db)
    {
        _tenant = tenant;
        _db = db;
    }

    [HttpPost("segment")]
    public async Task<IActionResult> Segment([FromBody] ContactSegmentRequest request, CancellationToken ct)
    {
        if (request.CustomerId != Guid.Empty) _tenant.CustomerId = request.CustomerId;
        if (request.OrgId != Guid.Empty) _tenant.OrgId = request.OrgId;

        var baseInvoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == DocumentStatus.Posted);

        HashSet<long>? matchingContactIds = null;

        // 1. Last invoice before or after date
        if (request.LastInvoiceBefore.HasValue || request.LastInvoiceAfter.HasValue)
        {
            var contactLatest = await baseInvoices
                .GroupBy(i => i.ContactId)
                .Select(g => new { ContactId = g.Key, LatestDate = g.Max(i => i.DocumentDate) })
                .ToListAsync(ct);

            var filtered = contactLatest.AsEnumerable();
            if (request.LastInvoiceBefore.HasValue)
            {
                filtered = filtered.Where(c => c.LatestDate < request.LastInvoiceBefore.Value);
            }
            if (request.LastInvoiceAfter.HasValue)
            {
                filtered = filtered.Where(c => c.LatestDate > request.LastInvoiceAfter.Value);
            }

            matchingContactIds = filtered.Select(c => c.ContactId).ToHashSet();
        }

        // 2. Sales in period min/max amount
        if (request.SalesMinAmount.HasValue || request.SalesMaxAmount.HasValue || request.SalesPeriodFrom.HasValue || request.SalesPeriodTo.HasValue)
        {
            var periodInvoices = baseInvoices;
            if (request.SalesPeriodFrom.HasValue)
            {
                periodInvoices = periodInvoices.Where(i => i.DocumentDate >= request.SalesPeriodFrom.Value);
            }
            if (request.SalesPeriodTo.HasValue)
            {
                periodInvoices = periodInvoices.Where(i => i.DocumentDate <= request.SalesPeriodTo.Value);
            }

            var contactSales = await periodInvoices
                .GroupBy(i => i.ContactId)
                .Select(g => new { ContactId = g.Key, TotalSales = g.Sum(i => i.TotalAmountBase) })
                .ToListAsync(ct);

            var filtered = contactSales.AsEnumerable();
            if (request.SalesMinAmount.HasValue)
            {
                filtered = filtered.Where(c => c.TotalSales >= request.SalesMinAmount.Value);
            }
            if (request.SalesMaxAmount.HasValue)
            {
                filtered = filtered.Where(c => c.TotalSales <= request.SalesMaxAmount.Value);
            }

            var ids = filtered.Select(c => c.ContactId).ToHashSet();
            if (matchingContactIds is null)
            {
                matchingContactIds = ids;
            }
            else
            {
                matchingContactIds.IntersectWith(ids);
            }
        }

        // 3. Bought item category
        if (request.ItemCategoryId.HasValue)
        {
            long categoryId = request.ItemCategoryId.Value;
            var invoiceIdsWithCategory = await (
                from detail in _db.InvoiceDetails.AsNoTracking()
                join item in _db.Items.AsNoTracking() on detail.ItemId equals item.ItemId
                where item.ItemCategoryId == categoryId
                select detail.InvoiceId
            ).Distinct().ToListAsync(ct);

            var contactIdsWithCategory = await baseInvoices
                .Where(i => invoiceIdsWithCategory.Contains(i.InvoiceId))
                .Select(i => i.ContactId)
                .Distinct()
                .ToListAsync(ct);

            var ids = contactIdsWithCategory.ToHashSet();
            if (matchingContactIds is null)
            {
                matchingContactIds = ids;
            }
            else
            {
                matchingContactIds.IntersectWith(ids);
            }
        }

        return Ok(new ContactSegmentResponse
        {
            ContactIds = (matchingContactIds ?? []).OrderBy(id => id).ToList()
        });
    }

    [HttpPost("first-invoices")]
    public async Task<IActionResult> GetFirstInvoices([FromBody] ContactFirstInvoicesRequest request, CancellationToken ct)
    {
        if (request.CustomerId != Guid.Empty) _tenant.CustomerId = request.CustomerId;
        if (request.OrgId != Guid.Empty) _tenant.OrgId = request.OrgId;

        if (request.ContactIds.Count == 0)
        {
            return Ok(new ContactFirstInvoicesResponse());
        }

        var contactIds = request.ContactIds.Distinct().ToList();

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == DocumentStatus.Posted && contactIds.Contains(i.ContactId))
            .OrderBy(i => i.ContactId)
            .ThenBy(i => i.DocumentDate)
            .ThenBy(i => i.InvoiceId)
            .Select(i => new { i.ContactId, i.DocumentDate, i.TotalAmountBase })
            .ToListAsync(ct);

        var firstInvoices = invoices
            .GroupBy(i => i.ContactId)
            .Select(g => g.First())
            .Select(x => new ContactFirstInvoiceDto
            {
                ContactId = x.ContactId,
                FirstInvoiceDate = x.DocumentDate,
                FirstInvoiceTotal = x.TotalAmountBase
            })
            .ToList();

        return Ok(new ContactFirstInvoicesResponse
        {
            FirstInvoices = firstInvoices
        });
    }
}
