# Bill-Book Backend Testing Plan: 5-API Batches

**Status**: Active Execution Plan  
**Target Architecture**: .NET 10 Microservices + YARP API Gateway + PostgreSQL Multi-Tenancy  
**Gateway URL**: `http://localhost:4500`  
**Batching Strategy**: 5 APIs per testing wave to optimize local machine memory, CPU, port availability, and logical dependency order.

---

## 1. Strategy & Batching Roadmap

Running all 23 API services simultaneously during local development can strain local system resources and clutter test diagnostics. This plan partitions the 23 backend services into **5 cohesive batches (5 APIs per batch)**, progressing from the foundational commerce backbone to domain-specific verticals and maintenance operations:

```
[Wave 1: APIs 1–5]  ──► Core Foundation & Commerce Backbone (Gateway, Master, Accounting, Inventory, Sales)
         │
[Wave 2: APIs 6–10] ──► Procurement, CRM, Output & Employee Master (Purchase, Customer, Printing, Reporting, Employee)
         │
[Wave 3: APIs 11–15]──► HRMS Lifecycle & Workforce Management (TimeLeave, Payroll, Claims, Recruitment, Performance)
         │
[Wave 4: APIs 16–20]──► School SIS & Campus Facilities (Student, Admission, Attendance, Fee, Facility)
         │
[Wave 5: APIs 21–23]──► Facility Maintenance & Operations (WorkOrder, Preventive, MaintenanceContract + Worker)
```

---

## 2. Batch Breakdown & Test Specifications

---

### Batch 1: Core Foundation & Commerce Backbone (APIs 1–5)

> **Services Active**:
> 1. `Gateway.Api` (Port `4500`)
> 2. `Master.Api` (Port `4511`)
> 3. `Accounting.Api` (Port `4501`)
> 4. `Inventory.Api` (Port `4509`)
> 5. `Sales.Api` (Port `4519`)

*This batch forms the minimum viable commercial ERP engine. It is the exact set launched by `./scripts/start-backend.ps1`.*

#### Pre-requisites:
- PostgreSQL running locally on `localhost:5432` with database `EP_Admin`.
- Demo customers seeded (`DEMO-RTL` org: `00000000-0000-0000-0d01-000000000001`).

#### Step-by-Step API Execution:

1. **API 1: Gateway Health & Routing (`Gateway.Api`)**
   ```http
   GET http://localhost:4500/health
   ```
   - **Expectation**: HTTP `200 OK`. Gateway is accepting requests and reverse-proxy route table is loaded.

2. **API 2: User Authentication & Token Acquisition (`Master.Api`)**
   ```http
   POST http://localhost:4500/api/auth/login
   Content-Type: application/json

   {
     "email": "owner@example.com",
     "password": "Password123!"
   }
   ```
   - **Expectation**: HTTP `200 OK`. Extract `accessToken` carrying `customer_id`, `org_id`, and `business_type`.

3. **API 3: General Ledger Chart of Accounts (`Accounting.Api`)**
   ```http
   GET http://localhost:4500/api/accounts
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   ```
   - **Expectation**: HTTP `200 OK`. Pre-seeded accounts (Cash, Bank, Sales Revenue, GST Output) are returned.

4. **API 4: Product Creation & Stock Inward (`Inventory.Api`)**
   ```http
   POST http://localhost:4500/api/items
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "code": "B1-ITEM-001",
     "name": "Batch 1 Sample Product",
     "hsnCode": "8471",
     "unitOfMeasure": "PCS",
     "costPrice": 500.0000,
     "sellingPrice": 1200.0000,
     "taxRate": 18.0000
   }
   ```
   - **Expectation**: HTTP `201 Created` with generated `id`.

5. **API 5: Sales Order & Invoice Settlement (`Sales.Api`)**
   ```http
   POST http://localhost:4500/api/sales-invoices
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "customerId": 1,
     "invoiceDate": "2026-10-06",
     "items": [
       { "itemId": 1, "quantity": 5, "rate": 1200.0000, "taxRate": 18.0000 }
     ],
     "payments": [
       { "method": "Cash", "amount": 7080.0000 }
     ]
   }
   ```
   - **Expectation**: HTTP `201 Created`. Stock decreases by 5 units in `Inventory.Api`; Sales & GST Output credited in `Accounting.Api`.

