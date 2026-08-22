using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.TableEntities;

namespace Reporting.Repository.SeedData;

/// <summary>
/// Writes the report catalog for a branch — the <c>rpt.Reports</c> row for each
/// report and the <c>rpt.ReportDetails</c> row for each of its columns.
///
/// <b>Re-runnable, and meant to be.</b> It adds only what is missing, so calling
/// it against a branch set up months ago backfills every report added since. That
/// is not a nicety: the catalog grows with each stage, and forty-five reports will
/// not all arrive on the same day. A branch seeded today and left alone would
/// otherwise never see R2's inventory reports.
///
/// <b>What it does not do is update.</b> A header a branch has renamed into its own
/// language stays renamed, and a column somebody turned off stays off — re-running
/// the seeder must never overwrite a branch's own choices, or the backfill becomes
/// a reset that quietly undoes somebody's configuration.
/// </summary>
public sealed class ReportCatalogSeeder
{
    private readonly ReportingDbContext _db;

    public ReportCatalogSeeder(ReportingDbContext db) => _db = db;

    /// <summary>Rows written, by report key. Empty on a branch already complete.</summary>
    public async Task<Dictionary<string, int>> SeedAsync(CancellationToken ct)
    {
        Dictionary<string, int> written = [];

        foreach (ReportSeed seed in Catalog)
        {
            int rows = await SeedOneAsync(seed, ct);

            if (rows > 0)
            {
                written[seed.ReportKey] = rows;
            }
        }

        await _db.SaveChangesAsync(ct);

        return written;
    }

    private async Task<int> SeedOneAsync(ReportSeed seed, CancellationToken ct)
    {
        Report? report = await _db.Reports
            .FirstOrDefaultAsync(r => r.ReportKey == seed.ReportKey, ct);

        int written = 0;

        if (report is null)
        {
            report = new Report
            {
                ReportKey = seed.ReportKey,
                Title = seed.Title,
                Module = seed.Module,
                Description = seed.Description,
                RequiredPermission = seed.RequiredPermission,
                IsActive = true,
                SortOrder = seed.SortOrder,
            };

            _db.Reports.Add(report);

            // Needed before the detail rows can reference it. The report and its
            // columns are one unit — a report row with no columns renders an empty
            // grid rather than failing, which is worse than not appearing at all.
            await _db.SaveChangesAsync(ct);
            written++;
        }

        HashSet<string> existing =
        [
            .. await _db.ReportDetails
                .Where(d => d.ReportId == report.ReportId)
                .Select(d => d.ColumnKey)
                .ToListAsync(ct),
        ];

        int order = 0;

        foreach (ColumnSeed column in seed.Columns)
        {
            order++;

            if (existing.Contains(column.Key))
            {
                continue;
            }

            _db.ReportDetails.Add(new ReportDetail
            {
                ReportId = report.ReportId,
                ColumnKey = column.Key,
                Header = column.Header,
                DataType = column.DataType,
                IsDefault = column.IsDefault,
                IsFilterable = column.IsFilterable,
                IsSortable = true,
                IsGroupable = column.IsGroupable,
                IsPivotable = column.IsPivotable,
                DefaultAggregate = column.Aggregate,
                Alignment = column.Alignment,
                SortOrder = order,
                IsPrimary = column.IsPrimary,
                IsHidden = column.IsHidden,
            });

            written++;
        }

        return written;
    }

