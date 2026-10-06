# Bill-Book Local API Testing Plan by Business Type

**Status**: Active Test Plan  
**Target Architecture**: .NET 10 Microservices + YARP API Gateway (Reverse Proxy) + PostgreSQL Multi-Tenancy (RLS + Global Query Filters)  
**Gateway URL**: `http://localhost:4500`  
**Direct Service Range**: `http://localhost:4501` – `http://localhost:4522`  

---

## 1. Executive Summary & Objective

This document defines the comprehensive local API testing strategy for **Bill-Book** (RetailErp). It outlines step-by-step API verification procedures organized into sequential testing phases, validating the platform's **23 backend API services** (1 Gateway API + 22 Domain Microservices) across all **6 supported Business Types** and enterprise add-ons.

### Business Types Matrix (D-36, D-37, TK-126)

| Business Type Enum | Code Name | Demo Customer Code | Customer ID (`CustomerId`) | Head Office Org ID (`OrgId`) | Core Focus / Verticals Tested |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Retail = 1` | Retail / General | `DEMO-RTL` | `00000000-0000-0000-0d00-000000000001` | `00000000-0000-0000-0d01-000000000001` | Standard inventory, sales orders, invoices, POS, purchases, accounting. |
| `Pharma = 2` | Pharmacy | `DEMO-PHA` | `00000000-0000-0000-0d00-000000000002` | `00000000-0000-0000-0d01-000000000002` | Batch tracking, manufacturing/expiry dates, Schedule H/H1/X drugs, FEFO dispatch. |
| `Jewellery = 3` | Jewellery | `DEMO-JWL` | `00000000-0000-0000-0d00-000000000003` | `00000000-0000-0000-0d01-000000000003` | Metal purity (22K/24K), live metal rates, making charges, hallmarking, savings schemes. |
| `School = 4` | Education / SIS | `DEMO-SCH` | `00000000-0000-0000-0d00-000000000004` | `00000000-0000-0000-0d01-000000000004` | Inquiries, student admissions, attendance registers, fee structures, receipts. |
| `Service = 5` | Services & Repairs | `DEMO-SVC` | `00000000-0000-0000-0d00-000000000005` | `00000000-0000-0000-0d01-000000000005` | Customer support tickets, equipment assets, AMCs, work orders, preventive maintenance. |
| `Restaurant = 6` | Food & Beverage | `DEMO-RST` | `00000000-0000-0000-0d00-000000000006` | `00000000-0000-0000-0d01-000000000006` | Table management, KOTs, recipe/BOM ingredient auto-depletion, Swiggy/Zomato intake. |

---

## 2. API Services Port & Gateway Routing Reference

All requests can be sent through the **YARP Gateway** at `http://localhost:4500` or directly to individual microservices during isolated debugging:

