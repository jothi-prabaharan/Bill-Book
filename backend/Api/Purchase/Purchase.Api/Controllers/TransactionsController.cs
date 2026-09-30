using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Purchase.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/transactions")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery)]
public sealed class TransactionsController : ControllerBase
{
    private readonly BillRepository _bills;
    private readonly PurchaseOrderRepository _purchaseOrders;
    private readonly DebitNoteRepository _debitNotes;
    private readonly GoodsReceiptRepository _goodsReceipts;

    public TransactionsController(
        BillRepository bills,
        PurchaseOrderRepository purchaseOrders,
        DebitNoteRepository debitNotes,
        GoodsReceiptRepository goodsReceipts)
    {
        _bills = bills;
        _purchaseOrders = purchaseOrders;
        _debitNotes = debitNotes;
        _goodsReceipts = goodsReceipts;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var transactions = new List<PurchaseTransactionListItem>();

        if (string.IsNullOrEmpty(type) || type.Equals("Bill", StringComparison.OrdinalIgnoreCase))
        {
            var bills = await _bills.ListAsync(ct);
            if (from.HasValue) bills = bills.Where(b => b.DocumentDate >= from.Value).ToList();
            if (to.HasValue) bills = bills.Where(b => b.DocumentDate <= to.Value).ToList();
            
            transactions.AddRange(bills.Select(b => new PurchaseTransactionListItem
            {
                TransactionId = b.BillId,
                TransactionType = "Bill",
                DocumentNo = b.DocumentNo,
                DocumentDate = b.DocumentDate,
                ContactId = b.ContactId,
                ContactName = b.ContactName,
                TotalAmount = b.TotalAmount,
                Status = b.Status,
                DueDate = b.DueDate
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase))
        {
            var purchaseOrders = await _purchaseOrders.ListAsync(ct);
            if (from.HasValue) purchaseOrders = purchaseOrders.Where(p => p.DocumentDate >= from.Value).ToList();
            if (to.HasValue) purchaseOrders = purchaseOrders.Where(p => p.DocumentDate <= to.Value).ToList();

            transactions.AddRange(purchaseOrders.Select(p => new PurchaseTransactionListItem
            {
                TransactionId = p.PurchaseOrderId,
                TransactionType = "PurchaseOrder",
                DocumentNo = p.DocumentNo,
                DocumentDate = p.DocumentDate,
                ContactId = p.ContactId,
                ContactName = p.ContactName,
                TotalAmount = p.TotalAmount,
                Status = p.Status,
                DueDate = null // typically doesn't have a specific single due date on list
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("DebitNote", StringComparison.OrdinalIgnoreCase))
        {
            var debitNotes = await _debitNotes.ListAsync(ct);
            if (from.HasValue) debitNotes = debitNotes.Where(d => d.DocumentDate >= from.Value).ToList();
            if (to.HasValue) debitNotes = debitNotes.Where(d => d.DocumentDate <= to.Value).ToList();

            transactions.AddRange(debitNotes.Select(d => new PurchaseTransactionListItem
            {
                TransactionId = d.DebitNoteId,
                TransactionType = "DebitNote",
                DocumentNo = d.DocumentNo,
                DocumentDate = d.DocumentDate,
                ContactId = d.ContactId,
                ContactName = d.ContactName,
                TotalAmount = d.TotalAmount,
                Status = d.Status,
                DueDate = null
            }));
        }

        if (string.IsNullOrEmpty(type) || type.Equals("GoodsReceipt", StringComparison.OrdinalIgnoreCase))
        {
            var goodsReceipts = await _goodsReceipts.ListAsync(ct);
            if (from.HasValue) goodsReceipts = goodsReceipts.Where(g => g.DocumentDate >= from.Value).ToList();
            if (to.HasValue) goodsReceipts = goodsReceipts.Where(g => g.DocumentDate <= to.Value).ToList();

            transactions.AddRange(goodsReceipts.Select(g => new PurchaseTransactionListItem
            {
                TransactionId = g.GoodsReceiptId,
                TransactionType = "GoodsReceipt",
                DocumentNo = g.DocumentNo,
                DocumentDate = g.DocumentDate,
                ContactId = g.ContactId,
                ContactName = g.ContactName,
                TotalAmount = g.TotalAmount, // actually Goods Receipts might not have a total amount in list item?
                Status = g.Status,
                DueDate = null
            }));
        }

        transactions = transactions.OrderByDescending(t => t.DocumentDate).ThenByDescending(t => t.TransactionId).ToList();

        return Ok(transactions);
    }
}
