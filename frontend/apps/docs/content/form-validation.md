# Form validation

**Status: built.**

All major create/edit forms now validate mandatory fields **before** sending the API request.

## What changed

- Required fields are now checked in the submit handler, not only by disabled buttons.
- Nested rows in composite forms (contacts, items, money documents) are validated row-by-row before save/post.
- The user now gets an immediate, field-specific message instead of a round-trip failure from the server.

## Coverage

Validation guards were added for these UI areas:

- **Accounting**: chart of accounts, numbering series, payment terms, tax master
- **Banking**: banks, bank accounts, spend/receive money, transfer money
- **Master**: contacts, contact person roles, roles, users
- **Inventory**: categories, items, metal purities, warehouses
- **Settings**: organizations, organization settings, currencies, SMTP settings

The API remains authoritative, but the client now blocks obvious mandatory-field misses earlier.
