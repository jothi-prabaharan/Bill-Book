# Generating the `sal` migration

`sal` has no migration yet. Generate it, then paste the two blocks below into the
generated file — EF writes the tables, indexes and check constraints from the
model, but **row-level security is raw SQL and EF will not write it**. A
per-customer table without a policy is one `IgnoreQueryFilters()` away from
handing one branch another branch's documents.

```
cd backend/Api/Sales/Sales.Repository
dotnet ef migrations add AddSalesDocuments --startup-project ../Sales.Api
```

**It must produce fifteen tables and nothing else.** If it also wants to create
`NumberingSeries`, the `ExcludeFromMigrations` call in `SalesDbContext` did not
take: Accounting owns that table and migrates it, and a second `CREATE TABLE`
would fail on any database that already has one.

## Into `Up()`, at the end

```csharp
// Row-level security, as on every other per-customer table. The EF query
// filter is the first line of defence, not the last: it is a property of the
// code, and one query written with IgnoreQueryFilters would read another
// branch's documents.
foreach (string table in new[]
{
    "Quotes", "QuoteDetails", "QuoteDetailTaxes",
    "SalesOrders", "SalesOrderDetails", "SalesOrderDetailTaxes",
    "DeliveryChallans", "DeliveryChallanDetails", "DeliveryChallanDetailTaxes",
    "Invoices", "InvoiceDetails", "InvoiceDetailTaxes",
    "CreditNotes", "CreditNoteDetails", "CreditNoteDetailTaxes",
})
{
    migrationBuilder.Sql($"ALTER TABLE sal.\"{table}\" ENABLE ROW LEVEL SECURITY;");
    migrationBuilder.Sql(
        $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\";");
    migrationBuilder.Sql(
        $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\" " +
        "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
}
```

## Into `Down()`, at the start

```csharp
foreach (string table in new[]
{
    "CreditNoteDetailTaxes", "CreditNoteDetails", "CreditNotes",
    "InvoiceDetailTaxes", "InvoiceDetails", "Invoices",
    "DeliveryChallanDetailTaxes", "DeliveryChallanDetails", "DeliveryChallans",
    "SalesOrderDetailTaxes", "SalesOrderDetails", "SalesOrders",
    "QuoteDetailTaxes", "QuoteDetails", "Quotes",
})
{
    migrationBuilder.Sql(
        $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\";");
}
```

## Then check

Run `dotnet ef migrations add Probe --startup-project ../Sales.Api` a second time.
It must produce an **empty** migration — that is T2.2's *Done when*, and it is
what says the model and the schema agree. Delete the probe afterwards.

And in `psql`, against a provisioned customer database:

```sql
SELECT tablename, rowsecurity FROM pg_tables WHERE schemaname = 'sal';
SELECT tablename, policyname FROM pg_policies WHERE schemaname = 'sal';
```

All fifteen must come back with `rowsecurity = t` and one policy each. The model
having a query filter is not the same fact and does not imply this one.

## Two things worth eyeballing in the generated file

- **`chk_*_base_quantity`** compares `BaseQuantity = Quantity * ConversionFactor`
  across `decimal(18,6)` columns. Postgres does exact decimal arithmetic, so this
  is safe — but if a seeded or migrated row ever trips it, the fix is the writer,
  not the constraint.
- **`chk_invoices_due_date`** requires a due date on every `INV`. If invoices are
  ever imported without payment terms, that is the constraint that will stop them,
  and it should.

---

Delete this file once the migration is in and T2.2 is ticked in `SALES.md`.
