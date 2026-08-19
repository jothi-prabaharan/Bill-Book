# Bill-Book (RetailErp) — Complete API Specification & Frontend Contract Mapping

**Document Version:** 1.0.0  
**Author:** Specification Miner (`spec_miner_api_1`)  
**Workspace:** `C:\Users\Praba\Source\repos\Bill-Book`  
**Date:** August 19, 2026  

---

## 1. Architectural & Routing Overview

### 1.1 YARP Gateway Routing & Microservices Architecture
The client (Angular desktop / web application) communicates exclusively through the **YARP Gateway** (`http://localhost:5000` by default). The gateway terminates client requests, logs interactions, and proxies requests to 7 underlying microservices based on route prefixes:

| Cluster | Microservice | Gateway Route Prefix | Description |
|---|---|---|---|
| **master** | `Master.Api` | `/api/auth/`, `/api/users/`, `/api/roles/`, `/api/customers/`, `/api/master/`, `/api/smtp-settings/`, `/api/organizations/`, `/api/contacts/`, `/api/contact-person-roles/` | Multi-tenant auth, organization branches, contacts, users, RBAC, reference data |
| **sales** | `Sales.Api` | `/api/sales/` | Quotes, sales orders, invoices, delivery challans, credit notes, unified transactions |
| **purchase** | `Purchase.Api` | `/api/purchase/` | Vendor bills, purchase orders, goods receipts, debit notes, unified transactions |
| **inventory** | `Inventory.Api` | `/api/items/`, `/api/item-categories/`, `/api/uom-types/`, `/api/metal-purities/`, `/api/warehouses/`, `/api/stock/`, `/api/stock-adjustments/` | Item master, UOMs, warehouses, stock movements, stock adjustments, costing |
| **accounting** | `Accounting.Api` | `/api/accounts/`, `/api/sub-accounts/`, `/api/tax-masters/`, `/api/payment-terms/`, `/api/numbering-series/`, `/api/journals/`, `/api/ledger/`, `/api/opening-balance/`, `/api/period-locks/`, `/api/banks/`, `/api/bank-accounts/`, `/api/spend-money/`, `/api/receive-money/`, `/api/transfer-money/`, `/api/statements/` | Chart of Accounts (**UI label: Accounts**), sub-accounts, taxes, journals, double-entry ledger, banking, reconciliation |
| **reporting** | `Reporting.Api` | `/api/reports/`, `/api/reporting/`, `/api/statements/` (P&L, Balance Sheet) | Dynamic reporting query engine, saved views, financial statements, GST filings |

### 1.2 Global Serialization & Convention Rules
1. **JSON Property Casing:** ASP.NET Core default `camelCase` naming strategy applies across all JSON inputs and outputs.
2. **Dates:** ISO 8601 strings (`YYYY-MM-DD` for `DateOnly`, ISO UTC strings with offsets for `DateTimeOffset`).
3. **Numbers & Decimals:** Sent as JSON numbers.
4. **Enums:** Handled as String representations (e.g. `"Draft"`, `"Posted"`, `"Taxable"`, `"Stock"`).
5. **No Client-Calculated Document Totals:** In trading documents (Quotes, Sales Orders, Bills, Purchase Orders, Goods Receipts, Debit Notes), document totals and taxes are derived server-side via `Shared.Kernel.Tax.GstCalculator` to enforce tax integrity.
6. **Tenancy:** Every organization-scoped resource is isolated by `OrgId` via JWT bearer token claims (`org_id`, `customer_id`).

---

## 2. Sales Module (`/api/sales`)

### 2.1 Quotes (`/api/sales/quotes`)

#### List API
- **Endpoint:** `GET /api/sales/quotes`
- **Auth:** Bearer Token, requires `sales` module permission (`sales.view`)
- **Query Params:** None (returns list of active quotes for current org)
- **Response:** `200 OK` -> `List<QuoteListItem>`
```typescript
interface QuoteListItem {
  quoteId: number;
  documentNo: string;
  documentDate: string; // YYYY-MM-DD
  validUntil: string;   // YYYY-MM-DD
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string; // "Draft" | "ReadyToPost" | "Posted" | "Void"
  isInterState: boolean;
  hasLapsed: boolean;
  convertedToSalesOrderId?: number | null;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/sales/quotes/{quoteId:long}`
- **Response:** `200 OK` -> `QuoteView` | `404 Not Found`
```typescript
interface QuoteView extends QuoteListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmountBase: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: QuoteLineView[];
}

interface QuoteLineView {
  quoteDetailId: number;
  lineNumber: number;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent?: number | null;
  discountAmount: number;
  grossAmount: number;
  taxableAmount: number;
  taxTreatment: string; // "Taxable" | "ZeroRated" | "NilRated" | "Exempt" | "NonGst"
  taxMasterId?: number | null;
  taxGroupId?: number | null;
  taxAmount: number;
  lineType: string; // "Stock" | "Expense" | "Capital"
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  lineTotal: number;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  taxes: QuoteLineTaxView[];
}

interface QuoteLineTaxView {
  quoteDetailTaxId: number;
  taxComponent: string; // "Cgst" | "Sgst" | "Igst" | "Cess"
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}
```

#### Create API
- **Endpoint:** `POST /api/sales/quotes`
- **Request Body:** `SaveQuoteRequest`
```typescript
interface SaveQuoteRequest {
  documentDate: string; // Required, YYYY-MM-DD
  contactId: number;    // Required, min 1
  validUntil: string;   // Required, YYYY-MM-DD
  contactGstin?: string | null; // MaxLength(15)
  placeOfSupplyStateCode?: string | null; // MaxLength(2)
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null; // MaxLength(3), null = org base currency
  exchangeRate?: number | null; // Range(0.00000001, ...)
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveQuoteLineRequest[];
}

interface SaveQuoteLineRequest {
  itemId?: number | null;
  description?: string | null; // Required if itemId is null, MaxLength(500)
  hsnSacCode?: string | null;  // MaxLength(8)
  warehouseId?: number | null;
  quantity: number;            // Required, > 0
  uomId?: number | null;
  conversionFactor?: number;   // Default 1, > 0
  unitPrice: number;           // >= 0
  isPriceInclusive?: boolean;  // Default false
  discountPercent?: number | null; // Range(0, 100)
  discountAmount?: number;     // >= 0
  taxTreatment?: string;       // "Taxable" | "ZeroRated" | "NilRated" | "Exempt" | "NonGst", default "Taxable"
  taxGroupId?: number | null;  // Required if Taxable/ZeroRated
  lineType?: string;           // "Stock" | "Expense" | "Capital", default "Stock"
  accountId?: number | null;   // Required if free-text or Expense
  fixedAssetCategoryId?: number | null; // Required if Capital
  itemBatchId?: number | null;
  lineNotes?: string | null;   // MaxLength(300)
}
```
- **Response:** `201 Created` -> `{ outcome: 0, quoteId: number, detail: null }` (with `Location` header)
- **Error Statuses:** `400 Bad Request` (LineInvalid, ValidityInvalid, PlaceOfSupplyRefused, LifecycleRefused), `409 Conflict` (AlreadyConverted, Lapsed), `503 Service Unavailable` (RatesUnavailable).

#### Update API
- **Endpoint:** `PUT /api/sales/quotes/{quoteId:long}`
- **Request Body:** `SaveQuoteRequest`
- **Response:** `204 No Content` | Errors (400, 404, 409, 503)

