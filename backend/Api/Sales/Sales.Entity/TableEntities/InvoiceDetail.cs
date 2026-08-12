using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One line of an invoice or a POS sale.</summary>
public class InvoiceDetail : DocumentLineBase
{
    public long InvoiceDetailId { get; set; }

    public long InvoiceId { get; set; }

    /// <summary>The order line this bills, when there is one. A real foreign key.</summary>
    public long? SalesOrderDetailId { get; set; }

    /// <summary>
    /// How much of this line has come back on a credit note.
    ///
    /// It is what stops a customer being credited twice for the same goods: a
    /// credit note line names the invoice line it reverses, and the running total
    /// here is what the guard checks against.
    /// </summary>
    public decimal ReturnedQuantity { get; set; }
}
