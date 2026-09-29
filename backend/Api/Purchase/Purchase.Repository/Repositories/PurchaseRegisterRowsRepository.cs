using Purchase.Entity.TableEntities;
using Shared.Kernel.Documents;

namespace Purchase.Repository.Repositories;

/// <summary>
/// A posted document's rows in <c>pur.PurchaseRegister</c> (TK-115), one per
/// line. Pure, so what a bill puts in the register is tested without a database.
/// </summary>
public static class PurchaseRegisterRowsRepository
{
    public static IEnumerable<PurchaseRegister> ForBill(Bill bill) =>
        bill.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => Row(
                bill, "BIL", bill.BillId, l.BillDetailId, bill.DocumentNo, bill.VendorBillNo, bill.VendorBillDate,
                bill.IsReverseCharge, l.ItcEligibility, l, l.Taxes.Select(t => (t.TaxComponent, t.Rate, t.Amount)),
                originalBillId: null, originalBillNo: null));

    /// <summary>
    /// A debit note's rows. Eligibility and reverse charge are the bill line's:
    /// what goes back reverses exactly what came in, credit claimed or not.
    /// </summary>
    public static IEnumerable<PurchaseRegister> ForDebitNote(DebitNote note, Bill bill)
    {
        Dictionary<long, BillDetail> billLines = bill.Lines.ToDictionary(l => l.BillDetailId);

        return note.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => Row(
                note, "DBN", note.DebitNoteId, l.DebitNoteDetailId, note.DocumentNo, bill.VendorBillNo, bill.VendorBillDate,
                bill.IsReverseCharge,
                billLines.TryGetValue(l.BillDetailId, out BillDetail? source) ? source.ItcEligibility : ItcEligibility.Eligible,
                l, l.Taxes.Select(t => (t.TaxComponent, t.Rate, t.Amount)),
                originalBillId: bill.BillId, originalBillNo: bill.DocumentNo));
    }

    private static PurchaseRegister Row(
        DocumentHeaderBase header,
        string type,
        long sourceId,
        long sourceLineId,
        string? documentNo,
        string supplierInvoiceNo,
        DateOnly supplierInvoiceDate,
        bool reverseCharge,
        ItcEligibility eligibility,
        DocumentLineBase line,
        IEnumerable<(TaxComponent Component, decimal Rate, decimal Amount)> taxes,
        long? originalBillId,
        string? originalBillNo)
    {
        List<(TaxComponent Component, decimal Rate, decimal Amount)> components = [.. taxes];
        decimal Of(TaxComponent c) => components.Where(t => t.Component == c).Sum(t => t.Amount);
        decimal tax = components.Sum(t => t.Amount);

        return new PurchaseRegister
        {
            OrgId = header.OrgId,
            TransactionTypeCode = type,
            SourceId = sourceId,
            SourceLineId = sourceLineId,
            DocumentNo = documentNo ?? $"{type}-{sourceId}",
            DocumentDate = header.DocumentDate,
            SupplierInvoiceNo = supplierInvoiceNo,
            SupplierInvoiceDate = supplierInvoiceDate,
            ContactId = header.ContactId,
            ContactGstin = header.ContactGstin,
            PlaceOfSupplyStateId = header.PlaceOfSupplyStateId,
            IsInterState = header.IsInterState,
            ReverseCharge = reverseCharge,
            ItcEligibility = eligibility,
            LineType = line.LineType,
            HsnSacCode = line.HsnSacCode,

            // The line's rate: IGST's alone, or CGST and SGST together.
            GstRate = components.Where(t => t.Component != TaxComponent.Cess).Sum(t => t.Rate),
            Quantity = line.Quantity,
            TaxableAmount = line.TaxableAmount,
            CgstAmount = Of(TaxComponent.Cgst),
            SgstAmount = Of(TaxComponent.Sgst) + Of(TaxComponent.Utgst),
            IgstAmount = Of(TaxComponent.Igst),
            CessAmount = Of(TaxComponent.Cess),
            TotalAmount = line.TaxableAmount + tax,
            CurrencyCode = header.CurrencyCode,
            ExchangeRate = header.ExchangeRate,
            TaxableAmountBase = Math.Round(line.TaxableAmount * header.ExchangeRate, 2),
            TaxAmountBase = Math.Round(tax * header.ExchangeRate, 2),
            OriginalBillId = originalBillId,
            OriginalBillNo = originalBillNo,
        };
    }
}