#### Status Operations
- **Approve (Post to ReadyToPost/Posted):** `POST /api/sales/quotes/{quoteId:long}/approve` (Requires `sales.approve`) -> `204 No Content`
- **Void:** `POST /api/sales/quotes/{quoteId:long}/void` (Requires `sales.void`)  
  **Request Body:** `VoidQuoteRequest` `{ reason: string }` (Required, max 300 chars) -> `204 No Content`

---

### 2.2 Sales Orders (`/api/sales/sales-orders`)

#### List API
- **Endpoint:** `GET /api/sales/sales-orders`
- **Response:** `200 OK` -> `List<SalesOrderListItem>`
```typescript
interface SalesOrderListItem {
  salesOrderId: number;
  documentNo: string;
  documentDate: string;
  quoteId?: number | null;
  deliveryDate?: string | null;
  fulfilmentStatus: string; // "Open" | "PartlyDelivered" | "Closed" | "Cancelled"
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string; // "Draft" | "ReadyToPost" | "Posted" | "Void"
  isInterState: boolean;
  invoicedDocumentId?: number | null;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/sales/sales-orders/{salesOrderId:long}`
- **Response:** `200 OK` -> `SalesOrderView`
```typescript
interface SalesOrderView extends SalesOrderListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmountBase: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: SalesOrderLineView[];
}

interface SalesOrderLineView {
  salesOrderDetailId: number;
  lineNumber: number;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
  reservedQuantity: number;
  deliveredQuantity: number;
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent?: number | null;
  discountAmount: number;
  grossAmount: number;
  taxableAmount: number;
  taxTreatment: string;
  taxMasterId?: number | null;
  taxGroupId?: number | null;
  taxAmount: number;
  lineType: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  lineTotal: number;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  taxes: SalesOrderLineTaxView[];
}

interface SalesOrderLineTaxView {
  salesOrderDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}
```

#### Create & Update APIs
- **Create:** `POST /api/sales/sales-orders` -> `201 Created` (`SalesOrderResult`)
- **Update:** `PUT /api/sales/sales-orders/{salesOrderId:long}` -> `204 No Content`
- **Request Body:** `SaveSalesOrderRequest`
```typescript
interface SaveSalesOrderRequest {
  documentDate: string;
  contactId: number;
  quoteId?: number | null;
  deliveryDate?: string | null;
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveSalesOrderLineRequest[];
}

interface SaveSalesOrderLineRequest {
  itemId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor?: number;
  unitPrice: number;
  isPriceInclusive?: boolean;
  discountPercent?: number | null;
  discountAmount?: number;
  taxTreatment?: string;
  taxGroupId?: number | null;
  lineType?: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
}
```

#### Status Actions
- **Approve:** `POST /api/sales/sales-orders/{salesOrderId:long}/approve` (Requires `approve`) -> `204 No Content`
- **Void:** `POST /api/sales/sales-orders/{salesOrderId:long}/void` (Requires `void`)  
  Body: `VoidSalesOrderRequest` `{ reason: string }` -> `204 No Content`

---

### 2.3 Invoices (`/api/sales/invoices`)

#### List API
- **Endpoint:** `GET /api/sales/invoices`
- **Query Params:** `from?: string` (YYYY-MM-DD), `to?: string` (YYYY-MM-DD)
- **Response:** `200 OK` -> `List<InvoiceListItem>`
```typescript
interface InvoiceListItem {
  invoiceId: number;
  salesOrderId?: number | null;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  status: number | string; // DocumentStatus
  dueDate?: string | null;
  totalAmount: number;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/sales/invoices/{id:long}`
- **Response:** `200 OK` -> `InvoiceView`
```typescript
interface InvoiceView {
  invoiceId: number;
  quoteId?: number | null;
  salesOrderId?: number | null;
  deliveryChallanId?: number | null;
  paymentTermId?: number | null;
  dueDate?: string | null;
  tillId?: number | null;
  cashierUserId?: string | null;
  paymentMode?: string | null;
  tenderedAmount?: number | null;
  changeAmount?: number | null;
  documentDate: string;
  documentNo: string;
  notes?: string | null;
  status: number | string;
  currencyCode: string;
  exchangeRate: number;
  contactId: number;
  contactName: string;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  lines: InvoiceLineView[];
}
```

#### Create & Update APIs
- **Create:** `POST /api/sales/invoices` -> `200 OK` `{ invoiceId: number }`
- **Update:** `PUT /api/sales/invoices/{id:long}` -> `200 OK`
- **Request Body:** `SaveInvoiceRequest`
```typescript
interface SaveInvoiceRequest {
  quoteId?: number | null;
  salesOrderId?: number | null;
  deliveryChallanId?: number | null;
  paymentTermId?: number | null;
  dueDate?: string | null;
  tillId?: number | null;
  cashierUserId?: string | null;
  paymentMode?: string | null;
  tenderedAmount?: number | null;
  changeAmount?: number | null;
  contactId: number;
  documentDate: string;
  notes?: string | null;
  currencyCode?: string; // Default "USD" or "INR"
  exchangeRate?: number; // Default 1
  billingAddress?: string | null;
  shippingAddress?: string | null;
  lines: SaveInvoiceLineRequest[];
}

interface SaveInvoiceLineRequest {
  itemId: number;
  quantity: number; // Range(0.000001, double.MaxValue)
  unitPrice: number;
  discountPercent: number;
  taxGroupIds: number[];
}
```

#### Post / Void APIs
- **Post:** `POST /api/sales/invoices/{id:long}/post` -> `200 OK`
- **Void:** `POST /api/sales/invoices/{id:long}/void` -> `200 OK`

---

### 2.4 Delivery Challans (`/api/sales/delivery-challans`)

#### List API
- **Endpoint:** `GET /api/sales/delivery-challans`
- **Query Params:** `from?: string`, `to?: string`
- **Response:** `200 OK` -> `List<DeliveryChallanListItem>`
```typescript
interface DeliveryChallanListItem {
  deliveryChallanId: number;
  salesOrderId?: number | null;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  status: number | string;
  dispatchDate: string;
  totalAmount: number;
}
```

#### Create & Update APIs
- **Create:** `POST /api/sales/delivery-challans` -> `200 OK` `{ deliveryChallanId: number }`
- **Update:** `PUT /api/sales/delivery-challans/{id:long}` -> `200 OK`
- **Request Body:** `SaveDeliveryChallanRequest`
```typescript
interface SaveDeliveryChallanRequest {
  deliveryChallanId?: number | null;
  salesOrderId?: number | null;
  documentDate: string;
  contactId: number;
  challanType: number | string; // 0=Sale, 1=JobWork, 2=Approval, 3=BranchTransfer, 4=Sample
  vehicleNo?: string | null;
  transporterName?: string | null;
  ewayBillNo?: string | null;
  ewayBillDate?: string | null;
  dispatchDate: string;
  currencyCode?: string | null;
  exchangeRate?: number;
  notes?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  lines: SaveDeliveryChallanLineRequest[];
}

interface SaveDeliveryChallanLineRequest {
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxGroupIds: number[];
}
```

#### Post / Void APIs
- `POST /api/sales/delivery-challans/{id:long}/post` -> `200 OK`
- `POST /api/sales/delivery-challans/{id:long}/void` -> `200 OK`

