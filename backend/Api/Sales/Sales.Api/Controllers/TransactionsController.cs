using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly QuoteService _quotes;
    private readonly SalesOrderService _salesOrders;
    private readonly InvoiceService _invoices;
    private readonly CreditNoteService _creditNotes;

    public TransactionsController(
        QuoteService quotes,
        SalesOrderService salesOrders,
        InvoiceService invoices,
        CreditNoteService creditNotes)
    {
        _quotes = quotes;
        _salesOrders = salesOrders;
        _invoices = invoices;
        _creditNotes = creditNotes;
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
                TotalAmount = q.TotalAmount,
                Status = q.Status,
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
                TotalAmount = s.TotalAmount,
                Status = s.Status.ToString(), // Assuming enum DocumentStatus
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
                TotalAmount = c.TotalAmount,
                Status = c.Status.ToString(),
                DueDate = null
            }));
        }

        // Sort unified list descending by Document Date
        transactions = transactions.OrderByDescending(t => t.DocumentDate).ThenByDescending(t => t.TransactionId).ToList();

        return Ok(transactions);
    }
}