#### Batch 1 Sign-Off Gate:
- [ ] Gateway forwards traffic to Master, Accounting, Inventory, Sales without 502/504 errors.
- [ ] Ledger entries balance (Total Debits == Total Credits).
- [ ] Stock adjustments reflect accurately.

---

### Batch 2: Procurement, CRM, Output & Employee Master (APIs 6–10)

> **Services Active**:
> *(Keeps Batch 1 dependencies running or stubbed)*  
> 6. `Purchase.Api` (Port `4516`)  
> 7. `Customer.Api` (Port `4505`)  
> 8. `Printing.Api` (Port `4515`)  
> 9. `Reporting.Api` (Port `4518`)  
> 10. `Employee.Api` (Port `4506`)  

#### Step-by-Step API Execution:

1. **API 6: Vendor Invoicing & Bill Booking (`Purchase.Api`)**
   ```http
   POST http://localhost:4500/api/bills
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "vendorId": 1,
     "billNumber": "VEND-B2-001",
     "billDate": "2026-10-06",
     "items": [
       { "itemId": 1, "quantity": 50, "rate": 500.0000, "taxRate": 18.0000 }
     ]
   }
   ```
   - **Expectation**: HTTP `201 Created`. Stock moves +50 in `Inventory.Api`; Accounts Payable credited.

2. **API 7: Lead Intake & Support Ticket (`Customer.Api`)**
   ```http
   POST http://localhost:4500/api/leads
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "contactName": "Sunil Sharma",
     "companyName": "Sharma Retail Ltd",
     "email": "sunil@sharma.example",
     "phone": "+91 9811223344",
     "estimatedValue": 150000.00
   }
   ```
   - **Expectation**: HTTP `201 Created` with lead status `New`.

3. **API 8: ESC/POS Thermal Receipt & Barcode Print (`Printing.Api`)**
   ```http
   POST http://localhost:4500/api/print/barcodes
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "barcode": "8901234567890",
     "format": "Code128",
     "labelTitle": "Batch 1 Sample Product",
     "mrp": 1200.00
   }
   ```
   - **Expectation**: HTTP `200 OK` returning rendered image or raw print binary.

4. **API 9: Certified Financial & GST Reporting (`Reporting.Api`)**
   ```http
   GET http://localhost:4500/api/reports/sales-register?fromDate=2026-10-01&toDate=2026-10-06
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   ```
   - **Expectation**: HTTP `200 OK` showing invoice generated in Batch 1.

5. **API 10: Staff Member Onboarding (`Employee.Api`)**
   ```http
   POST http://localhost:4500/api/employees
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "employeeCode": "EMP-101",
     "firstName": "Ravi",
     "lastName": "Kumar",
     "department": "Operations",
     "designation": "Inventory Manager",
     "dateOfJoining": "2026-10-01",
     "pan": "ABCDE1234F"
   }
   ```
   - **Expectation**: HTTP `201 Created` with generated `id`.

#### Batch 2 Sign-Off Gate:
- [ ] Vendor bill creates corresponding inventory movement and ledger liability.
- [ ] Reporting accurately extracts cross-service data from Accounting and Sales.
- [ ] Employee record is created with active employment state.

---

### Batch 3: HRMS Lifecycle & Workforce Automation (APIs 11–15)

> **Services Active**:
> 11. `TimeLeave.Api` (Port `4521`)  
> 12. `Payroll.Api` (Port `4512`)  
> 13. `Claims.Api` (Port `4504`)  
> 14. `Recruitment.Api` (Port `4517`)  
> 15. `Performance.Api` (Port `4513`)  

#### Step-by-Step API Execution:

1. **API 11: Leave Application & Approval (`TimeLeave.Api`)**
   ```http
   POST http://localhost:4500/api/leaves
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "employeeId": 1,
     "leaveType": "CasualLeave",
     "startDate": "2026-10-10",
     "endDate": "2026-10-11",
     "reason": "Family function"
   }
   ```
   - **Expectation**: HTTP `201 Created` with status `PendingApproval`.

2. **API 12: Salary Setup & Monthly Payroll Run (`Payroll.Api`)**
   ```http
   POST http://localhost:4500/api/payroll-runs
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "month": 10,
     "year": 2026,
     "payrollGroup": "All Staff"
   }
   ```
   - **Expectation**: HTTP `200 OK` or `201 Created`. Computes earnings, PF, ESI, TDS, and posts Salary Payable journal.

