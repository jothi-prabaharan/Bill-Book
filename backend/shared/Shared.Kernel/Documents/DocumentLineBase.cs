using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Documents;

/// <summary>
/// What every sales and purchase document line carries.
///
/// <b>It carries its own <c>OrgId</c></b>, through <see cref="OrgScopedEntity"/>,
/// like every other detail table in the product. Scoping a line through its
/// header means no EF query filter at all and an RLS policy that has to subquery
/// the header — strictly weaker than the two lines every other table gets for
/// nothing, and the filter is the first line of defence rather than the last.
///
/// See <c>SALES.md</c> §4 for the column list and the checks that go with it.
/// </summary>
public abstract class DocumentLineBase : OrgScopedEntity
{
    /// <summary>
    /// Position on the document, unique within it.
    ///
    /// It is not decoration: the ledger's <c>ITEM</c> leg keys on it, so removing
    /// a line renumbers the rest rather than leaving a gap that a posting would
    /// then point through at nothing.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Line number starts at one.")]
    public int LineNumber { get; set; }

    /// <summary>
    /// The item, from <c>inv.Items</c>. Unenforced id — Inventory owns that table.
    ///
    /// <b>Nullable, and that is a decision rather than a convenience.</b> A
    /// service, a delivery charge or a one-off fitting is a legitimate thing to
    /// trade and does not belong in the item master to satisfy a foreign key. The
    /// consequences are carried on the line: such a line moves no stock, earns no
    /// cost of sale, and must name the account it posts to instead.
    ///
    /// <b>There is no <c>ItemCode</c> or <c>ItemName</c> beside it</b>, for the
    /// same reason the contact's name is absent from the header — a corrected
    /// name should show everywhere, including on documents already raised.
    /// </summary>
    public long? ItemId { get; set; }

    /// <summary>
    /// A snapshot of the item's HSN or SAC, or typed directly on a free-text
    /// line. Required on the face of a GST invoice, and snapshotted because it is
    /// what was <i>filed</i>, not what the item is currently classified as.
    /// </summary>
    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    /// <summary>
    /// What the contact reads. <b>Required when <see cref="ItemId"/> is null</b>,
    /// enforced by check constraint — a line with neither an item nor a
    /// description describes nothing.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Where the goods come off or go to. A <b>location dimension only</b>: stock
    /// is one shared pool per branch, and nothing in the product partitions
    /// quantity, cost layers or weighted average by it.
    /// </summary>
    public long? WarehouseId { get; set; }

    /// <summary>As entered, in <see cref="UomId"/>.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The unit the quantity was entered in. Null on a free-text line.</summary>
    public long? UomId { get; set; }

    /// <summary>
    /// How many inventory units one entered unit is.
    ///
    /// <b>Stored, not re-derived.</b> A unit's conversion factor can be corrected
    /// on the master, and re-deriving would restate the quantity recorded against
    /// a document that has already moved stock.
    /// </summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary><see cref="Quantity"/> in the item's inventory unit. What stock actually moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>Per <b>entered</b> unit, not per inventory unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Whether <see cref="UnitPrice"/> already contains the tax — an MRP.
    ///
    /// Inclusive pricing back-computes the taxable value as
    /// <c>inclusive ÷ (1 + rate)</c>. Retail sells at a printed price and the tax
    /// is inside it, so this is not an edge case.
    /// </summary>
    public bool IsPriceInclusive { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Quantity × unit price, before discount.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// What tax is charged on. Whether the discount reduces it is the branch's
    /// <c>DiscountBeforeTax</c> setting: a trade discount does, a settlement
    /// discount does not and must still be shown.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>Snapshot of the item's tax preference at the document date.</summary>
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Taxable;

    /// <summary>Null unless <see cref="TaxTreatment"/> is Taxable or ZeroRated. Unenforced ids.</summary>
    public long? TaxMasterId { get; set; }

    public long? TaxGroupId { get; set; }

    /// <summary>
    /// The whole of the line's tax. <b>The split is rows</b>, in the matching
    /// <c>{Doc}DetailTaxes</c> table — see <see cref="DocumentLineTaxBase"/> for
    /// why it is not four columns here.
    /// </summary>
    public decimal TaxAmount { get; set; }

    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    /// <summary>
    /// The account a non-item line posts to. <b>Required on a free-text or
    /// Expense line</b> — without it the line has nowhere to go but a control
    /// account, which is the posting that makes a subledger stop tying.
    /// </summary>
    public long? AccountId { get; set; }

    /// <summary>Required when <see cref="LineType"/> is Capital. The category owns the GL mapping, not the asset.</summary>
    public long? FixedAssetCategoryId { get; set; }

    /// <summary><see cref="TaxableAmount"/> plus <see cref="TaxAmount"/>.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>One lot per line. A delivery from two batches is two lines.</summary>
    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}