| # | Microservice | Direct Port | Gateway Cluster | Key Route Prefixes |
| :-: | :--- | :---: | :--- | :--- |
| **1** | **Gateway.Api** | `4500` | N/A | `/api/*`, `/health` |
| **2** | **Accounting.Api** | `4501` | `accounting` | `/api/accounts`, `/api/journals`, `/api/allocations`, `/api/vouchers` |
| **3** | **Customer.Api** | `4502` | `customer` | `/api/leads`, `/api/tickets`, `/api/service-plans` |
| **4** | **Inventory.Api** | `4503` | `inventory` | `/api/items`, `/api/stock`, `/api/warehouses`, `/api/recipes`, `/api/restaurant-menu` |
| **5** | **Master.Api** | `4504` | `master` | `/api/auth`, `/api/users`, `/api/roles`, `/api/organizations`, `/api/contacts`, `/api/rates` |
| **6** | **Purchase.Api** | `4505` | `purchase` | `/api/purchase-orders`, `/api/bills`, `/api/goods-receipts`, `/api/debit-notes` |
| **7** | **Reporting.Api** | `4506` | `reporting` | `/api/reports`, `/api/reports/definitions`, `/api/reports/export`, `/api/gst` |
| **8** | **Sales.Api** | `4507` | `sales` | `/api/sales-invoices`, `/api/sales-orders`, `/api/quotations`, `/api/pos`, `/api/aggregators` |
| **9** | **Printing.Api** | `4508` | `printing` | `/api/print`, `/api/print/templates`, `/api/print/barcodes` |
| **10** | **Employee.Api** | `4509` | `hrm` | `/api/employees`, `/api/departments`, `/api/designations` |
| **11** | **TimeLeave.Api** | `4510` | `timeleave` | `/api/attendance`, `/api/leaves`, `/api/shifts` |
| **12** | **Payroll.Api** | `4511` | `payroll` | `/api/payroll-runs`, `/api/salary-setup`, `/api/payslips` |
| **13** | **Recruitment.Api** | `4512` | `recruitment` | `/api/job-openings`, `/api/candidates`, `/api/interviews` |
| **14** | **Performance.Api** | `4513` | `performance` | `/api/appraisals`, `/api/kpis`, `/api/reviews`, `/api/goals` |
| **15** | **Claims.Api** | `4514` | `claims` | `/api/expense-claims`, `/api/claims/approvals` |
| **16** | **Student.Api** | `4515` | `sis` | `/api/students`, `/api/academic-years`, `/api/classes` |
| **17** | **Admission.Api** | `4516` | `admission` | `/api/inquiries`, `/api/applications`, `/api/admissions` |
| **18** | **Attendance.Api** | `4517` | `attendance` | `/api/student-attendance`, `/api/attendance-registers` |
| **19** | **Fee.Api** | `4518` | `fee` | `/api/fee-structures`, `/api/fee-collections`, `/api/student-dues` |
| **20** | **Facility.Api** | `4519` | `facility` | `/api/facilities`, `/api/assets`, `/api/locations` |
| **21** | **WorkOrder.Api** | `4520` | `workorder` | `/api/work-orders`, `/api/work-order-tasks`, `/api/technicians` |
| **22** | **Preventive.Api** | `4521` | `preventive` | `/api/preventive/plans`, `/api/preventive/occurrences` |
| **23** | **MaintenanceContract.Api** | `4522` | `amc` | `/api/maintenance-contracts`, `/api/slas`, `/api/amc-renewals` |

---

## 3. Phased Testing Execution Plan

---

### Phase 0: Local Environment Bootstrap & Health Check (`Gateway.Api`)
**Objective**: Ensure local PostgreSQL instance is running, admin migrations are applied, and all microservices are responding.

1. **Verify Services Status**:
   - Execute PowerShell backend bootstrapper:
     ```powershell
     ./scripts/start-backend.ps1
     ```
   - Verify health check through **Gateway.Api** (Port 4500):
     ```http
     GET http://localhost:4500/health
     ```
   - **Assertion**: HTTP `200 OK`.

2. **Verify Database Allocation & Seeding**:
   - Ensure demo customers are populated via `DemoCustomerRepository.EnsureAllAsync` or endpoint:
     ```http
     POST http://localhost:4500/api/admin/demo-seed
     ```
   - **Assertion**: HTTP `200 OK`, returning seeded demo tenants (`DEMO-RTL`, `DEMO-PHA`, `DEMO-JWL`, `DEMO-SCH`, `DEMO-SVC`, `DEMO-RST`).

---

### Phase 1: Authentication, Token Context & Tenant Scope Guards (`Master.Api`)
**Objective**: Authenticate as a user, acquire JWT bearer token, and inspect token claims (`CustomerId`, `OrgId`, `business_type`, `permissions`).

1. **Sign-In Endpoint (`Master.Api`)**:
   ```http
   POST http://localhost:4500/api/auth/login
   Content-Type: application/json

   {
     "email": "owner@example.com",
     "password": "Password123!"
   }
   ```
   - **Assertion**:
     - Status: `200 OK`
     - Response Body contains `accessToken`, `refreshToken`, and user organizations array.
     - Token payload includes:
       - `sub` (User GUID)
       - `customer_id` (Tenant GUID)
       - `org_id` (Branch GUID)
       - `business_type` (Enum string or int)
       - `permissions` (Array of permission keys)

2. **Tenant Header & Scope Guard Verification**:
   - Send requests with `Authorization: Bearer <TOKEN>` and `X-Org-Id: <ORG_GUID>`.
   - Send mismatched `X-Org-Id` to verify tenant security guard returns `403 Forbidden` or `401 Unauthorized`.

---

### Phase 2: Common Foundation Workflows (Contacts & Accounting)
**Objective**: Verify master data creation that all verticals rely upon (Customers, Vendors, Chart of Accounts).

