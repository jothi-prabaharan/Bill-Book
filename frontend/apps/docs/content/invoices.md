# Invoices

The `Invoices` module represents the core billing workflow in the Sales domain (Stage T3.1). 
Unlike Quotes and Sales Orders, Invoices are financial instruments that mutate the General Ledger (`acc.JournalLedger`) and trigger inventory depletion upon posting.

## Features
- Create invoices directly or convert from existing Sales Orders.
- Apply dynamic tax rates (CGST, SGST, IGST, etc.) integrated with the Tax Engine.
- Preview General Ledger impact before finalizing.
- Void invoices using automatic reversing journal entries to preserve audit trails.

## Tenancy
Invoices are partitioned securely by `OrgId`. Cross-branch access is strictly denied at both the application (EF Core Global Query Filters) and database level (Row-Level Security).
