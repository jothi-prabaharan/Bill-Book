# Customer / Contacts Module

**Schema:** `con`

## Overview
Manages customer and vendor profiles, contact roles, prepayments, and outstanding balances. Coordinates with the Accounting module's AR/AP subledgers.

## Task Checklist
- [x] **0.1 — Schema design:** `con.Contacts` and role indexes.
- [x] **1.1 — Initial seeding:** Seed master contacts upon tenant creation.
- [ ] **2.1 — Contact Portal:** Basic self-serve views for statements.
- [ ] **2.2 — Prepayment advance routing:** Link payments to customer prepayment advances rather than specific documents when overpaid.
- [ ] **TBD — Credit limits and holds:** Enforce business rules during sales order generation.