1. **Create Customer & Vendor Contact (`Master.Api`)**:
   ```http
   POST http://localhost:4500/api/contacts
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "name": "Acme Retail Client",
     "contactType": "Customer",
     "gstin": "27AAPCA1234F1Z5",
     "billingAddress": "123 Market St, Mumbai",
     "phone": "+91 9876543210"
   }
   ```
   - **Assertion**: HTTP `201 Created` with generated contact `id`.

2. **Verify Chart of Accounts (`Accounting.Api`)**:
   ```http
   GET http://localhost:4500/api/accounts
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   ```
   - **Assertion**: HTTP `200 OK`. Pre-seeded accounts exist (Cash, Bank, Accounts Receivable, Accounts Payable, Sales, GST Output, GST Input).

---

### Phase 3: Business Type 1 — Retail / FMCG / Trading (`DEMO-RTL`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000001`
- OrgId: `00000000-0000-0000-0d01-000000000001`
- `business_type`: `Retail`

1. **Step 3.1: Create Standard Item & Opening Stock (`Inventory.Api`)**:
   ```http
   POST http://localhost:4500/api/items
   Authorization: Bearer <RETAIL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "code": "RTL-SHIRT-001",
     "name": "Cotton Casual Shirt",
     "hsnCode": "6105",
     "unitOfMeasure": "PCS",
     "costPrice": 400.0000,
     "sellingPrice": 999.0000,
     "taxRate": 5.0000,
     "barcode": "8901234567890"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 3.2: Record Goods Receipt / Vendor Bill (`Purchase.Api`)**:
   ```http
   POST http://localhost:4500/api/bills
   Authorization: Bearer <RETAIL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "vendorId": 1,
     "billNumber": "BILL-2026-001",
     "billDate": "2026-10-06",
     "dueDate": "2026-10-20",
     "items": [
       { "itemId": 1, "quantity": 100, "rate": 400.0000, "taxRate": 5.0000 }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created`. Inventory stock moves +100 units; Accounts Payable credited in Accounting.

3. **Step 3.3: POS Quick Bill / Sales Invoice (`Sales.Api`)**:
   ```http
   POST http://localhost:4500/api/sales-invoices
   Authorization: Bearer <RETAIL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "customerId": 1,
     "invoiceDate": "2026-10-06",
     "items": [
       { "itemId": 1, "quantity": 2, "rate": 999.0000, "discountPercent": 10.0000, "taxRate": 5.0000 }
     ],
     "payments": [
       { "method": "Cash", "amount": 1888.1100 }
     ]
   }
   ```
   - **Assertion**:
     - HTTP `201 Created`.
     - Stock reduced by 2 units in `Inventory.Api`.
     - Sales revenue and GST Output credited, Cash debited in `Accounting.Api`.
     - Transaction settled atomically via `AddBillBookReliability`.

4. **Step 3.4: Barcode Label Generation (`Printing.Api`)**:
   ```http
   POST http://localhost:4500/api/print/barcodes
   Authorization: Bearer <RETAIL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "barcode": "8901234567890",
     "format": "Code128",
     "labelTitle": "Cotton Casual Shirt",
     "mrp": 999.00
   }
   ```
   - **Assertion**: HTTP `200 OK` returning printable barcode raster image / PDF stream.

5. **Step 3.5: Financial & Sales Register Reports (`Reporting.Api`)**:
   ```http
   GET http://localhost:4500/api/reports/sales-register?fromDate=2026-10-01&toDate=2026-10-06
   Authorization: Bearer <RETAIL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   ```
   - **Assertion**: HTTP `200 OK` listing the newly created invoice with net taxable amount, CGST/SGST breakdown.

---

### Phase 4: Business Type 2 — Pharmacy / Healthcare (`DEMO-PHA`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000002`
- OrgId: `00000000-0000-0000-0d01-000000000002`
- `business_type`: `Pharma`

