# Handoff Report — API Specification Miner

**Date:** 2026-08-19  
**Agent:** `spec_miner_api_1`  
**Target:** Parent Orchestrator (`81ce1b4e-8b82-482d-87dd-d3c3263fc136` / `cc978969-df66-403f-b02a-6feb6cefd6fe`)  
**Output Document:** `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1\analysis.md`

---

## 1. Observation

1. **Gateway Configuration & Routing Architecture (`backend/Gateway/Gateway.Api/appsettings.json`, lines 25–242):**
   - Public gateway reverse-proxies requests across 7 backend microservice clusters:
     - `master`: `/api/auth/`, `/api/users/`, `/api/roles/`, `/api/customers/`, `/api/master/`, `/api/smtp-settings/`, `/api/organizations/`, `/api/contacts/`, `/api/contact-person-roles/`
     - `sales`: `/api/sales/` (quotes, sales-orders, invoices, delivery-challans, credit-notes, transactions)
     - `purchase`: `/api/purchase/` (bills, purchase-orders, goods-receipts, debit-notes, transactions)
     - `inventory`: `/api/items/`, `/api/item-categories/`, `/api/uom-types/`, `/api/metal-purities/`, `/api/warehouses/`, `/api/stock/`, `/api/stock-adjustments/`
     - `accounting`: `/api/accounts/`, `/api/sub-accounts/`, `/api/tax-masters/`, `/api/payment-terms/`, `/api/numbering-series/`, `/api/journals/`, `/api/ledger/`, `/api/opening-balance/`, `/api/period-locks/`, `/api/banks/`, `/api/bank-accounts/`, `/api/spend-money/`, `/api/receive-money/`, `/api/transfer-money/`, `/api/statements/`
     - `reporting`: `/api/reports/`, `/api/reporting/`, `/api/statements/` (P&L, Balance Sheet, Inventory Valuation)
2. **Accounting UI Terminology Rule (`AGENTS.md` and codebase controllers):**
   - The UI label for the accounting module is strictly **"Accounts"** (the word "Accounting" must never be displayed to users in navigation or headers).
3. **Core Base Schemas (`backend/Shared/Shared.Kernel/Documents/`):**
   - `DocumentHeaderBase.cs` (lines 25–185) and `DocumentLineBase.cs` (lines 17–147) govern financial calculation rules. Totals (`SubTotal`, `TaxableAmount`, `CgstAmount`, `SgstAmount`, `IgstAmount`, `CessAmount`, `RoundOffAmount`, `TotalAmount`) are computed strictly server-side by `GstCalculator`. Client requests provide line-level `quantity`, `unitPrice`, `discountPercent`/`discountAmount`, `isPriceInclusive`, `taxGroupId`, and `taxTreatment`.
4. **Sales & Purchase DTO Mappings (`Sales.Entity/Models` and `Purchase.Entity/Models`):**
   - Quotes (`QuoteModels.cs`): `SaveQuoteRequest` requires `validUntil`, `documentDate`, `contactId`, and `lines: SaveQuoteLineRequest[]`.
   - Sales Orders (`SalesOrderModels.cs`): `SaveSalesOrderRequest` includes optional `quoteId`, `deliveryDate`.
   - Invoices (`InvoiceModels.cs`): `SaveInvoiceRequest` supports retail till fields (`tillId`, `cashierUserId`, `tenderedAmount`, `changeAmount`) and trade links (`quoteId`, `salesOrderId`, `deliveryChallanId`).
   - Bills (`BillModels.cs`): `SaveBillRequest` requires `vendorBillNo` and `vendorBillDate`.
   - Goods Receipts (`GoodsReceiptModels.cs`): `SaveGoodsReceiptRequest` handles delivered vs accepted vs rejected quantities (`acceptedQuantity`, `rejectedQuantity`, `rejectionReason`).