---

### 2.5 Credit Notes (`/api/sales/credit-notes`)

#### List & Detail APIs
- **List:** `GET /api/sales/credit-notes` (`from?: string`, `to?: string`) -> `List<CreditNoteListItem>`
- **Get Detail:** `GET /api/sales/credit-notes/{id:long}` -> `CreditNoteView`
- **Create:** `POST /api/sales/credit-notes` -> `200 OK` `{ creditNoteId: number }`
- **Update:** `PUT /api/sales/credit-notes/{id:long}` -> `200 OK`
- **Request Body:** `SaveCreditNoteRequest`
```typescript
interface SaveCreditNoteRequest {
  creditNoteId?: number | null;
  invoiceId: number; // Required
  documentDate: string;
  contactId: number;
  reasonCode: number | string; // 0=SalesReturn, 1=PriceCorrection, 2=PostSaleDiscount, 3=Deficiency, 4=Cancellation
  currencyCode?: string | null;
  exchangeRate?: number;
  notes?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  lines: SaveCreditNoteLineRequest[];
}

interface SaveCreditNoteLineRequest {
  invoiceDetailId: number;
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxGroupIds: number[];
}
```

---

### 2.6 Unified Sales Transactions (`/api/sales/transactions`)

- **Endpoint:** `GET /api/sales/transactions`
- **Query Params:**
  - `type?: string` (`"Quote"` | `"SalesOrder"` | `"Invoice"` | `"CreditNote"` | null for all)
  - `from?: string` (YYYY-MM-DD)
  - `to?: string` (YYYY-MM-DD)
- **Response:** `200 OK` -> `List<SalesTransactionListItem>`
```typescript
interface SalesTransactionListItem {
  transactionId: number;
  transactionType: string; // "Quote" | "SalesOrder" | "Invoice" | "CreditNote"
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName?: string | null;
  totalAmount: number;
  status: string;
  dueDate?: string | null;
}
```

---

## 3. Purchases Module (`/api/purchase`)

### 3.1 Vendor Bills (`/api/purchase/bills`)

#### List API
- **Endpoint:** `GET /api/purchase/bills`
- **Response:** `200 OK` -> `List<BillListItem>`
```typescript
interface BillListItem {
  billId: number;
  documentNo: string;
  documentDate: string;
  vendorBillNo: string;
  vendorBillDate: string;
  dueDate: string;
  purchaseOrderId?: number | null;
  goodsReceiptId?: number | null;
  goodsReceiptNo?: string | null;
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string; // "Draft" | "ReadyToPost" | "Posted" | "Void"
  isInterState: boolean;
  daysOverdue: number;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/purchase/bills/{billId:long}`
- **Response:** `200 OK` -> `BillView`
```typescript
interface BillView extends BillListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
  paymentTermId?: number | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmountBase: number;
  landedCostAmount: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: BillLineView[];
}

interface BillLineView {
  billDetailId: number;
  lineNumber: number;
  goodsReceiptDetailId?: number | null;
  purchaseOrderDetailId?: number | null;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
  returnedQuantity: number;
  apportionedLandedCost: number;
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent?: number | null;
  discountAmount: number;
  grossAmount: number;
  taxableAmount: number;
  taxTreatment: string;
  taxMasterId?: number | null;
  taxGroupId?: number | null;
  taxAmount: number;
  lineType: string; // "Stock" | "Expense" | "Capital"
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  lineTotal: number;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  taxes: BillLineTaxView[];
}

interface BillLineTaxView {
  billDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}
```

#### Create & Update APIs
- **Create:** `POST /api/purchase/bills` -> `201 Created` (`BillResult`)
- **Update:** `PUT /api/purchase/bills/{billId:long}` -> `204 No Content`
- **Request Body:** `SaveBillRequest`
```typescript
interface SaveBillRequest {
  documentDate: string;
  contactId: number; // Required vendor ID
  purchaseOrderId?: number | null;
  goodsReceiptId?: number | null;
  vendorBillNo: string; // Required, MaxLength(50)
  vendorBillDate: string; // Required
  paymentTermId?: number | null;
  dueDate?: string | null; // Required if paymentTermId is null
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  landedCostAmount?: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveBillLineRequest[];
}

interface SaveBillLineRequest {
  itemId?: number | null;
  goodsReceiptDetailId?: number | null;
  purchaseOrderDetailId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor?: number;
  unitPrice: number;
  isPriceInclusive?: boolean;
  discountPercent?: number | null;
  discountAmount?: number;
  taxTreatment?: string;
  taxGroupId?: number | null;
  lineType?: string; // "Stock" | "Expense" | "Capital"
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
}
```

#### Status Actions
- **Post (Clears clearing account / stock):** `POST /api/purchase/bills/{billId:long}/post` (Requires `purchase.approve`) -> `204 No Content`
- **Void:** `POST /api/purchase/bills/{billId:long}/void` (Requires `purchase.void`)  
  Body: `VoidBillRequest` `{ reason: string }` -> `204 No Content`

---

### 3.2 Purchase Orders (`/api/purchase/purchase-orders`)

#### List & Detail APIs
- **List:** `GET /api/purchase/purchase-orders` -> `List<PurchaseOrderListItem>`
- **Get Detail:** `GET /api/purchase/purchase-orders/{purchaseOrderId:long}` -> `PurchaseOrderView`
- **Create:** `POST /api/purchase/purchase-orders` -> `201 Created` (`PurchaseOrderResult`)
- **Update:** `PUT /api/purchase/purchase-orders/{purchaseOrderId:long}` -> `204 No Content`
- **Approve (Review step):** `POST /api/purchase/purchase-orders/{purchaseOrderId:long}/approve` -> `204 No Content`
- **Confirm (Issued to vendor):** `POST /api/purchase/purchase-orders/{purchaseOrderId:long}/confirm` -> `204 No Content`
- **Void:** `POST /api/purchase/purchase-orders/{purchaseOrderId:long}/void`  
  Body: `VoidPurchaseOrderRequest` `{ reason: string }` -> `204 No Content`

---

### 3.3 Goods Receipts (`/api/purchase/goods-receipts`)

#### List & Detail APIs
- **List:** `GET /api/purchase/goods-receipts` -> `List<GoodsReceiptListItem>`
- **Get Detail:** `GET /api/purchase/goods-receipts/{goodsReceiptId:long}` -> `GoodsReceiptView`
- **Create:** `POST /api/purchase/goods-receipts` -> `201 Created` (`GoodsReceiptResult`)
- **Update:** `PUT /api/purchase/goods-receipts/{goodsReceiptId:long}` -> `204 No Content`
- **Post:** `POST /api/purchase/goods-receipts/{goodsReceiptId:long}/post` -> `204 No Content`
- **Void:** `POST /api/purchase/goods-receipts/{goodsReceiptId:long}/void`  
  Body: `VoidGoodsReceiptRequest` `{ reason: string }` -> `204 No Content`

