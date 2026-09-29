using System.Linq.Expressions;
using System.Text.Json;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

// The report builder's six datasets (TK-113). Each is wide and flat — one row
// per line, movement or record, every descriptive column as text so it can be
// grouped — and the builder narrows it to a report by choosing columns,
// filters and grouping, then saves that layout as a custom report. A dataset
// computes nothing a standard report does not; the standard reports stay the
// authority on their own figures.
//
// Amounts are in the branch's base currency: a dataset sums lines across
// documents in different currencies, and only base amounts add up.

/// <summary>One row per line of a posted sales invoice or POS sale, with the invoice's custom fields.</summary>
public sealed class SalesLinesDataset : DatasetSourceRepository<SalesLineRow>
{
    public SalesLinesDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-sales-lines";

    public override string Title => "Sales lines";

    public override string RequiredPermission => "sales.view";

    protected override CustomFieldEntityKind? Kind => CustomFieldEntityKind.Invoice;

    protected override Expression<Func<SalesLineRow, JsonDocument?>> CustomFieldsOf => r => r.CustomFields;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<SalesLineRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesLineRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesLineRow, string>("documentType", ColumnDataType.Text, r => r.DocumentType, groupable: true),
        ReportColumn.Of<SalesLineRow, string?>("customer", ColumnDataType.Text, r => r.Customer, groupable: true),
        ReportColumn.Of<SalesLineRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<SalesLineRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<SalesLineRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<SalesLineRow, string?>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        ReportColumn.Of<SalesLineRow, string>("currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),
        ReportColumn.Of<SalesLineRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, AggregateFunction.Sum),
        ReportColumn.Of<SalesLineRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<SalesLineRow, decimal>("discountBase", ColumnDataType.Money, r => r.DiscountBase, AggregateFunction.Sum),
        ReportColumn.Of<SalesLineRow, decimal>("taxableBase", ColumnDataType.Money, r => r.TaxableBase, AggregateFunction.Sum),
        ReportColumn.Of<SalesLineRow, decimal>("taxBase", ColumnDataType.Money, r => r.TaxBase, AggregateFunction.Sum),
        ReportColumn.Of<SalesLineRow, decimal>("lineTotalBase", ColumnDataType.Money, r => r.LineTotalBase, AggregateFunction.Sum),
        ReportColumn.Of<SalesLineRow, long>("lineId", ColumnDataType.Number, r => r.LineId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<SalesLineRow, long>>)(r => r.LineId);

    protected override IQueryable<SalesLineRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.Invoices, db.InvoiceDetails, db.Contacts, db.Items, db.Warehouses);

    /// <summary>The query, over sets a test can hand in as lists.</summary>
    public static IQueryable<SalesLineRow> Rows(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> lines,
        IQueryable<ContactRead> contacts,
        IQueryable<ItemRead> items,
        IQueryable<WarehouseRead> warehouses) =>
        from l in lines
        join h in invoices on l.InvoiceId equals h.InvoiceId
        where h.Status == DocumentStatus.Posted
        join c in contacts on h.ContactId equals c.ContactId into cs
        from c in cs.DefaultIfEmpty()
        join i in items on l.ItemId equals (long?)i.ItemId into its
        from i in its.DefaultIfEmpty()
        join w in warehouses on l.WarehouseId equals (long?)w.WarehouseId into ws
        from w in ws.DefaultIfEmpty()
        select new SalesLineRow
        {
            LineId = l.InvoiceDetailId,
            DocumentNo = h.DocumentNo,
            DocumentDate = h.DocumentDate,
            DocumentType = h.TransactionTypeCode,
            Customer = c != null ? c.DisplayName : null,
            ItemCode = i != null ? i.ItemCode : null,
            ItemName = i != null ? i.ItemName : null,
            Description = l.Description,
            Warehouse = w != null ? w.WarehouseName : null,
            CurrencyCode = h.CurrencyCode,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DiscountBase = Math.Round(l.DiscountAmount * h.ExchangeRate, 2),
            TaxableBase = Math.Round(l.TaxableAmount * h.ExchangeRate, 2),
            TaxBase = Math.Round(l.TaxAmount * h.ExchangeRate, 2),
            LineTotalBase = Math.Round(l.LineTotal * h.ExchangeRate, 2),
            CustomFields = h.CustomFields,
        };
}

