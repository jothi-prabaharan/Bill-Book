# Reporting Module

**Schema:** N/A (Read-only queries across `acc`, `inv`, `sal`, `pur`)

## Overview
Generates business reports, tax compliance documents (GST), and financial statements (P&L, Balance Sheet, Trial Balance).

## Task Checklist
- [ ] **1.1 — Profit & Loss Statement:** Read from `acc` ledger.
- [ ] **1.2 — Balance Sheet:** Read from `acc` ledger.
- [ ] **2.1 — Tax (GST) Reporting:** GSTR-1, GSTR-2, GSTR-3B outputs.
- [ ] **3.1 — Inventory Valuation Report:** Weighted average reporting from `inv` layers.
- [ ] **3.2 — Sales Register:** Export `sal.SalesRegister` data.