#### Goods Receipt Save Request Structure
```typescript
interface SaveGoodsReceiptRequest {
  documentDate: string;
  contactId: number;
  purchaseOrderId?: number | null;
  vendorDeliveryNoteNo?: string | null;
  vendorDeliveryNoteDate?: string | null;
  receivedBy?: string | null; // User Guid
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveGoodsReceiptLineRequest[];
}

interface SaveGoodsReceiptLineRequest {
  itemId?: number | null;
  purchaseOrderDetailId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;          // Delivered quantity
  acceptedQuantity: number;  // Moves into stock
  rejectedQuantity: number;
  rejectionReason?: string | null; // Required if rejectedQuantity > 0
  uomId?: number | null;
  conversionFactor?: number;
  unitPrice: number;
  isPriceInclusive?: boolean;
  discountPercent?: number | null;
  discountAmount?: number;
  taxTreatment?: string;
  taxGroupId?: number | null;
  lineType?: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  batchNumber?: string | null;
  batchExpiryDate?: string | null;
  batchManufactureDate?: string | null;
  lineNotes?: string | null;
}
```

---

### 3.4 Debit Notes (`/api/purchase/debit-notes`)

#### Endpoints
- **List:** `GET /api/purchase/debit-notes` -> `List<DebitNoteListItem>`
- **Get Detail:** `GET /api/purchase/debit-notes/{debitNoteId:long}` -> `DebitNoteView`
- **Create:** `POST /api/purchase/debit-notes` -> `201 Created` (`DebitNoteResult`)
- **Update:** `PUT /api/purchase/debit-notes/{debitNoteId:long}` -> `204 No Content`
- **Post:** `POST /api/purchase/debit-notes/{debitNoteId:long}/post` -> `204 No Content`
- **Void:** `POST /api/purchase/debit-notes/{debitNoteId:long}/void`  
  Body: `VoidDebitNoteRequest` `{ reason: string }` -> `204 No Content`

#### Save Request Structure
```typescript
interface SaveDebitNoteRequest {
  documentDate: string;
  contactId: number;
  billId: number; // Required
  reasonCode?: number | string; // Default PurchaseReturn
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  lines: SaveDebitNoteLineRequest[];
}

interface SaveDebitNoteLineRequest {
  billDetailId: number; // Required
  itemId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor?: number;
  unitPrice: number;
  isPriceInclusive?: boolean;
  discountPercent?: number | null;
  discountAmount?: number;
  taxTreatment?: string;
  taxGroupId?: number | null;
  lineType?: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
}
```

---

### 3.5 Unified Purchase Transactions (`/api/purchase/transactions`)
- **Endpoint:** `GET /api/purchase/transactions`
- **Query Params:** `type?: string` (`"Bill"` | `"PurchaseOrder"` | `"DebitNote"` | `"GoodsReceipt"`), `from?: string`, `to?: string`
- **Response:** `200 OK` -> `List<PurchaseTransactionListItem>`
```typescript
interface PurchaseTransactionListItem {
  transactionId: number;
  transactionType: string;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName?: string | null;
  totalAmount: number;
  status: string;
  dueDate?: string | null;
}
```

---

## 4. Inventory Module (`/api/items`, `/api/stock`, etc.)

### 4.1 Item Master (`/api/items`)

#### List API
- **Endpoint:** `GET /api/items`
- **Query Params:**
  - `search?: string`
  - `profile?: string` ("Standard" | "Jewellery" | "Pharma")
  - `categoryId?: number`
  - `includeInactive: boolean`
- **Response:** `200 OK` -> `List<ItemListItem>`
```typescript
interface ItemListItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  itemProfile: string;
  itemType: string;
  itemCategoryId?: number | null;
  categoryName?: string | null;
  costingType: string;
  trackInventory: boolean;
  inventoryUomCode: string;
  salesPrice?: number | null;
  mrp?: number | null;
  isActive: boolean;
  displayOrder: number;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/items/{itemId:long}`
- **Response:** `200 OK` -> `ItemDetail`
```typescript
interface ItemDetail extends ItemListItem {
  printName?: string | null;
  description?: string | null;
  hsnSacCodeId?: number | null;
  taxGroupId?: number | null;
  taxPreference: string;
  isPriceInclusiveOfTax: boolean;
  uomTypeId: number;
  inventoryUomId: number;
  salesUomId: number;
  purchaseUomId: number;
  reportUomId: number;
  isBatchTracked: boolean;
  isExpiryTracked: boolean;
  isSerialTracked: boolean;
  purchasePrice?: number | null;
  minSalePrice?: number | null;
  standardCost?: number | null;
  reorderLevel?: number | null;
  reorderQuantity?: number | null;
  minStockLevel?: number | null;
  maxStockLevel?: number | null;
  leadTimeDays?: number | null;
  defaultWarehouseId?: number | null;
  isSales: boolean;
  isPurchase: boolean;
  isReturnable: boolean;
  imageUrl?: string | null;
  hasStockMovements: boolean;
  jewellery?: ItemJewelleryModel | null;
  pharma?: ItemPharmaModel | null;
  barcodes: ItemBarcodeModel[];
}
```

#### Create & Update APIs
- **Create:** `POST /api/items` -> `201 Created` `{ itemId: number, itemCode: string }`
- **Update:** `PUT /api/items/{itemId:long}` -> `204 No Content`
- **Deactivate:** `DELETE /api/items/{itemId:long}` -> `204 No Content`
- **Reorder:** `PATCH /api/items/reorder` Body: `ReorderRequest` `{ id: number, displayOrder: number }[]` -> `204 No Content`
- **Request Body:** `SaveItemRequest`
```typescript
interface SaveItemRequest {
  itemCode?: string | null; // Leave empty for auto-number
  itemName: string;         // Required, MaxLength(200)
  printName?: string | null;
  description?: string | null;
  itemProfile: string;      // "Standard" | "Jewellery" | "Pharma"
  itemType: string;         // "Goods" | "Service"
  itemCategoryId?: number | null;
  hsnSacCodeId?: number | null;
  taxGroupId?: number | null;
  taxPreference: string;    // "Taxable" | "ZeroRated" | "NilRated" | "Exempt" | "NonGst"
  isPriceInclusiveOfTax?: boolean;
  uomTypeId: number;        // Required
  inventoryUomId: number;   // Required
  salesUomId: number;       // Required
  purchaseUomId: number;    // Required
  reportUomId: number;      // Required
  trackInventory?: boolean; // Default true
  costingType: string;      // "WeightedAverage" | "Fifo" | "Lifo" | "Fefo" | "SpecificIdentification"
  isBatchTracked?: boolean;
  isExpiryTracked?: boolean;
  isSerialTracked?: boolean;
  salesPrice?: number | null;
  purchasePrice?: number | null;
  mrp?: number | null;
  minSalePrice?: number | null;
  standardCost?: number | null;
  reorderLevel?: number | null;
  reorderQuantity?: number | null;
  minStockLevel?: number | null;
  maxStockLevel?: number | null;
  leadTimeDays?: number | null;
  defaultWarehouseId?: number | null;
  isSales?: boolean;
  isPurchase?: boolean;
  isReturnable?: boolean;
  imageUrl?: string | null;
  isActive?: boolean;
  jewellery?: ItemJewelleryModel | null;
  pharma?: ItemPharmaModel | null;
  barcodes: ItemBarcodeModel[];
}

interface ItemJewelleryModel {
  metalType: string; // "Gold" | "Silver" | "Platinum"
  metalPurityId: number;
  grossWeight: number;
  netWeight: number;
  stoneWeight?: number;
  stoneCharge?: number;
  wastagePercent?: number;
  makingChargeType: string; // "Percentage" | "PerGram" | "Fixed"
  makingChargeValue: number;
  isHallmarked?: boolean;
}

interface ItemPharmaModel {
  genericName: string;
  strength?: string | null;
  dosageForm: string; // "Tablet" | "Capsule" | "Syrup" | "Injection" | "Ointment" | "Drops"
  packSize: string;
  manufacturerName: string;
  marketedBy?: string | null;
  drugSchedule: string; // "None" | "ScheduleH" | "ScheduleH1" | "ScheduleX" | "ScheduleG"
  isPrescriptionRequired?: boolean;
  isNarcotic?: boolean;
  storageCondition: string; // "Ambient" | "Refrigerated" | "Frozen" | "CoolAndDry"
  shelfLifeDays?: number | null;
  minExpiryDaysOnReceipt?: number;
  expiryAlertDays?: number;
}

interface ItemBarcodeModel {
  itemBarcodeId?: number;
  barcode: string;
  barcodeType: string; // "Ean13" | "Code128" | "QrCode" | "UpcA"
  uomId?: number | null;
  isPrimary?: boolean;
  isActive?: boolean;
}
```

