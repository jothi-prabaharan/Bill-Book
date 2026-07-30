# Bill-Book

A multi-tenant retail ERP and accounting product for Indian SMBs. Zoho Books is the functional benchmark.

This is the **help and reference site**. It is a static app — markdown files, no API and no database — so it can be hosted anywhere and read offline.

## What this documents

Pages describe what is **actually built**. A page tagged `partial` or `planned` in the sidebar means the feature is not finished; the page says what exists and what does not, rather than describing an aspiration as if it worked.

## Where to start

- **[Architecture](#/architecture)** — the twelve services, schemas and how they talk
- **[Tenancy model](#/tenancy)** — customers, organizations and the database-per-customer rule
- **[Build status](#/status)** — an honest list of what is done
- **[Running locally](#/running-locally)** — get it up on your machine

## The two rules that shape everything

1. **A Customer owns one physical database; an Organization is a set of books inside it.** Every per-customer table carries `OrgId`, and a missing query filter leaks data between organizations.
2. **Everything posts through a journal.** Invoices, bills, payments, depreciation and opening balances all become ledger rows — nothing writes to the general ledger directly.