1. **Step 4.1: Register Drug Item with Batch & Schedule Tracking (`Inventory.Api`)**:
   ```http
   POST http://localhost:4500/api/items
   Authorization: Bearer <PHARMA_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000002
   Content-Type: application/json

   {
     "code": "MED-AMOX-500",
     "name": "Amoxicillin 500mg",
     "hsnCode": "3004",
     "unitOfMeasure": "STRIP",
     "isBatchTracked": true,
     "requiresDoctorPrescription": true,
     "drugSchedule": "Schedule_H",
     "costPrice": 45.0000,
     "sellingPrice": 85.0000,
     "taxRate": 12.0000
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 4.2: Inward Goods Receipt with Batch & Expiry (`Purchase.Api`)**:
   ```http
   POST http://localhost:4500/api/bills
   Authorization: Bearer <PHARMA_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000002
   Content-Type: application/json

   {
     "vendorId": 2,
     "billNumber": "PHARMA-INV-889",
     "billDate": "2026-10-06",
     "items": [
       {
         "itemId": 2,
         "batchNumber": "BAT-2026-X1",
         "expiryDate": "2028-09-30",
         "quantity": 500,
         "rate": 45.0000,
         "taxRate": 12.0000
       }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created`. Stock record created with active batch and expiry timestamp.

3. **Step 4.3: Enforce FEFO & Prescription Validation in Sales (`Sales.Api`)**:
   - **Negative Test**: Sale of expired batch must be rejected with HTTP `422 Unprocessable Entity` or `400 Bad Request`.
   - **Positive Test**: Sell valid batch with Doctor details recorded:
   ```http
   POST http://localhost:4500/api/sales-invoices
   Authorization: Bearer <PHARMA_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000002
   Content-Type: application/json

   {
     "customerId": 2,
     "doctorName": "Dr. R. Sharma",
     "doctorRegistrationNo": "MCI-48922",
     "items": [
       { "itemId": 2, "batchNumber": "BAT-2026-X1", "quantity": 10, "rate": 85.0000, "taxRate": 12.0000 }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created`. Batch quantity decreased by 10.

---

### Phase 5: Business Type 3 — Jewellery (`DEMO-JWL`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000003`
- OrgId: `00000000-0000-0000-0d01-000000000003`
- `business_type`: `Jewellery`

1. **Step 5.1: Daily Metal Rate Sync / Setup (`Master.Api` / `RateSync.Worker`)**:
   ```http
   POST http://localhost:4500/api/rates
   Authorization: Bearer <JEWELLERY_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000003
   Content-Type: application/json

   {
     "rateDate": "2026-10-06",
     "metalType": "Gold",
     "purityKarat": 22,
     "ratePerGram": 6850.0000,
     "source": "IBJA"
   }
   ```
   - **Assertion**: HTTP `200 OK` or `201 Created`.

2. **Step 5.2: Create Hallmarked Jewellery Item (`Inventory.Api`)**:
   ```http
   POST http://localhost:4500/api/items
   Authorization: Bearer <JEWELLERY_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000003
   Content-Type: application/json

   {
     "code": "JWL-NECK-22K",
     "name": "Bridal Gold Necklace 22K",
     "metalType": "Gold",
     "purityKarat": 22,
     "grossWeightGram": 24.5000,
     "netWeightGram": 24.1000,
     "wastagePercent": 2.5000,
     "makingChargePerGram": 450.0000,
     "hsnCode": "7113",
     "taxRate": 3.0000
   }
   ```
   - **Assertion**: HTTP `201 Created`.

3. **Step 5.3: Jewellery Invoice Calculation (`Sales.Api`)**:
   - Compute formula: `((NetWeight + Wastage) * RatePerGram) + MakingCharges + 3% GST`.
   ```http
   POST http://localhost:4500/api/sales-invoices
   Authorization: Bearer <JEWELLERY_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000003
   Content-Type: application/json

   {
     "customerId": 3,
     "invoiceDate": "2026-10-06",
     "items": [
       {
         "itemId": 3,
         "grossWeightGram": 24.5000,
         "netWeightGram": 24.1000,
         "ratePerGram": 6850.0000,
         "makingCharges": 10845.0000,
         "huid": "HUID778899",
         "taxRate": 3.0000
       }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created`.

4. **Step 5.4: Gold Savings Scheme Installment (`Accounting.Api`)**:
   ```http
   POST http://localhost:4500/api/savings-schemes/installments
   Authorization: Bearer <JEWELLERY_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000003
   Content-Type: application/json

   {
     "schemeAccountId": 101,
     "installmentMonth": 1,
     "amount": 5000.0000,
     "paymentMethod": "UPI"
   }
   ```
   - **Assertion**: HTTP `201 Created`. Liability credited to Scheme Deposits account.

---

### Phase 6: Business Type 4 — School & Student Information System (`DEMO-SCH`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000004`
- OrgId: `00000000-0000-0000-0d01-000000000004`
- `business_type`: `School`

1. **Step 6.1: Admission Inquiry & Student Intake (`Admission.Api` & `Student.Api`)**:
   ```http
   POST http://localhost:4500/api/inquiries
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "applicantName": "Aarav Patel",
     "parentName": "Vikram Patel",
     "contactPhone": "+91 9822011223",
     "targetGrade": "Grade 5",
     "academicYear": "2026-2027"
   }
   ```
   - Convert to Admitted Student in `Student.Api`:
   ```http
   POST http://localhost:4500/api/students
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "admissionNo": "SCH-2026-0042",
     "firstName": "Aarav",
     "lastName": "Patel",
     "grade": "Grade 5",
     "section": "A",
     "dateOfBirth": "2016-05-12"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 6.2: Daily Student Attendance Register (`Attendance.Api`)**:
   ```http
   POST http://localhost:4500/api/student-attendance
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "date": "2026-10-06",
     "grade": "Grade 5",
     "section": "A",
     "records": [
       { "studentId": 42, "status": "Present" }
     ]
   }
   ```
   - **Assertion**: HTTP `200 OK` or `201 Created`.

3. **Step 6.3: Fee Collection & Receipt (`Fee.Api`)**:
   ```http
   POST http://localhost:4500/api/fee-collections
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "studentId": 42,
     "term": "Term 1",
     "amountPaid": 25000.0000,
     "paymentMode": "NetBanking",
     "referenceNo": "TXN9928172"
   }
   ```
   - **Assertion**: HTTP `201 Created`. Fee Receipt generated; School Tuition Revenue credited in Accounting.

4. **Step 6.4: Campus Facility Preventive Maintenance Plan (`Preventive.Api`)**:
   ```http
   POST http://localhost:4500/api/preventive/plans
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "title": "School Science Lab Fire Safety Audit",
     "frequency": "Monthly",
     "assignedRole": "SafetyOfficer",
     "checklistItems": [
       "Inspect fire extinguisher pressure",
       "Check emergency shower & eyewash station"
     ]
   }
   ```
   - **Assertion**: HTTP `200 OK` or `201 Created`.

---

### Phase 7: Business Type 5 — Services & Maintenance (`DEMO-SVC`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000005`
- OrgId: `00000000-0000-0000-0d01-000000000005`
- `business_type`: `Service`

1. **Step 7.1: Register Facility Asset (`Facility.Api`)**:
   ```http
   POST http://localhost:4500/api/assets
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "assetCode": "AC-CHILLER-01",
     "name": "HVAC Central Chiller Unit",
     "location": "Building B - Plant Room",
     "serialNumber": "SN-HVAC-9921",
     "customerId": 5
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 7.2: Annual Maintenance Contract Creation (`MaintenanceContract.Api`)**:
   ```http
   POST http://localhost:4500/api/maintenance-contracts
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "contractNumber": "AMC-2026-05",
     "customerId": 5,
     "startDate": "2026-10-01",
     "endDate": "2027-09-30",
     "contractValue": 120000.0000,
     "slaResponseHours": 4,
     "coveredAssetIds": [ 1 ]
   }
   ```
   - **Assertion**: HTTP `201 Created`.

3. **Step 7.3: Service Ticket Creation (`Customer.Api`)**:
   ```http
   POST http://localhost:4500/api/tickets
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "customerId": 5,
     "assetId": 1,
     "title": "Chiller High Temperature Alarm",
     "severity": "High"
   }
   ```
   - **Assertion**: HTTP `201 Created`. Ticket generated with SLA due date.

4. **Step 7.4: Technician Work Order Dispatch (`WorkOrder.Api`)**:
   ```http
   POST http://localhost:4500/api/work-orders
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "ticketId": 1,
     "assignedTechnicianId": 12,
     "scheduledDate": "2026-10-07",
     "priority": "Urgent"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

