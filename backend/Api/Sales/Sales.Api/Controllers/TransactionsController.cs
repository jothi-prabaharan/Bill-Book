using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Repository.Repositories;
using Sales.Repository.Clients;
using Sales.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/transactions")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.Service)]
public sealed class TransactionsController : ControllerBase
{
    private readonly QuoteRepository _quotes;
    private readonly SalesOrderRepository _salesOrders;
    private readonly InvoiceRepository _invoices;
    private readonly CreditNoteRepository _creditNotes;
    private readonly DeliveryChallanRepository _deliveryChallans;

    public TransactionsController(
        QuoteRepository quotes,
        SalesOrderRepository salesOrders,
        InvoiceRepository invoices,
        CreditNoteRepository creditNotes,
        DeliveryChallanRepository deliveryChallans)
    {
        _quotes = quotes;
        _salesOrders = salesOrders;
        _invoices = invoices;
        _creditNotes = creditNotes;
        _deliveryChallans = deliveryChallans;
    }

    /// <summary>
    /// The document a printed number names (TK-162), for scan-to-open. A number
    /// that is not in this branch is a 404, the same as no such number.
    /// </summary>
    [HttpGet("by-number")]
    public async Task<IActionResult> FindByNumber(
        [FromQuery] string? no,
        [FromServices] DocumentNumberRepository numbers,
        CancellationToken ct)
    {
        DocumentNumberMatch? match = await numbers.FindAsync(no, ct);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var transactions = new List<SalesTransactionListItem>();

        if (string.IsNullOrEmpty(type) || type.Equals("Quote", StringComparison.OrdinalIgnoreCase))
        {
            var quotes = await _quotes.ListAsync(ct);
            // Apply memory filter if needed
            if (from.HasValue) quotes = quotes.Where(q => q.DocumentDate >= from.Value).ToList();
            if (to.HasValue) quotes = quotes.Where(q => q.DocumentDate <= to.Value).ToList();
            
            transactions.AddRange(quotes.Select(q => new SalesTransactionListItem
            {
                TransactionId = q.QuoteId,
                TransactionType = "Quote",
                DocumentNo = q.DocumentNo,
                DocumentDate = q.DocumentDate,
                ContactId = q.ContactId,
                ContactName = q.ContactName,
                CurrencyCode = q.CurrencyCode,
                TaxableAmount = q.TaxableAmount,
                TotalAmount = q.TotalAmount,
                Status = q.Status,
                CustomerResponse = q.CustomerResponse == "None" ? null : q.CustomerResponse,
                DueDate = q.ValidUntil
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
        {
            // The whole first page rather than the whole table: this screen mixes
            // five document types and pages over the mixture, so asking each one
            // for everything is the query that gets slower every month.
            var salesOrders = (await _salesOrders.ListAsync(0, 200, null, null, ct)).Rows;
            if (from.HasValue) salesOrders = salesOrders.Where(s => s.DocumentDate >= from.Value).ToList();
            if (to.HasValue) salesOrders = salesOrders.Where(s => s.DocumentDate <= to.Value).ToList();

            transactions.AddRange(salesOrders.Select(s => new SalesTransactionListItem
            {
                TransactionId = s.SalesOrderId,
                TransactionType = "SalesOrder",
                DocumentNo = s.DocumentNo,
                DocumentDate = s.DocumentDate,
                ContactId = s.ContactId,
                ContactName = s.ContactName,
                CurrencyCode = s.CurrencyCode,
                TaxableAmount = s.TaxableAmount,
                TotalAmount = s.TotalAmount,
                Status = s.Status.ToString(), // Assuming enum DocumentStatus
                FulfilmentStatus = s.FulfilmentStatus,
                DueDate = null // Sales Orders typically don't have due dates
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var invoices = await _invoices.ListAsync(from, to, ct);
            transactions.AddRange(invoices.Select(i => new SalesTransactionListItem
            {
                TransactionId = i.InvoiceId,
                TransactionType = "Invoice",
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
                ContactId = i.ContactId,
                ContactName = i.ContactName,
                CurrencyCode = i.CurrencyCode,
                TaxableAmount = i.TaxableAmount,
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                DueDate = i.DueDate
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("CreditNote", StringComparison.OrdinalIgnoreCase))
        {
            var creditNotes = await _creditNotes.ListAsync(from, to, ct);
            transactions.AddRange(creditNotes.Select(c => new SalesTransactionListItem
            {
                TransactionId = c.CreditNoteId,
                TransactionType = "CreditNote",
                DocumentNo = c.DocumentNo,
                DocumentDate = c.DocumentDate,
                ContactId = c.ContactId,
                ContactName = c.ContactName,
                CurrencyCode = c.CurrencyCode,
                TaxableAmount = c.TaxableAmount,
                TotalAmount = c.TotalAmount,
                Status = c.Status.ToString(),
                DueDate = null
            }));
        }

        // The Delivery challans list asks for this type, and until TK-187 there
        // was no branch for it, so that list was always empty.
        if (string.IsNullOrEmpty(type) || type.Equals("DeliveryChallan", StringComparison.OrdinalIgnoreCase))
        {
            var deliveryChallans = await _deliveryChallans.ListAsync(from, to, ct);
            transactions.AddRange(deliveryChallans.Select(d => new SalesTransactionListItem
            {
                TransactionId = d.DeliveryChallanId,
                TransactionType = "DeliveryChallan",
                DocumentNo = d.DocumentNo,
                DocumentDate = d.DocumentDate,
                ContactId = d.ContactId,
                ContactName = d.ContactName,
                CurrencyCode = d.CurrencyCode,
                TaxableAmount = d.TaxableAmount,
                TotalAmount = d.TotalAmount,
                Status = d.Status,
                DueDate = null
            }));
        }

        // Sort unified list descending by Document Date
        transactions = transactions.OrderByDescending(t => t.DocumentDate).ThenByDescending(t => t.TransactionId).ToList();

        return Ok(transactions);
    }
}