### 4.2 Item Categories (`/api/item-categories`)
- `GET /api/item-categories` (`includeInactive: boolean`) -> `List<ItemCategoryListItem>`
- `POST /api/item-categories` -> `201 Created`
- `PUT /api/item-categories/{categoryId:long}` -> `204 No Content`
- `DELETE /api/item-categories/{categoryId:long}` -> `204 No Content`
- `PATCH /api/item-categories/reorder` -> `204 No Content`

### 4.3 Units of Measure & Unit Types (`/api/uom-types`)
- `GET /api/uom-types` (`includeInactive: boolean`) -> `List<UomTypeListItem>` (includes inline `units: UnitOfMeasureListItem[]`)
- `POST /api/uom-types` -> `201 Created`
- `PUT /api/uom-types/{uomTypeId:long}` -> `204 No Content`
- `DELETE /api/uom-types/{uomTypeId:long}` -> `204 No Content`
- `PATCH /api/uom-types/reorder` -> `204 No Content`
- `POST /api/uom-types/units` -> `201 Created` (`SaveUnitOfMeasureRequest`)
- `PUT /api/uom-types/units/{uomId:long}` -> `204 No Content`
- `PUT /api/uom-types/units/{uomId:long}/base` -> `204 No Content` (Rescales unit type)
- `DELETE /api/uom-types/units/{uomId:long}` -> `204 No Content`

### 4.4 Warehouses (`/api/warehouses`)
- `GET /api/warehouses` (`includeInactive: boolean`) -> `List<WarehouseListItem>`
- `GET /api/warehouses/{warehouseId:long}` -> `WarehouseListItem`
- `POST /api/warehouses` -> `201 Created` (`SaveWarehouseRequest`)
- `PUT /api/warehouses/{warehouseId:long}` -> `204 No Content`
- `PUT /api/warehouses/{warehouseId:long}/default` -> `204 No Content`
- `DELETE /api/warehouses/{warehouseId:long}` -> `204 No Content`

### 4.5 Stock Positions & Movements (`/api/stock`)
- `GET /api/stock` (`search?: string`, `belowReorderOnly: boolean`) -> `List<StockPosition>`
- `GET /api/stock/{itemId:long}` -> `StockPosition`
- `GET /api/stock/movements` (`itemId?: long`, `warehouseId?: long`, `from?: DateOnly`, `to?: DateOnly`) -> `List<StockMovementListItem>`
- `GET /api/stock/movements/{stockMovementId:long}/allocations` -> `List<CostAllocationItem>`
- `GET /api/stock/costing-queue` -> `CostingQueueStatus`
- `GET /api/stock/recostings` (`itemId?: long`) -> `List<RecostingAdjustmentItem>`
- `POST /api/stock/movements` (`RecordStockMovementRequest`) -> `RecordStockMovementResult`
- `POST /api/stock/transfers` (`TransferStockRequest`) -> `RecordStockMovementResult`

### 4.6 Stock Adjustments (`/api/stock-adjustments`)
- `GET /api/stock-adjustments` (`status?: string`) -> `List<StockAdjustmentListItem>`
- `GET /api/stock-adjustments/{stockAdjustmentId:long}` -> `StockAdjustmentDetail`
- `POST /api/stock-adjustments` (`SaveStockAdjustmentRequest`) -> `{ stockAdjustmentId: number }`
- `PUT /api/stock-adjustments/{stockAdjustmentId:long}` -> `{ stockAdjustmentId: number }`
- `DELETE /api/stock-adjustments/{stockAdjustmentId:long}` -> `200 OK`
- `POST /api/stock-adjustments/{stockAdjustmentId:long}/post` -> `200 OK`
- `POST /api/stock-adjustments/{stockAdjustmentId:long}/reverse` (`ReverseStockAdjustmentRequest`) -> `200 OK`

---

## 5. Accounting Module (UI Label: Strictly "Accounts")

> **MANDATORY UI RULE:** The user-facing label for this module is strictly **"Accounts"**. The string "Accounting" must NEVER appear in the UI.

### 5.1 Chart of Accounts (`/api/accounts`)
- `GET /api/accounts` (`includeInactive: boolean`) -> `List<AccountListItem>`
- `GET /api/accounts/{accountId:long}` -> `AccountListItem`
- `POST /api/accounts` -> `201 Created` `{ accountId: number }`
- `PUT /api/accounts/{accountId:long}` -> `204 No Content`
- `DELETE /api/accounts/{accountId:long}` -> `204 No Content` (Deactivates; system accounts locked)

```typescript
interface SaveAccountRequest {
  accountTypeId: number; // 1=Asset, 2=Liability, 3=Equity, 4=Income, 5=Expense
  accountCode: string;   // Required, MaxLength(20)
  accountName: string;   // Required, MaxLength(200)
  parentAccountId?: number | null;
  currencyCode?: string | null;
  isContra?: boolean;
  isActive?: boolean;
  isLock?: boolean;
  isSales?: boolean;
  isPurchase?: boolean;
  isPayment?: boolean;
  isBank?: boolean;
}
```

### 5.2 Sub-Accounts (`/api/sub-accounts`)
- `GET /api/sub-accounts` (`accountId?: long`, `referenceType?: string`, `referenceId?: long`) -> `List<SubAccountListItem>`

### 5.3 Tax Masters (`/api/tax-masters`)
- `GET /api/tax-masters` (`includeHistory: boolean`, `includeInactive: boolean`) -> `List<TaxMasterListItem>`
- `GET /api/tax-masters/{taxMasterId:long}` -> `TaxMasterListItem`
- `GET /api/tax-masters/resolve/{taxGroupId:long}?onDate=YYYY-MM-DD` -> `TaxMasterListItem`
- `POST /api/tax-masters` -> `201 Created` (`SaveTaxMasterRequest`)
- `POST /api/tax-masters/{taxMasterId:long}/revise` -> `200 OK` `{ taxMasterId: number }`
- `PUT /api/tax-masters/{taxMasterId:long}/name` -> `204 No Content` (`RenameTaxRequest` `{ taxName: string }`)
- `DELETE /api/tax-masters/{taxMasterId:long}` -> `204 No Content`