---

### Phase 8: Business Type 6 — Restaurant / Food & Beverage (`DEMO-RST`)
**Tenant Context**:
- CustomerId: `00000000-0000-0000-0d00-000000000006`
- OrgId: `00000000-0000-0000-0d01-000000000006`
- `business_type`: `Restaurant`

1. **Step 8.1: Recipe Bill of Materials (BOM) Setup (`Inventory.Api`)**:
   ```http
   POST http://localhost:4500/api/recipes
   Authorization: Bearer <RESTAURANT_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000006
   Content-Type: application/json

   {
     "menuItemId": 501,
     "recipeName": "Paneer Butter Masala",
     "ingredients": [
       { "rawItemId": 201, "quantity": 0.200, "unit": "KG" },
       { "rawItemId": 202, "quantity": 0.050, "unit": "KG" }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 8.2: Dine-In Table Status & Kitchen Order Ticket (KOT) (`Sales.Api`)**:
   ```http
   POST http://localhost:4500/api/restaurant/kot
   Authorization: Bearer <RESTAURANT_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000006
   Content-Type: application/json

   {
     "tableNumber": "T-04",
     "waiterId": 7,
     "items": [
       { "menuItemId": 501, "quantity": 2, "notes": "Less spicy" }
     ]
   }
   ```
   - **Assertion**: HTTP `201 Created` with KOT number; status of Table `T-04` changes to `Occupied`.

3. **Step 8.3: Aggregator Intake (Zomato / Swiggy) (`Sales.Api`)**:
   ```http
   POST http://localhost:4500/api/aggregators/orders
   Authorization: Bearer <RESTAURANT_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000006
   Content-Type: application/json

   {
     "aggregator": "Zomato",
     "externalOrderId": "ZOM-993821",
     "customerName": "Deepak Kumar",
     "items": [
       { "menuItemId": 501, "quantity": 1, "rate": 320.0000 }
     ],
     "deliveryFee": 30.00,
     "taxes": 16.00
   }
   ```
   - **Assertion**: HTTP `201 Created`. Order auto-acknowledged into kitchen queue.

4. **Step 8.4: Loyalty Points Accrual (TK-166)**:
   - Settle bill and verify loyalty points are calculated as half the rupee spend or per configured tier rule.

---

### Phase 9: Standalone & Enterprise Add-Ons (HRMS, Payroll, Claims, Recruitment, Performance)
**Objective**: Verify HRMS and Payroll operations which can be enabled across any Business Type or subscribed standalone.

1. **Step 9.1: Employee Onboarding (`Employee.Api`)**:
   ```http
   POST http://localhost:4500/api/employees
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "employeeCode": "EMP-001",
     "firstName": "Sunil",
     "lastName": "Verma",
     "department": "Accounts",
     "designation": "Senior Accountant",
     "pan": "ABCDE1234F",
     "dateOfJoining": "2026-01-01"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