    /// <summary>
    /// The catalog. <b>Every column an <c>IReportSource</c> declares needs a row
    /// here and nothing else does</b> — the two lists are compared when a report's
    /// columns are built, and a mismatch refuses the report by name. Adding a
    /// column to a source without adding it here breaks that report rather than
    /// quietly omitting the column, which is the intended trade.
    /// </summary>
    private static IReadOnlyList<ReportSeed> Catalog =>
    [
        new()
        {
            ReportKey = "account-movement",
            Title = "Account Movement",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Every posting against every account, in date order.",
            SortOrder = 10,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("account", "Account", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPrimary: true),
                new("debit", "Debit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text, IsDefault: true),
                new("source", "Source", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPivotable: true),
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "account-transaction",
            Title = "Account Transaction",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description =
                "Every posting with its contact, its currency and a running balance.",
            SortOrder = 15,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true,
                    IsPrimary: true),
                new("source", "Source", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPivotable: true),
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("account", "Account", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("contactCode", "Contact Code", ColumnDataType.Text),
                new("contactName", "Contact Name", ColumnDataType.Text, IsDefault: true),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text),
                new("currency", "Currency", ColumnDataType.Text, IsGroupable: true),
                new("exchangeRate", "ExchangeRate", ColumnDataType.Rate,
                    Alignment: ColumnAlignment.Right),
                new("debitSource", "Debit(Source)", ColumnDataType.Money,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("creditSource", "Credit(Source)", ColumnDataType.Money,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("debit", "Debit(%CurCode%)", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit(%CurCode%)", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("runningBalance", "Running Balance", ColumnDataType.Money,
                    IsDefault: true, IsFilterable: false, Alignment: ColumnAlignment.Right),
                new("ledgerId", "Ledger", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "trial-balance",
            Title = "Trial Balance",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Every account with a balance, and the two totals that must agree.",
            SortOrder = 20,
            Columns =
            [
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsPrimary: true),
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPrimary: true),
                new("debit", "Debit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("currentBalance", "Current Balance", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                // CAAccountID in the source list. An internal key, never offered.
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "general-ledger-summary",
            Title = "General Ledger Summary",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Opening balance, period activity, and closing balance per account.",
            SortOrder = 25,
            Columns =
            [
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("account", "Account", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("openingBalance", "Opening Balance", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("debit", "Debit", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netMovement", "Net Movement", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("closing", "Closing", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "journal-report",
            Title = "Journal Report",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Every journal with its lines, audit trail, and status.",
            SortOrder = 30,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("reference", "Reference", ColumnDataType.Text, IsDefault: true),
                new("narration", "Narration", ColumnDataType.Text, IsDefault: true),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("transactions", "Transactions", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("contactName", "Contact Name", ColumnDataType.Text),
                new("currency", "Currency", ColumnDataType.Text, IsGroupable: true),
                new("exchangeRate", "Exchange Rate", ColumnDataType.Rate, Alignment: ColumnAlignment.Right),
                new("debitSource", "Debit(Source)", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("creditSource", "Credit(Source)", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("debit", "Debit(%CurCode%)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit(%CurCode%)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("createdByName", "Created By", ColumnDataType.Text, IsDefault: true),
                new("createdAt", "Created Date", ColumnDataType.DateTime, IsDefault: true),
                new("postedByName", "Approved/Posted By", ColumnDataType.Text),
                new("postedAt", "Approved/Posted Date", ColumnDataType.DateTime),
                new("modifiedByName", "Last Modified By", ColumnDataType.Text),
                new("modifiedAt", "Last Modified Date", ColumnDataType.DateTime),
                new("status", "Reconciled Status", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("journalDetailId", "Journal Detail", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "bank-summary",
            Title = "Bank Summary",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Opening, received, spent, closing, and revaluation per bank account.",
            SortOrder = 35,
            Columns =
            [
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("accountType", "Account Type", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("currencyCode", "Currency Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("openingBalance", "Opening Balance", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("received", "Received", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("spent", "Spent", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("closingBalance", "Closing Balance", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("bankRevaluation", "Bank Revaluation", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("bankAccountId", "Bank Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "reconciliation",
            Title = "Reconciliation Report",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Bank statement lines with matched/unmatched status and amounts.",
            SortOrder = 40,
            Columns =
            [
                new("transactionDate", "Transaction Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text, IsDefault: true),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("amountIn", "Amount In", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("amountOut", "Amount Out", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("status", "Status", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("statement", "Statement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("bankStatementLineId", "Statement Line", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "inventory-aging",
            Title = "Inventory Aging Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Inventory aged by cost layer receipt date with dynamic buckets.",
            SortOrder = 50,
            Columns =
            [
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("inventoryAssetAccount", "Inventory Asset Account", ColumnDataType.Text),
                new("bucket", "Aged Inventory", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("agedInventoryQuantity", "Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("agedInventoryValue", "Value", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("costLayerId", "Cost Layer", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "inventory-item-list",
            Title = "Inventory Item List",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Master item list with stock, pricing, and accounts.",
            SortOrder = 55,
            Columns =
            [
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemGroup", "Item Group", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("inventoryType", "Inventory Type", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("status", "Status", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("date", "Date", ColumnDataType.Date),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("purchaseDescription", "Purchase Description", ColumnDataType.Text),
                new("salesDescription", "Sales Description", ColumnDataType.Text),
                new("purchaseTaxRate", "Purchase Tax Rate", ColumnDataType.Text),
                new("salesTaxRate", "Sales Tax Rate", ColumnDataType.Text),
                new("inventoryAccount", "Inventory Account", ColumnDataType.Text),
                new("purchaseAccount", "Purchase Account", ColumnDataType.Text),
                new("salesAccount", "Sales Account", ColumnDataType.Text),
                new("quantityOnHand", "Quantity On Hand", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("averageCost", "Average Cost", ColumnDataType.Money, IsDefault: true, Alignment: ColumnAlignment.Right),
                new("unitCostPrice", "Unit Cost Price", ColumnDataType.Money, IsDefault: true, Alignment: ColumnAlignment.Right),
                new("unitSalePrice", "Unit Sale Price", ColumnDataType.Money, IsDefault: true, Alignment: ColumnAlignment.Right),
                new("totalValue", "Total Value", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityOnOrder", "Quantity On Order", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityReceived", "Quantity Received", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("committedQuotes", "Committed Quotes", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("committedToDO", "Committed to DO", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("itemId", "Item", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "inventory-item-detail",
            Title = "Inventory Item Detail",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Stock movements per item with cost, value, and margin.",
            SortOrder = 60,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemGroup", "Item Group", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("contactCode", "Contact Code", ColumnDataType.Text),
                new("contactName", "Contact Name", ColumnDataType.Text, IsDefault: true),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text),
                new("source", "Source", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("qohMovement", "QoH Movement", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("valueMovement", "Value Movement", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unitCostPrice", "Unit Cost Price", ColumnDataType.Money, Alignment: ColumnAlignment.Right),
                new("unitSalePrice", "Unit Sale Price", ColumnDataType.Money, Alignment: ColumnAlignment.Right),
                new("margin", "Margin", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("profitPerItem", "Profit Per Item", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("inventoryAccount", "Inventory Account", ColumnDataType.Text),
                new("purchaseAccount", "Purchase Account", ColumnDataType.Text),
                new("salesAccount", "Sales Account", ColumnDataType.Text),
                new("adjustmentAccount", "Adjustment Account", ColumnDataType.Text),
                new("stockMovementId", "Stock Movement", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "inventory-item-summary",
            Title = "Inventory Item Summary",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Opening, purchased, sold, adjusted, and closing per item.",
            SortOrder = 65,
            Columns =
            [
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemGroup", "Item Group", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("inventoryType", "Inventory Type", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("openingQuantity", "Opening Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("openingBalance", "Opening Balance", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityPurchased", "Quantity Purchased", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("purchases", "Purchases", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantitySold", "Quantity Sold", ColumnDataType.Quantity, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("sales", "Sales", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityAdjusted", "Quantity Adjusted", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("adjustments", "Adjustments", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("cogs", "COGS", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("profit", "Profit", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("closingQuantity", "Closing Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("closingBalance", "Closing Balance", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("inventoryAccount", "Inventory Account", ColumnDataType.Text),
                new("purchaseAccount", "Purchase Account", ColumnDataType.Text),
                new("salesAccount", "Sales Account", ColumnDataType.Text),
                new("itemId", "Item", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "batch-tracking-status",
            Title = "Batch Tracking Status Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Batch quantities, dates, and warehouse availability.",
            SortOrder = 70,
            Columns =
            [
                new("batchNo", "Batch No", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("description", "Description", ColumnDataType.Text),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("manufacturedDate", "Manufactured Date", ColumnDataType.Date, IsDefault: true),
                new("expiryDate", "Expiry Date", ColumnDataType.Date, IsDefault: true),
                new("batchQuantity", "Batch Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("availableQuantity", "Available Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("warehouse", "Warehouse", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemBatchId", "Item Batch", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "batch-tracking-detail",
            Title = "Batch Tracking Detail Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Batch movements with quantities in/out and period columns.",
            SortOrder = 75,
            Columns =
            [
                new("batchNo", "Batch No", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("description", "Description", ColumnDataType.Text),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("manufacturedDate", "Manufactured Date", ColumnDataType.Date, IsDefault: true),
                new("expiryDate", "Expiry Date", ColumnDataType.Date, IsDefault: true),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("warehouse", "Warehouse", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("transactionDate", "Transaction Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true),
                new("transactionType", "Transaction Type", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("contactName", "Contact Name", ColumnDataType.Text),
                new("quantityIn", "Quantity IN", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityOut", "Quantity OUT", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("stockMovementId", "Stock Movement", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "serial-tracking-status",
            Title = "Serial Tracking Status Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Serial numbers with availability, dates, and warehouse.",
            SortOrder = 80,
            Columns =
            [
                new("serialNo", "Serial No", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("manufacturedDate", "Manufactured Date", ColumnDataType.Date, IsDefault: true),
                new("expiryDate", "Expiry Date", ColumnDataType.Date, IsDefault: true),
                new("availableQuantity", "Available Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("warehouse", "Warehouse", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemSerialId", "Item Serial", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "serial-tracking-detail",
            Title = "Serial Tracking Detail Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Serial movements with quantities in/out and warehouse.",
            SortOrder = 85,
            Columns =
            [
                new("serialNo", "Serial No", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("productCategory", "Product Category", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("manufacturedDate", "Manufactured Date", ColumnDataType.Date, IsDefault: true),
                new("expiryDate", "Expiry Date", ColumnDataType.Date, IsDefault: true),
                new("costingMethod", "Costing Method", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("warehouse", "Warehouse", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("transactionDate", "Transaction Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true),
                new("transactionType", "Transaction Type", ColumnDataType.Enum, IsDefault: true, IsGroupable: true),
                new("contactName", "Contact Name", ColumnDataType.Text),
                new("quantityIn", "Quantity IN", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityOut", "Quantity OUT", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unitOfMeasurement", "Unit of Measurement", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("stockMovementId", "Stock Movement", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "warehouse-tracking-status",
            Title = "Warehouse Tracking Status Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Warehouse totals with quantities in, out, and available.",
            SortOrder = 90,
            Columns =
            [
                new("warehouseName", "Warehouse Name", ColumnDataType.Text, IsDefault: true, IsPrimary: true),
                new("address", "Address", ColumnDataType.Text, IsDefault: true),
                new("city", "City", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("state", "State", ColumnDataType.Number, IsDefault: true, IsGroupable: true),
                new("country", "Country", ColumnDataType.Number, IsDefault: true, IsGroupable: true),
                new("primary", "Primary", ColumnDataType.Enum, IsDefault: true),
                new("status", "Status", ColumnDataType.Enum, IsDefault: true),
                new("quantityIn", "Quantity IN", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityOut", "Quantity OUT", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("availableQuantity", "Available Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("warehouseId", "Warehouse", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "warehouse-tracking-detail",
            Title = "Warehouse Tracking Detail Report",
            Module = ReportModule.Inventory,
            RequiredPermission = "inventory.view",
            Description = "Warehouse movements with tracked/untracked split and running balance.",
            SortOrder = 95,
            Columns =
            [
                new("warehouseName", "Warehouse Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("itemCode", "Item Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("itemName", "Item Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("batchOrSerialNo", "Batch/Serial No", ColumnDataType.Text, IsDefault: true),
                new("transactionDate", "Transaction Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("quantityIn", "Quantity IN", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("quantityOut", "Quantity OUT", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("balanceQuantity", "Balance Quantity", ColumnDataType.Quantity, IsDefault: true, IsFilterable: false, Alignment: ColumnAlignment.Right),
                new("trackedQuantity", "Tracked Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unTrackedQuantity", "UnTracked Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("totalQuantity", "Total Quantity", ColumnDataType.Quantity, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("stockMovementId", "Stock Movement", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "fx-gain-loss",
            Title = "Foreign Currency Gain or Loss",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Realized and unrealized FX gain/loss per account and currency. Unrealized columns are null until period-end revaluation job exists.",
            SortOrder = 100,
            Columns =
            [
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true, IsPrimary: true),
                new("currencyCode", "Currency Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("dueSource", "Due(Source)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("due", "Due(%CurCode%)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("revalueFxRate", "Revalue FxRate", ColumnDataType.Rate, Alignment: ColumnAlignment.Right),
                new("revaluedDue", "Revalued Due(%CurCode%)", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedAmount", "Realized Amount", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedExposure", "Realized Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedYtd", "Realized YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedAmount", "Unrealized Amount", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedExposure", "Unrealized Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedYtd", "Unrealized YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netAmount", "Net Amount", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netExposure", "Net Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netYtd", "Net YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "fx-gain-loss-details",
            Title = "Foreign Currency Gain or Loss Details",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Detailed FX gain/loss per transaction with contact and reference. Unrealized columns are null until period-end revaluation job exists.",
            SortOrder = 105,
            Columns =
            [
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("currencyCode", "Currency Code", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("contactName", "Contact Name", ColumnDataType.Text),
                new("transactionDate", "Transaction Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true),
                new("transactionFxRate", "Transaction Fx Rate", ColumnDataType.Rate, Alignment: ColumnAlignment.Right),
                new("reference", "Reference", ColumnDataType.Text),
                new("source", "Source", ColumnDataType.Text, IsDefault: true, IsGroupable: true),
                new("dueSource", "Due(Source)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("due", "Due(%CurCode%)", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("revalueFxRate", "Revalue FxRate", ColumnDataType.Rate, Alignment: ColumnAlignment.Right),
                new("revaluedDue", "Revalued Due(%CurCode%)", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedAmount", "Realized Amount", ColumnDataType.Money, IsDefault: true, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedExposure", "Realized Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("realizedYtd", "Realized YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedAmount", "Unrealized Amount", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedExposure", "Unrealized Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("unrealizedYtd", "Unrealized YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netAmount", "Net Amount", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netExposure", "Net Exposure", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("netYtd", "Net YTD", ColumnDataType.Money, Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("ledgerId", "Ledger", ColumnDataType.Number, IsHidden: true),
            ],
        },
    ];

    private sealed class ReportSeed
    {
        public required string ReportKey { get; init; }

        public required string Title { get; init; }

        public required ReportModule Module { get; init; }

        public required string RequiredPermission { get; init; }

        public string? Description { get; init; }

        public int SortOrder { get; init; }

        public required IReadOnlyList<ColumnSeed> Columns { get; init; }
    }

    private sealed record ColumnSeed(
        string Key,
        string Header,
        ColumnDataType DataType,
        bool IsDefault = false,
        bool IsFilterable = true,
        bool IsGroupable = false,
        bool IsPivotable = false,
        bool IsPrimary = false,
        bool IsHidden = false,
        AggregateFunction Aggregate = AggregateFunction.None,
        ColumnAlignment Alignment = ColumnAlignment.Left);
}