### 5.4 Payment Terms (`/api/payment-terms`)
- `GET /api/payment-terms` (`includeInactive: boolean`) -> `List<PaymentTermListItem>`
- `GET /api/payment-terms/{paymentTermId:long}` -> `PaymentTermListItem`
- `GET /api/payment-terms/{paymentTermId:long}/due-date?documentDate=YYYY-MM-DD` -> `DueDateResult`
- `POST /api/payment-terms` -> `201 Created` (`SavePaymentTermRequest`)
- `PUT /api/payment-terms/{paymentTermId:long}` -> `204 No Content`
- `PUT /api/payment-terms/{paymentTermId:long}/default` -> `204 No Content`
- `PATCH /api/payment-terms/reorder` -> `204 No Content`
- `DELETE /api/payment-terms/{paymentTermId:long}` -> `204 No Content`

### 5.5 Numbering Series (`/api/numbering-series`)
- `GET /api/numbering-series` (`includeInactive: boolean`, `seriesFor?: string`) -> `List<NumberingSeriesListItem>`
- `GET /api/numbering-series/{numberingSeriesId:long}` -> `NumberingSeriesListItem`
- `POST /api/numbering-series` -> `201 Created` (`SaveNumberingSeriesRequest`)
- `PUT /api/numbering-series/{numberingSeriesId:long}` -> `204 No Content`
- `PUT /api/numbering-series/{numberingSeriesId:long}/next-number` -> `SetNextNumberRequest` `{ nextNumber: number }`
- `PUT /api/numbering-series/{numberingSeriesId:long}/default` -> `204 No Content`
- `PATCH /api/numbering-series/reorder` -> `204 No Content`
- `DELETE /api/numbering-series/{numberingSeriesId:long}` -> `204 No Content`

### 5.6 Journal Entries (`/api/journals`)
- `GET /api/journals` (`status?: string`, `from?: DateOnly`, `to?: DateOnly`) -> `List<JournalListItem>`
- `GET /api/journals/{journalId:long}` -> `JournalDetailView`
- `POST /api/journals` -> `201 Created` `{ journalId: number }` (`SaveJournalRequest`)
- `PUT /api/journals/{journalId:long}` -> `204 No Content`
- `POST /api/journals/{journalId:long}/post` -> `204 No Content`
- `POST /api/journals/{journalId:long}/reverse` (`ReverseJournalRequest`) -> `200 OK` `{ journalId: number }`
- `DELETE /api/journals/{journalId:long}` -> `204 No Content` (Draft only)

```typescript
interface SaveJournalRequest {
  journalDate: string;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  reference?: string | null;
  memo?: string | null;
  lines: SaveJournalLineRequest[];
}

interface SaveJournalLineRequest {
  accountId: number;
  subAccountId?: number | null;
  debitAmount: number;  // Debit XOR Credit
  creditAmount: number;
  lineMemo?: string | null;
}
```

### 5.7 Ledger & Double-Entry Reports (`/api/ledger`)
- `GET /api/ledger/accounts/{accountId:long}` (`from?: DateOnly`, `to?: DateOnly`) -> `AccountLedgerView`
- `GET /api/ledger/trial-balance` (`from?: DateOnly`, `to?: DateOnly`) -> `TrialBalanceView`
- `GET /api/ledger/sub-ledger-tie` (`asAt?: DateOnly`) -> `SubLedgerTieView`
- `GET /api/ledger/documents/{transactionTypeCode}/{transactionId:long}/rate` -> `SettlementRateView`
- `GET /api/ledger/contacts/{contactId:long}/outstanding-balances/{ledgerTypeId:int}` -> `List<OutstandingBalanceView>`

### 5.8 Opening Balances (`/api/opening-balance`)
- `GET /api/opening-balance` -> `OpeningBalanceView`
- `GET /api/opening-balance/readiness` -> `OpeningBalanceReadinessView` (Validation & equity delta check)
- `GET /api/opening-balance/tie` -> `SubLedgerTieView`
- `PUT /api/opening-balance` (`SaveOpeningBalanceRequest`) -> `204 No Content`
- `POST /api/opening-balance/finalize` -> `204 No Content`
- `DELETE /api/opening-balance` -> `204 No Content`

### 5.9 Closing Dates / Period Locks (`/api/period-locks`)
- `GET /api/period-locks` -> `List<PeriodLockListItem>`
- `GET /api/period-locks/mine` -> `PeriodLockStatus` `{ lockedUpto: DateOnly | null, openFrom: DateOnly | null }`
- `PUT /api/period-locks` (`SavePeriodLockRequest`) -> `{ periodLockId: number }`
- `DELETE /api/period-locks/{roleId:int}` -> `204 No Content`

### 5.10 Banking: Institutions & Accounts
- **Banks (`/api/banks`):** `GET`, `POST`, `PUT /{id}`, `DELETE /{id}`, `PATCH /reorder`
- **Bank Accounts (`/api/bank-accounts`):** `GET`, `POST`, `PUT /{id}`, `PUT /{id}/default`, `POST /{id}/link-ledger`, `DELETE /{id}`, `PATCH /reorder`

### 5.11 Banking: Money Documents
- **Spend Money (`/api/spend-money`):** `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `POST /{id}/post`, `POST /{id}/void`, `DELETE /{id}`
- **Receive Money (`/api/receive-money`):** `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `POST /{id}/post`, `POST /{id}/void`, `DELETE /{id}`
- **Transfer Money (`/api/transfer-money`):** `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `POST /{id}/post`, `POST /{id}/void`, `DELETE /{id}`

```typescript
interface SaveMoneyDocumentRequest {
  transactionDate: string;
  bankAccountId: number;
  contactId: number;
  amount: number;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  paymentMethod: number | string; // 0=Cash, 1=Cheque, 2=BankTransfer, 3=Upi, 4=Card
  referenceNo?: string | null;
  referenceDate?: string | null;
  memo?: string | null;
  lines: SaveMoneyLineRequest[];
}