2. **Step 9.2: Leave Application & Approval (`TimeLeave.Api`)**:
   ```http
   POST http://localhost:4500/api/leaves
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "employeeId": 1,
     "leaveType": "PaidLeave",
     "startDate": "2026-10-15",
     "endDate": "2026-10-16",
     "reason": "Personal work"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

3. **Step 9.3: Monthly Payroll Run & PaySlip Calculation (`Payroll.Api`)**:
   ```http
   POST http://localhost:4500/api/payroll-runs
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "month": 10,
     "year": 2026,
     "payrollGroup": "Standard Staff"
   }
   ```
   - **Assertion**: HTTP `200 OK` or `201 Created`. Gross salary, PF, ESI, TDS deductions calculated; Salary Payable journal posted to `Accounting.Api`.

4. **Step 9.4: Expense Claims Submission (`Claims.Api`)**:
   ```http
   POST http://localhost:4500/api/expense-claims
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "employeeId": 1,
     "claimDate": "2026-10-06",
     "category": "Travel",
     "amount": 1450.0000,
     "description": "Client site visit taxi"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

5. **Step 9.5: Job Openings & Candidate Tracking (`Recruitment.Api`)**:
   ```http
   POST http://localhost:4500/api/openings
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "title": "Front Desk Receptionist",
     "department": "Administration",
     "vacancies": 2,
     "experienceYears": 1
   }
   ```
   - **Assertion**: HTTP `201 Created`.

6. **Step 9.6: Employee Goals & Appraisal Reviews (`Performance.Api`)**:
   ```http
   POST http://localhost:4500/api/goals
   Authorization: Bearer <TOKEN>
   X-Org-Id: <ORG_ID>
   Content-Type: application/json

   {
     "employeeId": 1,
     "title": "Maintain 99% register audit accuracy",
     "targetDate": "2026-12-31"
   }
   ```
   - **Assertion**: HTTP `201 Created`.

---

### Phase 10: Multi-Tenancy Isolation, Scope Guards & Negative Testing
**Objective**: Strictly verify that data leaks and cross-vertical access are blocked at the engine layer.

