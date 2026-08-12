# Tenancy model

Two nested boundaries, enforced by different mechanisms. Getting these confused is the most damaging mistake available in this codebase.

## Head office and branch

Two levels, and only two.

- **Customer** — the **head office**. The account, the billing relationship, the licence. Owns **one physical database**.
- **Organization** — a **branch**. One place you trade from, and one complete set of books: its own code, GSTIN, address and currency. A head office owns **many**, all sharing its database.

| Boundary | Enforced by |
|---|---|
| Head office ↔ head office | Separate physical databases |
| Branch ↔ branch | `OrgId` + EF Core global query filter + Postgres RLS |

**A branch is a hard data boundary, not a tag on a transaction.** Each branch keeps its own items, contacts, stock, chart of accounts and numbering series, and nothing crosses between them. Chennai cannot see Bangalore's rows, because the query filter and the row-level security policy both stop it.

That is the trade this model makes. It gives each branch clean, independent books and makes cross-branch leakage structurally impossible — at the cost of maintaining master data per branch, and of consolidated reporting being a deliberate read across organizations rather than a default.

**There is no separate branch table, and no `BranchId` column.** `OrgId` *is* the branch. One briefly existed and was removed: it duplicated the organization almost column for column while only `OrgId` scoped anything.

Branches are created and switched between from **Settings › Branches** — see [Branches](organizations).

## Which database holds what

The **master database** is shared by every customer:

- `mst` — countries, states, currencies, and the seeded reference masters
- `plt` — customers, organizations, licences, tenant directory, SMTP, configuration
- `idn` — users, roles, permissions, tokens
- `rat` — currency and metal rate history

Every other schema is **replicated per customer database**: `con` `crm` `inv` `sal` `pur` `acc` `bnk` `sup` `rpt` `ntf`.

## Per-request resolution

```
JWT → customer_id
    → tenant directory (mst.CustomerDatabases, cached)
    → connection string from Key Vault
    → set_config('app.current_org_id', <org>, true)
```

The last step is **transaction-local**, never connection-level. Connections are pooled and reused across requests; setting org context on the connection would leak it to the next caller.

## Cross-database references

Postgres cannot enforce a foreign key across databases, so these are plain ids validated in C#:

- `CreatedBy` / `ModifiedBy` — users live in the master database
- `acc.Accounts.AccountTypeId` → `mst.AccountTypes`
- Contacts referencing countries and states

Resolve the display names in **batches** — a naive per-row lookup is an N+1 on every list screen.
