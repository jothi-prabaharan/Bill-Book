# Signup & provisioning

**Status: built.** Public self-service signup that provisions an entire tenant.

## What the form collects

- **You** — name, email, mobile, password
- **Company** — company name, organization name, financial-year start month, base currency
- **Location** — country, state (dependent dropdown), city, postal code
- **Statutory**, all optional at signup — GSTIN, PAN, TAN, TIN, CIN, Udyam

When a GSTIN is supplied, its first two digits are validated against the chosen state's GST code. A mismatch silently breaks CGST/SGST vs IGST determination later, so it is checked at entry.

## What the server does

```
POST /api/customers/signup   → 202 Accepted
```

1. Create the Customer with a generated `CustomerCode` (10 digits, zero-padded)
2. Create a **Trial licence automatically** — 14 days, 3 users, 1 organization. The customer never picks it.
3. Create the first Organization and enable its base currency, active
4. Queue provisioning and return immediately

The background provisioner then:

1. `CREATE DATABASE … ENCODING 'UTF8'` — UTF-8 matters, it is why Tamil and Chinese work
2. Store the tenant connection string in the secret store
3. Create the owner user with the Owner role, through Identity's internal API
4. Publish `CustomerProvisioned` so each service migrates its own schema and seeds its data
5. Flip the Customer to Trial and the database to Ready

## Why login is blocked until it finishes

Creating a database is not instant, so signup is **eventually consistent**. The screen polls:

```
GET /api/customers/{id}/status   → { canLogin: false | true }
```

`canLogin` only becomes true when the customer is active **and** the database is Ready. Logging in earlier would hit a database that does not exist yet.

If provisioning fails, the tenant directory row is marked `Failed` for an operator to retry — the signup is not silently lost.

## Concurrency

`CustomerCode` is generated read-max-then-increment, which races under simultaneous signups. A unique index on the column arbitrates, and the insert retries on conflict — so two signups landing in the same millisecond get different codes rather than one failing.
