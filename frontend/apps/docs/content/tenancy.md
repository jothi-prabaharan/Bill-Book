# Tenancy model

Two nested boundaries, enforced by different mechanisms. Getting these confused is the most damaging mistake available in this codebase.

## Customer vs Organization

- **Customer** — the account and billing entity. Owns **one physical database**.
- **Organization** — a set of books with its own GSTIN, currency and branches. A Customer owns **many**, all sharing that Customer's database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer | Separate physical databases |
| Organization ↔ Organization | `OrgId` + EF Core global query filter + Postgres RLS |

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
    → tenant directory (plt.CustomerDatabases, cached)
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