public sealed class SalesLineRow
{
    public long LineId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string DocumentType { get; set; } = null!;
    public string? Customer { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public string? Warehouse { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountBase { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal TaxBase { get; set; }
    public decimal LineTotalBase { get; set; }
    public JsonDocument? CustomFields { get; set; }
}

/// <summary>One row per line of a posted purchase bill, with the bill's custom fields.</summary>
public sealed class PurchaseLinesDataset : DatasetSourceRepository<PurchaseLineRow>
{
    public PurchaseLinesDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-purchase-lines";

    public override string Title => "Purchase lines";

    public override string RequiredPermission => "purchase.view";

    protected override CustomFieldEntityKind? Kind => CustomFieldEntityKind.Bill;

    protected override Expression<Func<PurchaseLineRow, JsonDocument?>> CustomFieldsOf => r => r.CustomFields;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<PurchaseLineRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<PurchaseLineRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PurchaseLineRow, string?>("vendor", ColumnDataType.Text, r => r.Vendor, groupable: true),
        ReportColumn.Of<PurchaseLineRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<PurchaseLineRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<PurchaseLineRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<PurchaseLineRow, string?>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        ReportColumn.Of<PurchaseLineRow, string>("lineType", ColumnDataType.Text, r => r.LineType, groupable: true),
        ReportColumn.Of<PurchaseLineRow, string>("currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),
        ReportColumn.Of<PurchaseLineRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseLineRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<PurchaseLineRow, decimal>("taxableBase", ColumnDataType.Money, r => r.TaxableBase, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseLineRow, decimal>("taxBase", ColumnDataType.Money, r => r.TaxBase, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseLineRow, decimal>("lineTotalBase", ColumnDataType.Money, r => r.LineTotalBase, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseLineRow, long>("lineId", ColumnDataType.Number, r => r.LineId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<PurchaseLineRow, long>>)(r => r.LineId);

    protected override IQueryable<PurchaseLineRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.Bills, db.BillDetails, db.Contacts, db.Items, db.Warehouses);

    public static IQueryable<PurchaseLineRow> Rows(
        IQueryable<BillRead> bills,
        IQueryable<BillDetailRead> lines,
        IQueryable<ContactRead> contacts,
        IQueryable<ItemRead> items,
        IQueryable<WarehouseRead> warehouses) =>
        from l in lines
        join h in bills on l.BillId equals h.BillId
        where h.Status == DocumentStatus.Posted
        join c in contacts on h.ContactId equals c.ContactId into cs
        from c in cs.DefaultIfEmpty()
        join i in items on l.ItemId equals (long?)i.ItemId into its
        from i in its.DefaultIfEmpty()
        join w in warehouses on l.WarehouseId equals (long?)w.WarehouseId into ws
        from w in ws.DefaultIfEmpty()
        select new PurchaseLineRow
        {
            LineId = l.BillDetailId,
            DocumentNo = h.DocumentNo,
            DocumentDate = h.DocumentDate,
            Vendor = c != null ? c.DisplayName : null,
            ItemCode = i != null ? i.ItemCode : null,
            ItemName = i != null ? i.ItemName : null,
            Description = l.Description,
            Warehouse = w != null ? w.WarehouseName : null,
            LineType = l.LineType == DocumentLineType.Stock ? "Stock"
                : l.LineType == DocumentLineType.Expense ? "Expense"
                : "Capital",
            CurrencyCode = h.CurrencyCode,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            TaxableBase = Math.Round(l.TaxableAmount * h.ExchangeRate, 2),
            TaxBase = Math.Round(l.TaxAmount * h.ExchangeRate, 2),
            LineTotalBase = Math.Round(l.LineTotal * h.ExchangeRate, 2),
            CustomFields = h.CustomFields,
        };
}

public sealed class PurchaseLineRow
{
    public long LineId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string? Vendor { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public string? Warehouse { get; set; }
    public string LineType { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal TaxBase { get; set; }
    public decimal LineTotalBase { get; set; }
    public JsonDocument? CustomFields { get; set; }
}

/// <summary>
/// One row per general-ledger line. No custom fields: a ledger row belongs to
/// no one kind of record, and the documents behind it have their own datasets.
/// </summary>
public sealed class LedgerRowsDataset : DatasetSourceRepository<LedgerDatasetRow>
{
    public LedgerRowsDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-ledger-rows";

    public override string Title => "Ledger rows";

    public override string RequiredPermission => "accounting.view";

    protected override CustomFieldEntityKind? Kind => null;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<LedgerDatasetRow, DateOnly>("ledgerDate", ColumnDataType.Date, r => r.LedgerDate),
        ReportColumn.Of<LedgerDatasetRow, string?>("accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),
        ReportColumn.Of<LedgerDatasetRow, string?>("accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),
        ReportColumn.Of<LedgerDatasetRow, string?>("accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),
        ReportColumn.Of<LedgerDatasetRow, string?>("contact", ColumnDataType.Text, r => r.Contact, groupable: true),
        ReportColumn.Of<LedgerDatasetRow, string>("transactionType", ColumnDataType.Text, r => r.TransactionType, groupable: true),
        ReportColumn.Of<LedgerDatasetRow, string?>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<LedgerDatasetRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<LedgerDatasetRow, decimal>("debitBase", ColumnDataType.Money, r => r.DebitBase, AggregateFunction.Sum),
        ReportColumn.Of<LedgerDatasetRow, decimal>("creditBase", ColumnDataType.Money, r => r.CreditBase, AggregateFunction.Sum),
        ReportColumn.Of<LedgerDatasetRow, decimal>("netBase", ColumnDataType.Money, r => r.NetBase, AggregateFunction.Sum),
        ReportColumn.Of<LedgerDatasetRow, long>("ledgerId", ColumnDataType.Number, r => r.LedgerId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<LedgerDatasetRow, long>>)(r => r.LedgerId);

    protected override IQueryable<LedgerDatasetRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.Ledger, db.Accounts, db.Contacts);

    public static IQueryable<LedgerDatasetRow> Rows(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        IQueryable<ContactRead> contacts) =>
        from l in ledger
        join a in accounts on l.AccountId equals a.AccountId into accs
        from a in accs.DefaultIfEmpty()
        join c in contacts on l.ContactId equals (long?)c.ContactId into cs
        from c in cs.DefaultIfEmpty()
        select new LedgerDatasetRow
        {
            LedgerId = l.LedgerId,
            LedgerDate = l.LedgerDate,

            // mst.AccountTypes is five fixed rows in the master database, which
            // cannot be joined; its names are written here so the column can
            // be grouped in SQL.
            AccountType = a == null ? null
                : a.AccountTypeId == 1 ? "Asset"
                : a.AccountTypeId == 2 ? "Liability"
                : a.AccountTypeId == 3 ? "Equity"
                : a.AccountTypeId == 4 ? "Income"
                : a.AccountTypeId == 5 ? "Expense"
                : null,
            AccountCode = a != null ? a.AccountCode : null,
            AccountName = a != null ? a.AccountName : null,
            Contact = c != null ? c.DisplayName : null,
            TransactionType = l.TransactionTypeCode,
            DocumentNo = l.DocumentNo,
            Description = l.TransactionDesc,
            DebitBase = l.DebitAmountBase,
            CreditBase = l.CreditAmountBase,
            NetBase = l.DebitAmountBase - l.CreditAmountBase,
        };
}

public sealed class LedgerDatasetRow
{
    public long LedgerId { get; set; }
    public DateOnly LedgerDate { get; set; }
    public string? AccountType { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? Contact { get; set; }
    public string TransactionType { get; set; } = null!;
    public string? DocumentNo { get; set; }
    public string? Description { get; set; }
    public decimal DebitBase { get; set; }
    public decimal CreditBase { get; set; }
    public decimal NetBase { get; set; }
}

/// <summary>One row per stock movement, with its item's custom fields.</summary>
public sealed class StockMovementsDataset : DatasetSourceRepository<StockMovementDatasetRow>
{
    public StockMovementsDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-stock-movements";

    public override string Title => "Stock movements";

    public override string RequiredPermission => "inventory.view";

    protected override CustomFieldEntityKind? Kind => CustomFieldEntityKind.Item;

    protected override Expression<Func<StockMovementDatasetRow, JsonDocument?>> CustomFieldsOf => r => r.CustomFields;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<StockMovementDatasetRow, DateOnly>("movementDate", ColumnDataType.Date, r => r.MovementDate),
        ReportColumn.Of<StockMovementDatasetRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, string>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, string?>("category", ColumnDataType.Text, r => r.Category, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, string?>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, string>("direction", ColumnDataType.Text, r => r.Direction, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, string?>("sourceType", ColumnDataType.Text, r => r.SourceType, groupable: true),
        ReportColumn.Of<StockMovementDatasetRow, decimal>("quantityIn", ColumnDataType.Quantity, r => r.QuantityIn, AggregateFunction.Sum),
        ReportColumn.Of<StockMovementDatasetRow, decimal>("quantityOut", ColumnDataType.Quantity, r => r.QuantityOut, AggregateFunction.Sum),
        ReportColumn.Of<StockMovementDatasetRow, decimal>("netQuantity", ColumnDataType.Quantity, r => r.NetQuantity, AggregateFunction.Sum),
        ReportColumn.Of<StockMovementDatasetRow, decimal?>("unitCost", ColumnDataType.Money, r => r.UnitCost),
        ReportColumn.Of<StockMovementDatasetRow, decimal?>("totalCost", ColumnDataType.Money, r => r.TotalCost, AggregateFunction.Sum),
        ReportColumn.Of<StockMovementDatasetRow, long>("stockMovementId", ColumnDataType.Number, r => r.StockMovementId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<StockMovementDatasetRow, long>>)(r => r.StockMovementId);

    protected override IQueryable<StockMovementDatasetRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.StockMovements, db.Items, db.ItemCategories, db.Warehouses);

    public static IQueryable<StockMovementDatasetRow> Rows(
        IQueryable<StockMovementRead> movements,
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        IQueryable<WarehouseRead> warehouses) =>
        from m in movements
        join i in items on m.ItemId equals i.ItemId
        join c in categories on i.ItemCategoryId equals (long?)c.ItemCategoryId into cs
        from c in cs.DefaultIfEmpty()
        join w in warehouses on m.WarehouseId equals (long?)w.WarehouseId into ws
        from w in ws.DefaultIfEmpty()
        select new StockMovementDatasetRow
        {
            StockMovementId = m.StockMovementId,
            MovementDate = m.MovementDate,
            ItemCode = i.ItemCode,
            ItemName = i.ItemName,
            Category = c != null ? c.CategoryName : null,
            Warehouse = w != null ? w.WarehouseName : null,

            // StockDirection: 1 is in, 2 is out, as the tracking reports read it.
            Direction = m.Direction == 1 ? "In" : "Out",
            SourceType = m.SourceType,
            QuantityIn = m.Direction == 1 ? m.Quantity : 0m,
            QuantityOut = m.Direction == 1 ? 0m : m.Quantity,
            NetQuantity = m.Direction == 1 ? m.Quantity : -m.Quantity,
            UnitCost = m.UnitCost,
            TotalCost = m.TotalCost,
            CustomFields = i.CustomFields,
        };
}

public sealed class StockMovementDatasetRow
{
    public long StockMovementId { get; set; }
    public DateOnly MovementDate { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public string? Warehouse { get; set; }
    public string Direction { get; set; } = null!;
    public string? SourceType { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? TotalCost { get; set; }
    public JsonDocument? CustomFields { get; set; }
}

/// <summary>One row per item, with its custom fields.</summary>
public sealed class ItemsDataset : DatasetSourceRepository<ItemDatasetRow>
{
    public ItemsDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-items";

    public override string Title => "Items";

    public override string RequiredPermission => "inventory.view";

    protected override CustomFieldEntityKind? Kind => CustomFieldEntityKind.Item;

    protected override Expression<Func<ItemDatasetRow, JsonDocument?>> CustomFieldsOf => r => r.CustomFields;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<ItemDatasetRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ItemDatasetRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ItemDatasetRow, string?>("category", ColumnDataType.Text, r => r.Category, groupable: true),
        ReportColumn.Of<ItemDatasetRow, string?>("unit", ColumnDataType.Text, r => r.Unit, groupable: true),
        ReportColumn.Of<ItemDatasetRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<ItemDatasetRow, decimal?>("salesPrice", ColumnDataType.Money, r => r.SalesPrice),
        ReportColumn.Of<ItemDatasetRow, decimal?>("purchasePrice", ColumnDataType.Money, r => r.PurchasePrice),
        ReportColumn.Of<ItemDatasetRow, decimal?>("mrp", ColumnDataType.Money, r => r.Mrp),
        ReportColumn.Of<ItemDatasetRow, decimal?>("reorderLevel", ColumnDataType.Quantity, r => r.ReorderLevel),
        ReportColumn.Of<ItemDatasetRow, decimal>("quantityOnHand", ColumnDataType.Quantity, r => r.QuantityOnHand, AggregateFunction.Sum),
        ReportColumn.Of<ItemDatasetRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<ItemDatasetRow, long>>)(r => r.ItemId);

    protected override IQueryable<ItemDatasetRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.Items, db.ItemCategories, db.UnitsOfMeasure, db.ItemStocks);

    public static IQueryable<ItemDatasetRow> Rows(
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        IQueryable<UnitOfMeasureRead> units,
        IQueryable<ItemStockRead> stocks) =>
        from i in items
        join c in categories on i.ItemCategoryId equals (long?)c.ItemCategoryId into cs
        from c in cs.DefaultIfEmpty()
        join u in units on i.InventoryUomId equals u.UomId into us
        from u in us.DefaultIfEmpty()
        select new ItemDatasetRow
        {
            ItemId = i.ItemId,
            ItemCode = i.ItemCode,
            ItemName = i.ItemName,
            Category = c != null ? c.CategoryName : null,
            Unit = u != null ? u.UomName : null,
            Status = i.IsActive ? "Active" : "Inactive",
            SalesPrice = i.SalesPrice,
            PurchasePrice = i.PurchasePrice,
            Mrp = i.Mrp,
            ReorderLevel = i.ReorderLevel,
            QuantityOnHand = stocks.Where(s => s.ItemId == i.ItemId).Sum(s => (decimal?)s.QuantityOnHand) ?? 0m,
            CustomFields = i.CustomFields,
        };
}

public sealed class ItemDatasetRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = null!;
    public decimal? SalesPrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? Mrp { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal QuantityOnHand { get; set; }
    public JsonDocument? CustomFields { get; set; }
}

/// <summary>One row per contact, with its custom fields.</summary>
public sealed class ContactsDataset : DatasetSourceRepository<ContactDatasetRow>
{
    public ContactsDataset(ICustomFieldDefinitions? definitions = null)
        : base(definitions)
    {
    }

    public override string ReportKey => "dataset-contacts";

    public override string Title => "Contacts";

    public override string RequiredPermission => "contacts.view";

    protected override CustomFieldEntityKind? Kind => CustomFieldEntityKind.Contact;

    protected override Expression<Func<ContactDatasetRow, JsonDocument?>> CustomFieldsOf => r => r.CustomFields;

    protected override IReadOnlyList<ReportColumn> BaseColumns =>
    [
        ReportColumn.Of<ContactDatasetRow, string>("contactCode", ColumnDataType.Text, r => r.ContactCode),
        ReportColumn.Of<ContactDatasetRow, string>("displayName", ColumnDataType.Text, r => r.DisplayName),
        ReportColumn.Of<ContactDatasetRow, string?>("legalName", ColumnDataType.Text, r => r.LegalName),
        ReportColumn.Of<ContactDatasetRow, string>("role", ColumnDataType.Text, r => r.Role, groupable: true),
        ReportColumn.Of<ContactDatasetRow, string?>("gstin", ColumnDataType.Text, r => r.Gstin),
        ReportColumn.Of<ContactDatasetRow, string>("currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),
        ReportColumn.Of<ContactDatasetRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<ContactDatasetRow, decimal?>("creditLimit", ColumnDataType.Money, r => r.CreditLimit),
        ReportColumn.Of<ContactDatasetRow, long>("contactId", ColumnDataType.Number, r => r.ContactId, filterable: false),
    ];

    protected override LambdaExpression DefaultOrder => (Expression<Func<ContactDatasetRow, long>>)(r => r.ContactId);

    protected override IQueryable<ContactDatasetRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        Rows(db.Contacts);

    public static IQueryable<ContactDatasetRow> Rows(IQueryable<ContactRead> contacts) =>
        from c in contacts
        select new ContactDatasetRow
        {
            ContactId = c.ContactId,
            ContactCode = c.ContactCode,
            DisplayName = c.DisplayName,
            LegalName = c.LegalName,
            Role = c.IsCustomer && c.IsVendor ? "Customer and vendor"
                : c.IsCustomer ? "Customer"
                : c.IsVendor ? "Vendor"
                : "Other",
            Gstin = c.Gstin,
            CurrencyCode = c.CurrencyCode,
            Status = c.IsActive ? "Active" : "Inactive",
            CreditLimit = c.CreditLimit,
            CustomFields = c.CustomFields,
        };
}

public sealed class ContactDatasetRow
{
    public long ContactId { get; set; }
    public string ContactCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? LegalName { get; set; }
    public string Role { get; set; } = null!;
    public string? Gstin { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal? CreditLimit { get; set; }
    public JsonDocument? CustomFields { get; set; }
}