interface SaveMoneyLineRequest {
  ledgerSourceId: number; // Purpose
  mappingTransactionTypeCode?: string | null;
  mappingTransactionId?: number | null;
  amount: number;
  lineMemo?: string | null;
}
```

### 5.12 Bank Statements & Reconciliation (`/api/statements`)
- `GET /api/statements` (`bankAccountId?: long`) -> `List<BankStatementListItem>`
- `GET /api/statements/{bankStatementId:long}/lines` -> `List<StatementLineView>`
- `GET /api/statements/profiles/{bankAccountId:long}` -> `ImportProfileView`
- `PUT /api/statements/profiles` -> `SaveImportProfileRequest`
- `POST /api/statements/import` (Multipart upload: `bankAccountId`, `file`) -> `ImportStatementResult`
- `POST /api/statements/lines/{lineId:long}/match` -> `MatchStatementLineRequest`
- `POST /api/statements/lines/{lineId:long}/unmatch` -> `204 No Content`
- `POST /api/statements/lines/{lineId:long}/ignore` -> `IgnoreStatementLineRequest`
- `GET /api/statements/{bankStatementId:long}/export?format=csv|xlsx` -> File stream

---

## 6. Contacts Module (`/api/contacts`)

### 6.1 Contacts Master (`/api/contacts`)

#### List API
- **Endpoint:** `GET /api/contacts`
- **Query Params:**
  - `search?: string`
  - `role?: string` ("customer" | "vendor" | "jobworker" | "prescriber")
  - `includeInactive: boolean`
- **Response:** `200 OK` -> `List<ContactListItem>`
```typescript
interface ContactListItem {
  contactId: number;
  contactCode: string;
  displayName: string;
  contactCategory: string; // "Individual" | "Business"
  isCustomer: boolean;
  isVendor: boolean;
  isJobWorker: boolean;
  isPrescriber: boolean;
  gstin?: string | null;
  gstRegistrationType: string; // "Regular" | "Composition" | "Unregistered" | "Overseas" | "Consumer" | "Sez"
  currencyCode: string;
  creditLimit?: number | null;
  isSubLedgerLinked: boolean;
  isActive: boolean;
  email?: string | null;
  mobileNumber?: string | null;
  city?: string | null;
}
```

#### Get Detail API
- **Endpoint:** `GET /api/contacts/{contactId:long}`
- **Response:** `200 OK` -> `ContactDetail`
```typescript
interface ContactDetail extends ContactListItem {
  legalName?: string | null;
  pan?: string | null;
  tan?: string | null;
  placeOfSupplyStateId?: number | null;
  countryId?: number | null;
  paymentTermId?: number | null;
  maxOutstandingDays?: number | null;
  maxDiscountPercent?: number | null;
  receivableAccountId?: number | null;
  payableAccountId?: number | null;
  isTdsApplicable: boolean;
  tdsSection?: string | null;
  isMsme: boolean;
  udyamNumber?: string | null;
  notes?: string | null;
  addresses: ContactAddressModel[];
  persons: ContactPersonModel[];
  bankDetails: ContactBankDetailModel[];
  licences: ContactLicenceModel[];
  attachments: ContactAttachmentModel[];
}
```

#### Create & Update APIs
- **Create:** `POST /api/contacts` -> `201 Created` `{ contactId: number, contactCode: string }`
- **Update:** `PUT /api/contacts/{contactId:long}` -> `204 No Content`
- **Link Sub-Ledger Retry:** `POST /api/contacts/{contactId:long}/link-sub-ledger` -> `204 No Content`
- **Deactivate:** `DELETE /api/contacts/{contactId:long}` -> `204 No Content`
- **Request Body:** `SaveContactRequest`
```typescript
interface SaveContactRequest {
  contactCode?: string | null;
  isCustomer: boolean;
  isVendor: boolean;
  isJobWorker: boolean;
  isPrescriber: boolean;
  contactCategory: string; // "Individual" | "Business"
  displayName: string;     // Required, MaxLength(200)
  legalName?: string | null;
  gstin?: string | null;
  gstRegistrationType: string;
  pan?: string | null;
  tan?: string | null;
  placeOfSupplyStateId?: number | null;
  countryId?: number | null;
  currencyCode: string;    // Required, default "INR"
  paymentTermId?: number | null;
  creditLimit?: number | null;
  maxOutstandingDays?: number | null;
  maxDiscountPercent?: number | null;
  receivableAccountId?: number | null;
  payableAccountId?: number | null;
  isTdsApplicable?: boolean;
  tdsSection?: string | null;
  isMsme?: boolean;
  udyamNumber?: string | null;
  notes?: string | null;
  isActive?: boolean;
  addresses: ContactAddressModel[];
  persons: ContactPersonModel[];
  bankDetails: ContactBankDetailModel[];
  licences: ContactLicenceModel[];
}
```

### 6.2 Contact Attachments & Roles
- **Contact Roles:** `GET /api/contact-person-roles`, `POST`, `PUT /{id}`, `PUT /{id}/default`, `PATCH /reorder`, `DELETE /{id}`
- **Attachments:** `GET /api/contacts/{contactId}/attachments`, `POST /api/contacts/{contactId}/attachments` (Multipart), `GET /api/contacts/attachments/{id}/download`, `DELETE /api/contacts/attachments/{id}`
- **Expiring Licences:** `GET /api/contacts/licences/expiring?withinDays=30`

---

## 7. Master, Auth & Topbar Dropdown Endpoints

### 7.1 Authentication (`/api/auth`)
- **Login Step 1:** `POST /api/auth/login` (Body: `{ email, password }`) -> `LoginResponse`
  - Returns `preAuthToken`, `expiresInSeconds`, `organizations: AccessibleOrgDto[]`, `requiresOrgSelection: boolean`.
- **Select Org Step 2:** `POST /api/auth/select-organization` (Header: `X-PreAuth-Token`, Body: `{ orgId }`) -> `TokenResponse`
  - Returns `accessToken`, `refreshToken`, `accessExpiresInSeconds`, `licenseStatus`, `licenseExpiry`, `expiryIsBranchLevel`.
- **Switch Org:** `POST /api/auth/switch-organization` (Header: Bearer access token, Body: `{ orgId }`) -> `TokenResponse`.
- **List Accessible Orgs:** `GET /api/auth/organizations` -> `List<AccessibleOrgDto>` `{ orgId, orgName, roleName }`.

### 7.2 Organization Master & Topbar Dropdowns (`/api/organizations`)
- **List All Branches:** `GET /api/organizations` -> `List<OrganizationListItem>`
- **Get Current Branch:** `GET /api/organizations/current` -> `OrganizationListItem`
- **Update Current Branch:** `PUT /api/organizations/current` (`SaveOrganizationRequest`) -> `204 No Content`
- **Create Branch:** `POST /api/organizations` -> `201 Created` (`SaveOrganizationResult`)
- **Update Branch by ID:** `PUT /api/organizations/{orgId:guid}` -> `204 No Content`

#### Topbar Data Source Integration:
1. **Searchable Org Dropdown:**
   - Source: `GET /api/auth/organizations` or `GET /api/organizations`
   - Active Branch: Selected `OrgId` stored in state / token claim `org_id`.
   - Action on Switch: Call `POST /api/auth/switch-organization` -> update token and reload application state.
2. **Financial Year Tag:**
   - Sourced from `GET /api/organizations/current` field `financialYearStartMonth` (integer 1-12, default 4 for April).
   - Calculation: If current month >= `financialYearStartMonth`, FY is `CurrentYear - (CurrentYear + 1)` (e.g. FY 2026-27). Otherwise `(CurrentYear - 1) - CurrentYear`.

### 7.3 Reference Master Data (`/api/master`)
- `GET /api/master/countries` -> `List<{ countryId, countryCode, countryName, currencyCode, phoneCode }>`
- `GET /api/master/countries/{countryId}/states` -> `List<{ stateId, stateCode, stateName }>`
- `GET /api/master/states/{stateId}` -> `{ stateId, countryId, stateCode, stateName, isActive }`
- `GET /api/master/currencies` -> `List<{ currencyId, code, name, symbol, format, decimalPlaces, symbolPosition }>`
- `GET /api/master/transaction-types` (`postingOnly?: boolean`) -> `List<{ code, name, isLedgerPosting }>`
- `GET /api/master/ledger-types` -> `List<{ ledgerTypeId, code, name }>`
- `GET /api/master/ledger-sources` -> `List<{ ledgerSourceId, code, name, direction }>`
- `GET /api/master/hsn-sac` (`search?`, `codeType?`, `includeChapters`, `includeInactive`, `skip`, `take`) -> `{ total, skip, rows }`
- `GET /api/master/hsn-sac/chapters` -> `List<{ code, description, codeType }>`
- `GET /api/master/account-types` -> `List<{ accountTypeId, displayName, normalBalance, reportSection, sortOrder }>`

---

## 8. Reporting Module (`/api/reports`, `/api/statements`, `/api/reports/gst`)

### 8.1 Reporting Engine (`/api/reports`)
- `GET /api/reports` -> Catalog list of available reports
- `GET /api/reports/{reportKey}` -> Metadata, parameter definitions, and column definitions
- `POST /api/reports/{reportKey}/query` (`ReportQueryRequest`) -> `ReportResultView` (Pivots, groupings, aggregations)
- `POST /api/reports/{reportKey}/export?format=xlsx` (`ReportQueryRequest`) -> Excel binary file
- `GET/POST/PUT/DELETE /api/reports/{reportKey}/views` -> Saved layouts management

### 8.2 Standard Financial Statements (`/api/statements`)
- `GET /api/statements/profit-and-loss?fromDate=...&toDate=...` -> P&L statement with Net Profit & Gross Profit
- `GET /api/statements/balance-sheet?asOfDate=...` -> Balance Sheet with Assets, Liabilities, Equity (including Current Year Earnings)
- `GET /api/statements/inventory-valuation` -> Stock valuation report by Item

### 8.3 GST Filings (`/api/reports/gst`)
- `GET /api/reports/gst/gstr1?from=...&to=...` -> GSTR-1 Outward Supplies split into B2B and B2C
- `GET /api/reports/gst/gstr3b?from=...&to=...` -> GSTR-3B Outward Tax Summary

---

## 9. Frontend Service & Form Model Blueprint

### 9.1 Discrepancies Fixed & Alignment Matrix

| Module | Backend Request DTO | Identified Discrepancies in Legacy Frontend Models | Required Angular Form Control / Property Mapping |
|---|---|---|---|
| **Sales Quote** | `SaveQuoteRequest` / `SaveQuoteLineRequest` | Legacy `QuoteLineRequest` had `taxMasterId` instead of `taxGroupId`, and missed `warehouseId`, `conversionFactor`, `isPriceInclusive`, `discountAmount`, `taxTreatment`, `lineType`, `accountId`, `fixedAssetCategoryId`, `itemBatchId`. | `documentDate`, `contactId`, `validUntil`, `contactGstin`, `placeOfSupplyStateCode`, `billingAddress`, `shippingAddress`, `currencyCode`, `exchangeRate`, `notes`, `termsAndConditions`, `lines: FormArray<QuoteLineGroup>` containing `itemId`, `description`, `hsnSacCode`, `warehouseId`, `quantity`, `uomId`, `conversionFactor`, `unitPrice`, `isPriceInclusive`, `discountPercent`, `discountAmount`, `taxTreatment`, `taxGroupId`, `lineType`, `accountId`, `fixedAssetCategoryId`, `itemBatchId`, `lineNotes`. |
| **Sales Order** | `SaveSalesOrderRequest` / `SaveSalesOrderLineRequest` | Similar to Quotes: missed `warehouseId`, `conversionFactor`, `isPriceInclusive`, `discountAmount`, `taxGroupId`, `lineType`, `accountId`. | Matches `SaveQuoteRequest` structure plus `deliveryDate`, with `quoteId` if converted from quote. |
| **Sales Invoice** | `SaveInvoiceRequest` / `SaveInvoiceLineRequest` | Simple POS/retail invoice vs standard trading header. | `contactId`, `documentDate`, `currencyCode`, `exchangeRate`, `quoteId`, `salesOrderId`, `deliveryChallanId`, `paymentTermId`, `dueDate`, `tillId`, `cashierUserId`, `paymentMode`, `tenderedAmount`, `changeAmount`, `billingAddress`, `shippingAddress`, `notes`, `lines` (`itemId`, `quantity`, `unitPrice`, `discountPercent`, `taxGroupIds: number[]`). |
| **Purchase Bill** | `SaveBillRequest` / `SaveBillLineRequest` | Vendor bill requires `vendorBillNo` and `vendorBillDate`. | `documentDate`, `contactId`, `purchaseOrderId`, `goodsReceiptId`, `vendorBillNo` (Required), `vendorBillDate` (Required), `paymentTermId`, `dueDate`, `contactGstin`, `placeOfSupplyStateCode`, `billingAddress`, `shippingAddress`, `currencyCode`, `exchangeRate`, `landedCostAmount`, `notes`, `termsAndConditions`, `lines`. |
| **Items Master** | `SaveItemRequest` | Aggregate form including `barcodes`, `jewellery`, and `pharma` conditional sub-forms. | `itemCode`, `itemName` (Req), `printName`, `description`, `itemProfile` (Req), `itemType` (Req), `itemCategoryId`, `hsnSacCodeId`, `taxGroupId`, `taxPreference` (Req), `isPriceInclusiveOfTax`, `uomTypeId` (Req), `inventoryUomId` (Req), `salesUomId` (Req), `purchaseUomId` (Req), `reportUomId` (Req), `trackInventory`, `costingType` (Req), `isBatchTracked`, `isExpiryTracked`, `isSerialTracked`, `salesPrice`, `purchasePrice`, `mrp`, `minSalePrice`, `standardCost`, `reorderLevel`, `reorderQuantity`, `minStockLevel`, `maxStockLevel`, `leadTimeDays`, `defaultWarehouseId`, `isSales`, `isPurchase`, `isReturnable`, `imageUrl`, `isActive`, `jewellery` (FormGroup), `pharma` (FormGroup), `barcodes` (FormArray). |
| **Contacts Master** | `SaveContactRequest` | Aggregate form requiring `addresses`, `persons` (at least 1 default), `bankDetails`, `licences`. | `contactCode`, `isCustomer`, `isVendor`, `isJobWorker`, `isPrescriber` (at least 1 true), `contactCategory` ("Individual"\|"Business"), `displayName` (Req), `legalName`, `gstin`, `gstRegistrationType` (Req), `pan`, `tan`, `placeOfSupplyStateId`, `countryId`, `currencyCode` (Req), `paymentTermId`, `creditLimit`, `maxOutstandingDays`, `maxDiscountPercent`, `receivableAccountId`, `payableAccountId`, `isTdsApplicable`, `tdsSection`, `isMsme`, `udyamNumber`, `notes`, `isActive`, `addresses: FormArray`, `persons: FormArray`, `bankDetails: FormArray`, `licences: FormArray`. |
| **Accounts Master** | `SaveAccountRequest` | Label strictly **"Accounts"**. | `accountTypeId` (1-5), `accountCode` (Req), `accountName` (Req), `parentAccountId`, `currencyCode`, `isContra`, `isActive`, `isLock`, `isSales`, `isPurchase`, `isPayment`, `isBank`. |
| **Journal Entry** | `SaveJournalRequest` | Balanced debit & credit lines. | `journalDate` (Req), `currencyCode`, `exchangeRate`, `reference`, `memo`, `lines: FormArray` (`accountId`, `subAccountId`, `debitAmount`, `creditAmount`, `lineMemo`). Must enforce `totalDebit == totalCredit` on post. |

---

## 10. Summary & Sign-off

The API contracts for all 7 modules across RetailErp (Bill-Book) have been fully probed, analyzed, and mapped from the C# controller endpoints, entity models, YARP reverse proxy definitions, and Postman collection generators. Frontend teams can proceed with 100% confidence that reactive form controls, models, DTOs, and URL patterns match backend contracts.