5. **Inventory DTOs (`Inventory.Entity/Models/ItemModels.cs`):**
   - `SaveItemRequest` manages Standard, Jewellery, and Pharma profiles with conditional sub-objects (`jewellery`, `pharma`, `barcodes`).
6. **Master, Auth & Topbar Dropdowns (`Master.Entity/Models/` and `Master.Api/Controllers/`):**
   - Authentication flow: 2-step login (`/api/auth/login` -> `/api/auth/select-organization`) yielding JWT with `org_id` and `customer_id`.
   - Topbar organization switcher: Populated via `GET /api/auth/organizations` or `GET /api/organizations`, switched via `POST /api/auth/switch-organization`.
   - Financial year display: Derived from current org's `financialYearStartMonth` (from `GET /api/organizations/current`).
7. **Frontend Model Discrepancies:**
   - Existing frontend models (e.g. `frontend/libs/sales/sales-core/src/lib/quote.service.ts`) used obsolete property names (e.g., `taxMasterId` instead of `taxGroupId`) and lacked required fields (`conversionFactor`, `warehouseId`, `isPriceInclusive`, `discountAmount`, `taxTreatment`, `lineType`, `accountId`).

---

## 2. Logic Chain

1. Starting from the YARP gateway reverse-proxy configuration (`appsettings.json`), all publicly accessible route patterns and their target downstream services were identified.
2. By reading each C# controller and its referenced DTO models in `Master.Entity`, `Sales.Entity`, `Purchase.Entity`, `Inventory.Entity`, `Accounting.Entity`, and `Reporting.Entity`, exact endpoint paths, HTTP verbs, query parameters, request bodies, response models, and status codes were extracted.
3. Comparing the extracted backend DTOs against the legacy Angular TypeScript models in `frontend/libs/` revealed several missing fields and misnamed parameters in client services.
4. Documenting these exact schemas in `analysis.md` provides an unambiguous, authoritative specification for frontend agents to build reactive forms and typed HTTP services without guesswork.

---

## 3. Caveats

- GSTR-2 inward supplies endpoint (`/api/reports/gst/gstr2`) is currently a stub returning `501 Not Implemented` pending Purchase Register integration.
- PDF export on reports (`/api/reports/{reportKey}/export?format=pdf`) intentionally returns `400 Bad Request` per architectural decision in `Reporting.md` §5.8 (Excel format `.xlsx` is fully functional).

---

## 4. Conclusion

The full API specification across all 7 microservices in RetailErp (Bill-Book) is completely mined, verified, and documented in `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1\analysis.md`. All required modules (Sales, Purchases, Inventory, Accounts, Contacts, Settings/Organizations, Financial Years, Reporting) are covered in exhaustive detail with exact TypeScript models and form field mapping tables.

---

## 5. Verification Method

To independently verify the contracts and documentation:
1. **Inspect Specification Document:** Review `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1\analysis.md`.
2. **Inspect Backend Controllers & Models:**
   - Sales: `backend/Api/Sales/Sales.Api/Controllers/` & `backend/Api/Sales/Sales.Entity/Models/`
   - Purchases: `backend/Api/Purchase/Purchase.Api/Controllers/` & `backend/Api/Purchase/Purchase.Entity/Models/`
   - Inventory: `backend/Api/Inventory/Inventory.Api/Controllers/` & `backend/Api/Inventory/Inventory.Entity/Models/`
   - Accounts: `backend/Api/Accounting/Accounting.Api/Controllers/` & `backend/Api/Accounting/Accounting.Entity/Models/`
   - Master/Contacts/Auth: `backend/Api/Master/Master.Api/Controllers/` & `backend/Api/Master/Master.Entity/Models/`
   - Reporting: `backend/Api/Reporting/Reporting.Api/Controllers/` & `backend/Api/Reporting/Reporting.Entity/Models/`
3. **Gateway Routing Table:** Inspect `backend/Gateway/Gateway.Api/appsettings.json`.