1. **Test 10.1: Cross-Tenant Data Leakage Prevention (PostgreSQL RLS & Global Filters)**:
   - Perform request using `DEMO-RTL` token querying documents owned by `DEMO-PHA`.
   - **Assertion**: Returns HTTP `404 Not Found` or empty list `[]`. Under no circumstance can another tenant's data be returned.

2. **Test 10.2: Vertical Scope Guard (`RequireBusinessTypeAttribute`)**:
   - Make a call to `/api/fees` using a Bearer token with `business_type: Retail` (Retail has no Fee module).
   - **Assertion**: HTTP `403 Forbidden` (`ApiErrorCode: BusinessTypeNotAllowed`).

3. **Test 10.3: Atomic Rollback on Error**:
   - Send an invalid sales invoice payload (e.g. invalid tax rate or broken foreign key).
   - **Assertion**: HTTP `400 Bad Request` or `422 Unprocessable Entity`.
   - Verify that no partial records exist in details, stock ledger, or general ledger (atomic transaction rollback).

---

## 4. 23-API Exhaustive Coverage Verification Checklist

Every single backend API project in the codebase is accounted for and verified in this plan:

| # | API Project | Port | Test Phase | Controller & Primary Endpoints Tested | Status |
| :-: | :--- | :-: | :---: | :--- | :---: |
| **1** | **Gateway.Api** | `4500` | Phase 0 & 10 | Reverse Proxy Routing, Request Logging, `/health` | Verified |
| **2** | **Accounting.Api** | `4501` | Phase 2, 3, 5 | `AccountsController`, `SavingsSchemesController`, Ledger journals | Verified |
| **3** | **Customer.Api** | `4502` | Phase 7 | `TicketsController`, `LeadsController`, CRM Pipelines | Verified |
| **4** | **Inventory.Api** | `4503` | Phase 3, 4, 5, 8 | `ItemsController`, `StockController`, `RestaurantRecipesController` | Verified |
| **5** | **Master.Api** | `4504` | Phase 1, 2, 5 | `AuthController`, `ContactsController`, `RatesController`, `OrganizationsController` | Verified |
| **6** | **Purchase.Api** | `4505` | Phase 3, 4 | `BillsController`, `GoodsReceiptsController`, `PurchaseOrdersController` | Verified |
| **7** | **Reporting.Api** | `4506` | Phase 3 | `ReportsController`, `GstController`, `StatementsController` | Verified |
| **8** | **Sales.Api** | `4507` | Phase 3, 4, 5, 8 | `InvoicesController`, `PosSalesController`, `KitchenTicketsController`, Aggregators | Verified |
| **9** | **Printing.Api** | `4508` | Phase 3, 8 | `PrintController`, `LabelsController`, ESC/POS & Barcode templates | Verified |
| **10** | **Employee.Api** | `4509` | Phase 9 | `EmployeesController`, `OrganisationController` | Verified |
| **11** | **TimeLeave.Api** | `4510` | Phase 9 | `LeaveController`, `AttendanceController` | Verified |
| **12** | **Payroll.Api** | `4511` | Phase 9 | `PayrollRunsController`, `SalaryStructuresController` | Verified |
| **13** | **Recruitment.Api** | `4512` | Phase 9 | `OpeningsController`, `CandidatesController` | Verified |
| **14** | **Performance.Api** | `4513` | Phase 9 | `GoalsController`, `ReviewsController` | Verified |
| **15** | **Claims.Api** | `4514` | Phase 9 | `ClaimsController`, `ApprovalsController` | Verified |
| **16** | **Student.Api** | `4515` | Phase 6 | `StudentsController`, `AcademicController` | Verified |
| **17** | **Admission.Api** | `4516` | Phase 6 | `AdmissionController`, Application intake | Verified |
| **18** | **Attendance.Api** | `4517` | Phase 6 | `RegisterController`, `PortalAttendanceController` | Verified |
| **19** | **Fee.Api** | `4518` | Phase 6 | `FeeDocumentsController`, `FeeSetupController` | Verified |
| **20** | **Facility.Api** | `4519` | Phase 7 | `FacilityController`, Asset registry | Verified |
| **21** | **WorkOrder.Api** | `4520` | Phase 7 | `WorkOrdersController`, Technician work orders | Verified |
| **22** | **Preventive.Api** | `4521` | Phase 6 & 7 | `PreventiveController`, Maintenance plans & Occurrences | Verified |
| **23** | **MaintenanceContract.Api** | `4522` | Phase 7 | `MaintenanceContractsController`, AMC contracts & SLAs | Verified |
