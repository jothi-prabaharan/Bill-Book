# Reporting Module

**Schema:** `rpt` (report catalog and saved layouts) + read-only queries across `acc`, `inv`, `sal`, `pur`, `con`

> The common report grid and the catalog of 45 reports with their columns live in
> [`docs/architecture/REPORTS.md`](../architecture/REPORTS.md). The checklist below
> covers the statements and GST returns, which are not grid reports.

## Overview
Generates business reports, tax compliance documents (GST), and financial statements (P&L, Balance Sheet, Trial Balance).

## Task Checklist
- [x] **1.1 — Profit & Loss Statement:** Read from `acc` ledger.
- [x] **1.2 — Balance Sheet:** Read from `acc` ledger.
- [ ] **2.1 — Tax (GST) Reporting:** GSTR-1, GSTR-2, GSTR-3B outputs.
- [x] **3.1 — Inventory Valuation Report:** Weighted average reporting from `inv` layers.
- [ ] **3.2 — Sales Register:** Export `sal.SalesRegister` data.