3. **API 13: Staff Expense Claim Submission (`Claims.Api`)**
   ```http
   POST http://localhost:4500/api/expense-claims
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "employeeId": 1,
     "claimDate": "2026-10-06",
     "category": "ClientTravel",
     "amount": 2400.0000,
     "description": "Train tickets for client store audit"
   }
   ```
   - **Expectation**: HTTP `201 Created`.

4. **API 14: Job Requisition & Candidate Pipeline (`Recruitment.Api`)**
   ```http
   POST http://localhost:4500/api/openings
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "title": "Junior Accountant",
     "department": "Finance",
     "vacancies": 1,
     "experienceYears": 2
   }
   ```
   - Follow up with candidate application:
   ```http
   POST http://localhost:4500/api/candidates
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "openingId": 1,
     "fullName": "Pooja Mehta",
     "email": "pooja.mehta@example.com",
     "phone": "+91 9876501234"
   }
   ```
   - **Expectation**: HTTP `201 Created`.

5. **API 15: Appraisal Goals & Performance Review (`Performance.Api`)**
   ```http
   POST http://localhost:4500/api/goals
   Authorization: Bearer <TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000001
   Content-Type: application/json

   {
     "employeeId": 1,
     "title": "Complete Q4 stock audit with zero discrepancies",
     "targetDate": "2026-12-31"
   }
   ```
   - **Expectation**: HTTP `201 Created`.

#### Batch 3 Sign-Off Gate:
- [ ] Employee attendance & leaves link correctly with payroll calculation.
- [ ] Payroll engine computes accurate deductions and books payroll liabilities.
- [ ] Claims, recruitment, and performance review workflows execute without error.

---

### Batch 4: School SIS & Campus Facilities (APIs 16–20)

> **Services Active**:
> 16. `Student.Api` (Port `4520`)  
> 17. `Admission.Api` (Port `4502`)  
> 18. `Attendance.Api` (Port `4503`)  
> 19. `Fee.Api` (Port `4508`)  
> 20. `Facility.Api` (Port `4507`)  

*Tenant Context: `DEMO-SCH` (OrgId: `00000000-0000-0000-0d01-000000000004`, `business_type`: `School`)*

#### Step-by-Step API Execution:

1. **API 16: Academic Structure & Student Master (`Student.Api`)**
   ```http
   POST http://localhost:4500/api/students
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "admissionNo": "SCH-2026-101",
     "firstName": "Aarav",
     "lastName": "Patel",
     "grade": "Grade 6",
     "section": "A",
     "dateOfBirth": "2015-08-14"
   }
   ```
   - **Expectation**: HTTP `201 Created`.

2. **API 17: Admission Application Intake (`Admission.Api`)**
   ```http
   POST http://localhost:4500/api/inquiries
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "applicantName": "Ananya Sharma",
     "parentName": "Rajesh Sharma",
     "contactPhone": "+91 9844001122",
     "targetGrade": "Grade 1",
     "academicYear": "2026-2027"
   }
   ```
   - **Expectation**: HTTP `201 Created` with inquiry status `Registered`.

3. **API 18: Student Daily Attendance Register (`Attendance.Api`)**
   ```http
   POST http://localhost:4500/api/student-attendance
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "date": "2026-10-06",
     "grade": "Grade 6",
     "section": "A",
     "records": [
       { "studentId": 101, "status": "Present" }
     ]
   }
   ```
   - **Expectation**: HTTP `200 OK` or `201 Created`.

4. **API 19: Term Fee Collection & Dues (`Fee.Api`)**
   ```http
   POST http://localhost:4500/api/fee-collections
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "studentId": 101,
     "term": "Term 2",
     "amountPaid": 30000.0000,
     "paymentMode": "UPI",
     "referenceNo": "UPI-SCH-998822"
   }
   ```
   - **Expectation**: HTTP `201 Created`. Fee receipt created and tuition fees credited.

5. **API 20: School Asset & Physical Facility Registry (`Facility.Api`)**
   ```http
   POST http://localhost:4500/api/assets
   Authorization: Bearer <SCHOOL_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000004
   Content-Type: application/json

   {
     "assetCode": "SCH-BUS-04",
     "name": "School Bus 40-Seater",
     "location": "Parking Bay 2",
     "serialNumber": "TN-01-AB-4321"
   }
   ```
   - **Expectation**: HTTP `201 Created`.

