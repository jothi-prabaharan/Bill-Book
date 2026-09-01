import os

path = r'c:\Users\Praba\Source\repos\Bill-Book\backend\Api\Reporting\Reporting.Repository\ReportingDbContext.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

db_sets = '''    public DbSet<SalesOrderRead> SalesOrders => Set<SalesOrderRead>();
    public DbSet<SalesOrderDetailRead> SalesOrderDetails => Set<SalesOrderDetailRead>();
    public DbSet<QuoteRead> Quotes => Set<QuoteRead>();
    public DbSet<DeliveryChallanRead> DeliveryChallans => Set<DeliveryChallanRead>();
    public DbSet<InvoiceDetailRead> InvoiceDetails => Set<InvoiceDetailRead>();
    public DbSet<PurchaseOrderRead> PurchaseOrders => Set<PurchaseOrderRead>();
    public DbSet<GoodsReceiptRead> GoodsReceipts => Set<GoodsReceiptRead>();
    public DbSet<BillDetailRead> BillDetails => Set<BillDetailRead>();
'''

map_reads = '''        MapRead<SalesOrderRead>(modelBuilder, "SalesOrders", "sal", e => e.SalesOrderId);
        MapRead<SalesOrderDetailRead>(modelBuilder, "SalesOrderDetails", "sal", e => e.SalesOrderDetailId);
        MapRead<QuoteRead>(modelBuilder, "Quotes", "sal", e => e.QuoteId);
        MapRead<DeliveryChallanRead>(modelBuilder, "DeliveryChallans", "sal", e => e.DeliveryChallanId);
        MapRead<InvoiceDetailRead>(modelBuilder, "InvoiceDetails", "sal", e => e.InvoiceDetailId);
        MapRead<PurchaseOrderRead>(modelBuilder, "PurchaseOrders", "pur", e => e.PurchaseOrderId);
        MapRead<GoodsReceiptRead>(modelBuilder, "GoodsReceipts", "pur", e => e.GoodsReceiptId);
        MapRead<BillDetailRead>(modelBuilder, "BillDetails", "pur", e => e.BillDetailId);
'''

# insert db_sets before: protected override void OnModelCreating
target1 = "    protected override void OnModelCreating(ModelBuilder modelBuilder)"
if target1 in content:
    content = content.replace(target1, db_sets + "\n" + target1)

# insert map_reads before: } in ConfigureReadModels
# we know ConfigureReadModels ends with MapRead<InvoiceDetailTaxRead>...
target2 = 'MapRead<InvoiceDetailTaxRead>(modelBuilder, "InvoiceDetailTaxes", "sal", e => e.InvoiceDetailTaxId);'
if target2 in content:
    content = content.replace(target2, target2 + "\n" + map_reads)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)
