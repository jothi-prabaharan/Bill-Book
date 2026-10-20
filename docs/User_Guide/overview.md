# Bill-Book

A multi-tenant retail ERP and accounting platform for Indian SMBs.

![Executive Dashboard Overview](assets/screenshots/dashboard-overview.png)

Bill-Book provides complete business management across sales, procurement, inventory, banking, double-entry accounting, Indian GST compliance, and industry-specific workflows.

## System Capabilities

Bill-Book is structured around standard business workflows where every commercial transaction flows cleanly from inquiry to settlement:

- **Sales & Distribution**: Quotes convert to sales orders, orders fulfill via delivery challans, bills generate as GST tax invoices, and sales returns issue credit notes. A fast Point of Sale (POS) interface supports retail counter checkout.
- **Purchasing & Procurement**: Purchase orders track vendor commitments, goods receipts verify warehouse inward delivery, vendor bills verify accounts payable, and returns record debit notes.
- **Inventory & Warehouses**: Real-time stock tracking across multiple warehouses with batch tracking, expiry management, serial tracking, stock adjustments, and weighted-average valuation.
- **Banking & Cash Management**: Bank accounts, statement imports (CSV/XLSX), direct bank reconciliation, spend/receive money vouchers, and fund transfers.
- **Double-Entry Accounting**: Real-time general ledger, journal vouchers, trial balance, balance sheet, profit & loss, foreign currency gains and losses, and period closing locks.
- **Multi-Branch Operations**: Head office oversight with independent books, tax settings, numbering series, and stock per branch.
- **Workforce Management**: Employee profiles, payroll runs, recruitment candidate tracking, and performance reviews.
- **Industry Solutions**: Dedicated vertical configurations for Retail, Pharmaceuticals, Jewellery, Restaurants, Schools, and Service Providers.

---

## Core Operational Principles

1. **Complete Branch Isolation**:
   Your account represents the head office, while each branch operates as an independent organization with its own chart of accounts, tax rates, numbering series, inventory items, and customer balances. No branch can inadvertently view or modify transactions belonging to another branch.

2. **Strict Double-Entry Posting**:
   Every commercial activity that changes financial position—sales invoices, vendor bills, customer receipts, vendor payments, inventory adjustments, and asset depreciation—posts through balanced journal entries. No transaction directly alters the general ledger without a traceable audit trail.

3. **Pre-flight Validation & Integrity**:
   All transactions and master records are validated field-by-field before posting. Mandatory fields, tax splits, debit/credit totals, and credit limits are verified up-front to prevent accounting discrepancies.

---

## Business Modules

| Module | Core Features | Key Outputs |
|---|---|---|
| **Sales** | Quotes, Sales Orders, Delivery Challans, Invoices, Credit Notes, Counter POS | GST Tax Invoices, E-Way Bill data, Delivery Notes |
| **Purchase** | Purchase Orders, Goods Receipts, Bills, Debit Notes | GRN Slips, Payment Schedules, Vendor Statements |
| **Inventory** | Item Master, Warehouses, Stock Adjustments, Price Lists, Metal Purity | Stock Ledger, Valuation Reports, Low-Stock Alerts |
| **Accounting** | Chart of Accounts, Journal Entries, Period Locks, Opening Balances | General Ledger, Trial Balance, Balance Sheet, P&L |
| **Banking** | Bank Accounts, Spend/Receive Money, Statement Import, Reconciliation | Bank Statements, Cash Books, Unreconciled Reports |
| **Reports** | Over 40 standard business and compliance reports | Sales & Purchase Registers, Aging Analysis, GST Reports |
| **Settings** | Organization Profiles, Branch Setup, Numbering Series, Tax Masters, Print Templates | Branded Invoice Templates, Audit Logs |

---

## Navigation & Workspaces

The application interface is organized into a streamlined desktop navigation rail and mobile tabs:

- **Navigation Rail**: Quick access to core modules (Sales, Purchase, Inventory, Accounts, Banking, Reports, Settings).
- **Secondary Panels**: Grouped submenus allow quick exploration within modules, including quick-create shortcuts (`+`) for new transactions.
- **Branch Switcher**: Instantly switch between branches, warehouses, or legal entities without re-authenticating.
- **Responsive Layout**: Adapts from wide-screen multi-column desktop displays down to 360px mobile viewports for floor operations.
