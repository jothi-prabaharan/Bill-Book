# Bill-Book Local API Testing Plan by Business Type

**Status**: Active Test Plan  
**Target Architecture**: .NET 10 Microservices + YARP API Gateway (Reverse Proxy) + PostgreSQL Multi-Tenancy (RLS + Global Query Filters)  
**Gateway URL**: `http://localhost:4500`  
**Direct Service Range**: `http://localhost:4501` – `http://localhost:4522`  

---

## 1. Executive Summary & Objective

This document defines the comprehensive local API testing strategy for **Bill-Book** (RetailErp). It outlines step-by-step API verification procedures organized into sequential testing phases, validating the platform's **23 backend API services** across all **6 supported Business Types** and enterprise add-ons.

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

| Microservice | Direct Port | Gateway Cluster | Key Route Prefixes |
| :--- | :---: | :--- | :--- |
| **Gateway.Api** | `4500` | N/A | `/api/*`, `/health` |
| **Accounting.Api** | `4501` | `accounting` | `/api/accounts`, `/api/journals`, `/api/allocations`, `/api/vouchers` |
| **Customer.Api** | `4502` | `customer` | `/api/leads`, `/api/tickets`, `/api/service-plans` |
| **Inventory.Api** | `4503` | `inventory` | `/api/items`, `/api/stock`, `/api/warehouses`, `/api/recipes`, `/api/restaurant-menu` |
| **Master.Api** | `4504` | `master` | `/api/auth`, `/api/users`, `/api/roles`, `/api/organizations`, `/api/contacts`, `/api/rates` |
| **Purchase.Api** | `4505` | `purchase` | `/api/purchase-orders`, `/api/bills`, `/api/goods-receipts`, `/api/debit-notes` |
| **Reporting.Api** | `4506` | `reporting` | `/api/reports`, `/api/reports/definitions`, `/api/reports/export` |
| **Sales.Api** | `4507` | `sales` | `/api/sales-invoices`, `/api/sales-orders`, `/api/quotations`, `/api/pos`, `/api/aggregators` |
| **Printing.Api** | `4508` | `printing` | `/api/print`, `/api/print/templates`, `/api/print/barcodes` |
| **Employee.Api** | `4509` | `hrm` | `/api/employees`, `/api/departments`, `/api/designations` |
| **TimeLeave.Api** | `4510` | `timeleave` | `/api/attendance`, `/api/leaves`, `/api/shifts` |
| **Payroll.Api** | `4511` | `payroll` | `/api/payroll-runs`, `/api/salary-setup`, `/api/payslips` |
| **Recruitment.Api** | `4512` | `recruitment` | `/api/job-openings`, `/api/candidates`, `/api/interviews` |
| **Performance.Api** | `4513` | `performance` | `/api/appraisals`, `/api/kpis`, `/api/reviews` |
| **Claims.Api** | `4514` | `claims` | `/api/expense-claims`, `/api/claims/approvals` |
| **Student.Api** | `4515` | `sis` | `/api/students`, `/api/academic-years`, `/api/classes` |
| **Admission.Api** | `4516` | `admission` | `/api/inquiries`, `/api/applications`, `/api/admissions` |
| **Attendance.Api** | `4517` | `attendance` | `/api/student-attendance`, `/api/attendance-registers` |
| **Fee.Api** | `4518` | `fee` | `/api/fee-structures`, `/api/fee-collections`, `/api/student-dues` |
| **Facility.Api** | `4519` | `facility` | `/api/facilities`, `/api/assets`, `/api/locations` |
| **WorkOrder.Api** | `4520` | `workorder` | `/api/work-orders`, `/api/work-order-tasks`, `/api/technicians` |
| **Preventive.Api** | `4521` | `preventive` | `/api/preventive-schedules`, `/api/preventive-audits` |
| **MaintenanceContract.Api** | `4522` | `amc` | `/api/maintenance-contracts`, `/api/slas`, `/api/amc-renewals` |

---

## 3. Phased Testing Execution Plan

---

### Phase 0: Local Environment Bootstrap & Health Check
**Objective**: Ensure local PostgreSQL instance is running, admin migrations are applied, and all microservices are responding.

1. **Verify Services Status**:
   - Execute PowerShell backend bootstrapper:
     ```powershell
     ./scripts/start-backend.ps1
     ```
   - Verify health checks through Gateway:
     ```http
     GET http://localhost:4500/health
     ```
   - **Expectation**: HTTP `200 OK`.

2. **Verify Database Allocation & Seeding**:
   - Inspect PostgreSQL database `EP_Admin` and ensure connection string credentials match `Host=localhost;Port=5432;Database=EP_Admin;Username=postgres;Password=123`.
   - Ensure demo customers are populated via `DemoCustomerRepository.EnsureAllAsync` or endpoint `/api/admin/demo-seed`.

---

### Phase 1: Authentication, Token Context & Tenant Scope Guards
**Objective**: Authenticate as a user, acquire JWT bearer token, and inspect token claims (`CustomerId`, `OrgId`, `business_type`, `permissions`).

1. **Sign-In Endpoint**:
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
   - **Assertion**: HTTP `201 Created` with generated `id`.

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
   - **Assertion**: HTTP `200 OK` returning printable raster image / PDF stream.

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

3. **Step 7.3: Service Ticket & Work Order Dispatch (`Customer.Api` & `WorkOrder.Api`)**:
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
   - Convert to Work Order:
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

### Phase 9: Standalone & Enterprise Add-Ons (HRMS, Payroll, Claims)
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

## 4. Test Execution Summary & Sign-Off Checklist

| Phase | Description | Verticals Involved | Target APIs | Pass/Fail Criteria |
| :---: | :--- | :--- | :--- | :--- |
| **0** | Infrastructure & Health | All | Gateway, All APIs | Gateway `/health` = 200, DB reachable. |
| **1** | Auth, Seeding & Master | All | Master.Api | JWT token valid, demo tenants seeded. |
| **2** | Contacts & Chart of Accounts | All | Master.Api, Accounting.Api | Contacts created, COA seeded. |
| **3** | Retail Vertical | `DEMO-RTL` | Inventory, Sales, Purchase, Printing | Standard stock cycle & POS checkout pass. |
| **4** | Pharmacy Vertical | `DEMO-PHA` | Inventory, Purchase, Sales | Batches, expiry rejection & prescription pass. |
| **5** | Jewellery Vertical | `DEMO-JWL` | Inventory, Sales, Master (Rates), Accounting | Metal rate sync, hallmark & schemes pass. |
| **6** | School Vertical | `DEMO-SCH` | Admission, Student, Attendance, Fee | Inquiry -> Admission -> Attendance -> Fees pass. |
| **7** | Services Vertical | `DEMO-SVC` | Customer, Facility, AMC, WorkOrder, Preventive | Asset -> AMC -> Ticket -> Work Order pass. |
| **8** | Restaurant Vertical | `DEMO-RST` | Inventory, Sales (KOT), Printing | Tables, KOT, BOM ingredient depletion pass. |
| **9** | Standalone Add-ons | All | Employee, TimeLeave, Payroll, Claims | Employee onboarding -> Leave -> Payroll run pass. |
| **10**| Multi-Tenancy & Security | All | Gateway, Kernel ScopeGuard, RLS | 0 cross-tenant leaks, 403 on mismatched vertical. |
