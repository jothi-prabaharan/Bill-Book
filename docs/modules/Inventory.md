# Inventory Module

**Schema:** `inv`

## Overview
Handles stock levels, reservations, adjustments, and the physical count of inventory. Integrates with Accounting for inventory valuation (weighted average) and Sales/Purchase for stock movements.

## Task Checklist
- [x] **0.1 — Schema design:** `inv.*` tables for stock layers and movements.
- [ ] **1.1 — Stock Reservations:** API to reserve stock for sales orders.
- [ ] **1.2 — Stock decrement:** Guarded release-then-issue in one transaction upon invoicing.
- [x] **9.1 — Stock Adjustments:** Header and lines with reasons and approval routing.
- [x] **9.2 — Physical Count:** Adjustments based on counted quantities.
- [ ] **TBD — Expiry and Batch Tracking:** Manage batch dates and serials during stock movements.