#### Batch 4 Sign-Off Gate:
- [ ] Student admission converts into active student record.
- [ ] Attendance registers persist without missing entries.
- [ ] Fee collection generates receipt and marks outstanding student dues as settled.

---

### Batch 5: Facility Maintenance & Operations (APIs 21–23 + Worker)

> **Services Active**:
> 21. `WorkOrder.Api` (Port `4522`)  
> 22. `Preventive.Api` (Port `4514`)  
> 23. `MaintenanceContract.Api` (Port `4510`)  
> *(+ `BillBook.Worker` Background Service & Security Scope Engine)*

*Tenant Context: `DEMO-SVC` (OrgId: `00000000-0000-0000-0d01-000000000005`, `business_type`: `Service`)*

#### Step-by-Step API Execution:

1. **API 21: Technician Work Order Dispatch (`WorkOrder.Api`)**
   ```http
   POST http://localhost:4500/api/work-orders
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "title": "Repair Air Compressor Leak",
     "assetId": 1,
     "assignedTechnicianId": 15,
     "priority": "High",
     "scheduledDate": "2026-10-08"
   }
   ```
   - **Expectation**: HTTP `201 Created` with status `Assigned`.

2. **API 22: Preventive Maintenance Plan & Occurrences (`Preventive.Api`)**
   ```http
   POST http://localhost:4500/api/preventive/plans
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "title": "Quarterly Generator Servicing",
     "frequency": "Quarterly",
     "assignedRole": "ChiefEngineer",
     "checklistItems": [
       "Inspect engine oil levels",
       "Test battery voltage",
       "Clean air filters"
     ]
   }
   ```
   - **Expectation**: HTTP `200 OK` or `201 Created`. Next scheduled occurrence calculated.

3. **API 23: Annual Maintenance Contract (AMC) & SLA (`MaintenanceContract.Api`)**
   ```http
   POST http://localhost:4500/api/maintenance-contracts
   Authorization: Bearer <SERVICE_TOKEN>
   X-Org-Id: 00000000-0000-0000-0d01-000000000005
   Content-Type: application/json

   {
     "contractNumber": "AMC-CORP-2026",
     "customerId": 5,
     "startDate": "2026-10-01",
     "endDate": "2027-09-30",
     "contractValue": 250000.0000,
     "slaResponseHours": 2,
     "coveredAssetIds": [ 1 ]
   }
   ```
   - **Expectation**: HTTP `201 Created`.

4. **Background Worker Execution (`BillBook.Worker`)**
   - Verify outbox notifications, rate synchronization, and payment reminder background jobs process queued messages.
   - **Expectation**: Worker logs reflect processed messages without unhandled exceptions.

5. **Cross-Engine Tenancy & Security Scope Audit**
   - Attempt calling `WorkOrder.Api` or `Fee.Api` with an unauthorized tenant or wrong `business_type`.
   - **Expectation**: HTTP `403 Forbidden` (`ApiErrorCode: BusinessTypeNotAllowed`). Zero row leakage across organizations.

#### Batch 5 Sign-Off Gate:
- [ ] Maintenance contract links to covered assets.
- [ ] Work order dispatch and status progression complete smoothly.
- [ ] ScopeGuard and RLS security policies reject cross-vertical or cross-tenant intrusion.

---

## 3. Quick Reference Matrix (5 APIs per Batch)

| Batch | Batch Name | APIs Included | Ports | Target Business Vertical |
| :---: | :--- | :--- | :---: | :--- |
| **Batch 1** | **Core Commerce Backbone** | Gateway, Master, Accounting, Inventory, Sales | 4500, 4511, 4501, 4509, 4519 | Retail (`DEMO-RTL`) / All |
| **Batch 2** | **Procurement & Operations** | Purchase, Customer, Printing, Reporting, Employee | 4516, 4505, 4515, 4518, 4506 | Retail, Pharmacy, All |
| **Batch 3** | **HRMS & Payroll** | TimeLeave, Payroll, Claims, Recruitment, Performance | 4521, 4512, 4504, 4517, 4513 | Enterprise / All Add-Ons |
| **Batch 4** | **School SIS & Education** | Student, Admission, Attendance, Fee, Facility | 4520, 4502, 4503, 4508, 4507 | School (`DEMO-SCH`) |
| **Batch 5** | **Maintenance & Operations** | WorkOrder, Preventive, MaintenanceContract (+ Worker) | 4522, 4514, 4510 | Service (`DEMO-SVC`) / Infrastructure |
