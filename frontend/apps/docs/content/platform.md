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



# Branches

The places you trade from. Each one is a complete set of books.

**Settings › Branches**

## What a branch is here

Your **account is the head office**. Every **branch is an organization** under it, sharing the account's database and separated by its organization id.

That separation is real, not a label. A branch has **its own items, contacts, stock, chart of accounts, tax rates and numbering series**, and nothing crosses between them. Chennai cannot see Bangalore's rows — the query filter and the database's own row-level security both stop it.

The trade this makes: clean independent books per branch, and leakage between them made structurally impossible — at the cost of maintaining master data per branch, and of consolidated reporting being a deliberate read across branches rather than a default.

## Adding one

A branch is not a row you insert. It is a small provisioning.

Creating one writes the branch, then asks every service to set up its books: chart of accounts, GST rates, numbering series, payment terms, contact person roles, unit types, units and metal purities. Until that finishes the branch shows as **Setting up** and cannot be used.

That is deliberate. A branch handed over half-created cannot save an item — saving one requires a unit type — so it stays visibly unfinished rather than looking ready and failing at the first thing you try. If a service could not be reached, the branch waits with a **Finish setup** action.

**Finish setup adds only what is missing**, so it is safe to press at any time — on a half-provisioned branch, on a branch set up last year, on one that is already complete, where it does nothing. It is also how a branch created before a new default existed gets it: when a GST rate or a unit is added to what we ship, running setup again on an older branch brings it in without touching anything already there.

What is yours stays yours. Rows are matched on their internal name rather than their label, so a payment term you renamed is recognised as already present and never duplicated back under its original wording, and anything you added yourself is left alone. Two cases are skipped rather than forced: a default we ship whose name you have already used for something of your own, and a unit type whose base unit you have changed — the conversion factors we ship are relative to the original base, and adding them against a different one would silently misstate stock.

No new database is created. Branches share the account's.

## Branch code

Short, up to ten characters: `HO`, `CHN`, `BLR2`. It is read aloud and typed.

It also goes into generated document numbers when a numbering series is set to include it, so `INV/2526/CHN/00042` says where it was written. The code is **copied onto the series** rather than read back each time, so renaming a branch later does not restyle numbers already issued.

## GSTIN and state

Set a GSTIN on a branch that holds its own registration — typically because it is in another state.

**Its first two digits must match the branch's state**, and saving is refused otherwise. Same rule as a contact's GSTIN, for the same reason: those digits are the state code, and a mismatch splits every document's tax the wrong way — CGST + SGST where IGST belongs — with nothing complaining until filing.

## Base currency and financial year

**The base currency is fixed once the branch exists.** Every amount posted in that branch is converted to it, so changing it later would restate the entire set of books. It is editable only while creating.

The financial year start month drives the year segment in generated numbers — April for India.

## Switching between branches

**Switch to** moves you into another branch without signing out. You get a new session carrying that branch and the permissions you hold *there* — permissions are per branch, so the same person can be an accountant in one and a viewer in another.

The page reloads on switching, deliberately: everything on screen belongs to the branch you just left.

Only branches you have been given access to appear.

## Adding a branch beyond your licence

Your **licence** covers a number of branches — a trial covers one. Adding one beyond that is **not refused**. It is created, seeded and usable, on its own **30-day trial**, and marked *Trial* in the list.

That is deliberate. A branch is a complete set of books, and nobody can judge one from an empty screen: it has to be set up, its masters adjusted and a month traded through it. Thirty days rather than the account's fourteen, because a fortnight does not cover a monthly cycle.

The trial is a **cap, not an extension**. Login enforces whichever ends first, so a trial branch under a licence expiring next week stops next week. When it ends the branch stops and everything in it is kept; your other branches are unaffected, and there is nothing to renew on the account itself. Adding a licence for the branch clears the trial.

Nothing takes payment yet, so nothing clears the flag automatically.

## Suspending and deleting

**The first branch cannot be suspended.** The account would have nowhere to sign in to.

Branches are never deleted. Their documents, ledger rows and stock all live under the branch's id, and removing it would leave that history belonging to nothing. Suspending takes a branch out of use and leaves everything intact.

## Editing the branch you are in

**Settings › Organization** edits the branch you are signed in to, in three tabs:

- **Profile** — code, name, address, contact details, website, logo.
- **Statutory** — GSTIN, PAN, TAN, TIN, CIN and Udyam number. The GSTIN's first two digits must match the state on Profile; the form says so before the save is refused.
- **Financial** — financial year start month. The base currency is shown but fixed: every posting in the branch converts to it, so changing it after anything has been posted would restate the books.

Which branch is taken from your sign-in rather than the address, so it is always the one you are working in. To edit a different one, switch to it from Branches.

TAN, TIN, CIN, Udyam number, website and logo had no screen at all before this — they could be set at signup and never corrected, which is not how a CIN or an MSME registration arrives.

## When a branch's access ends

Every branch carries its **own end date**, set when the branch is created and taken from the account's licence at that moment. It is checked at every sign-in, alongside the licence.

It is a **cap, not a replacement**. Whichever of the two ends first is the one that applies, so a branch can never outlive the licence paying for it — and a branch can be wound down early without touching the account everyone else works in. A seasonal counter, a franchise leaving, a location closing: the branch stops and nothing else does.

Signing in still works. You land on a page saying **this branch has closed**, with the date, and you sign out and pick another branch — the switcher lives on a settings page, and settings pages are behind the same check that stopped you. The wording is deliberately different from an expired licence: your account is fine, so there is nothing to renew and nobody should be sent to a billing page.

Branches created before this existed have no end date of their own, and follow the account's licence exactly as they always did.

> **Renewing the licence does not move the branch dates.** Each branch holds its own copy, taken when it was created. Extending the licence without extending the branches leaves them closed under an account that is perfectly valid. There is no renewal screen yet; when there is, it has to move both.



# Authentication

**Status: built** (backend and screens). Email OTP works; SMS does not — see the caveat below.

## Two-step login

One account can span several organizations, so login is two calls.

```
POST /api/auth/login              email + password
  → pre-auth token (5 min, no org context) + the orgs you can reach

POST /api/auth/select-organization  X-PreAuth-Token header + orgId
  → access token (15 min) + refresh token (7 days)
```

The client skips step two automatically when there is exactly one organization.

The access token carries `sub`, `customer_id`, `org_id`, `display_name`, `permission[]`, `license_status` and `license_expiry`.

## Password rules

- Hashed with **BCrypt, work factor 12** — one-way, never encrypted, never recoverable
- **5 failed attempts → 15-minute lockout** (`FailedLoginCount`, `LockedOutUntil`)
- Every attempt, success or failure, writes a `LoginHistories` row
- The error message is deliberately generic — it never says which field was wrong

## Forgot password (OTP)

Three steps: request → verify → reset.

1. `POST /api/auth/forgot-password` — **always returns the same 200 and always advances to the code screen**, whether or not the account exists. Revealing that would let anyone enumerate your users.
2. `POST /api/auth/verify-otp` — 6-digit code, **10-minute expiry**, **locks after 5 wrong tries**. Only the SHA-256 hash of the code is stored.
3. `POST /api/auth/reset-password` — re-hashes the password and **revokes every refresh token**, so all other sessions end.

> **SMS is not wired.** The stack has SMTP for email but no SMS provider, so the mobile channel is specced and modelled but does not deliver. Keep the mobile option hidden until a provider is chosen.

## Invitations

Invited users get a **tokenised link, never a temporary password**. Until they complete it, `PasswordHash` is empty and login is refused. The invite token lives 7 days; a reset OTP lives 10 minutes.

## Secret handling

The rule: **hash what you only verify, encrypt only what you must replay.**

| Secret | Method |
|---|---|
| Login password | Hash (BCrypt) |
| Refresh token, OTP code, invite token | Hash (SHA-256) |
| **SMTP password** | **Encrypt (AES)** — the mail server needs the real value |
| Tenant connection string | Key Vault reference, never in the database |

The SMTP password is the only encrypted secret in the system, and that is deliberate.



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
3. Create the owner user with the Owner role
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



# Licensing & trial expiry

**Status: built.**

## The licence

One row per customer, created automatically at signup:

| Field | Trial default |
|---|---|
| `LicenseType` | Trial |
| `ExpiryDate` | signup + 14 days |
| `MaxUsers` | 3 |
| `MaxOrganizations` | 1 |
| `GraceDays` | 0 |

A licence is expired when `today > ExpiryDate + GraceDays`. Expiry is evaluated **lazily**, the first time an org context is resolved, and stamped onto the customer — no nightly job is required.

## Expiry blocks the app, never the login

This is the important rule, and it is deliberate.

An expired customer **still authenticates normally**. The tokens issue, and the access token carries `license_status: "Expired"`. What changes is what they can reach:

- A route guard sits above every feature route. An expired licence **cancels navigation and renders an empty "Trial expired" page** — so typing `/accounting/journal` directly lands there, not on the journal.
- The only live routes are the expiry page, billing/upgrade and logout.
- **Every feature API also returns `403 LicenseExpired`.** The guard is the UX half; the API check is the real boundary. Hand-crafting a request gets you nothing.

Letting login itself fail would have been simpler to build and worse to use — the customer could not see why they were locked out, or reach a renew button.

## User limit

Inviting a user checks `MaxUsers` and returns `409` with an upgrade prompt when the cap is reached, rather than creating a user that cannot log in.



# Email & invitations

**Status: built.** Real SMTP delivery, sent on a background worker.

## SMTP settings

`Settings → Email`. One platform-wide default mailbox, and an optional per-customer override so a customer can send from its own address.

| Field | Notes |
|---|---|
| Host, Port, SSL | e.g. `smtp.gmail.com`, 587, on |
| From address / name | What recipients see |
| Username | Usually the same as the from address |
| **Password** | **Write-only** — see below |
| Active | An inactive row is skipped |

### The password is the one encrypted secret

Everywhere else a secret is stored — login passwords, refresh tokens, OTP codes, invitation tokens — it is **hashed**, one-way, because we only ever need to verify it. An SMTP password is different: the mail server needs the real value, so it must be recoverable.

It is stored **AES-GCM encrypted** with a 32-byte key held outside the database — configuration in development, Key Vault in production. Keeping the key beside the ciphertext would defeat the purpose.

The API **never returns it**. The screen shows `••••••` when one is stored and sends a value only when you type a new one; leaving the field blank keeps the stored password. That also means a client cannot accidentally echo it back.

**Send test email** proves the credentials before anything depends on them. It sends **inline** rather than queued, so a bad host or password reports the actual error instead of failing silently in the background.

## Where credentials live, and why sending is centralised

Master owns `mst.SmtpSettings`, so **Master does the sending**. Services that are not Master post the message to an internal endpoint:

```
POST /internal/notifications/email   { toEmail, subject, htmlBody, ... }
```

Invitations and OTP codes go the same way. The decrypted password never leaves the process that holds the settings — and since auth and SMTP are one service now, sending an invitation does not cross a boundary at all.

## Sending happens in the background

An SMTP round-trip can take seconds and can fail. Blocking an invite request on it would make the UI feel broken and lose the invitation if the mail server hiccuped.

So `IEmailSender` **queues** and returns immediately; a background worker drains the queue and delivers, retrying transient failures at **2s, 10s, then 30s**. A message that still fails is logged and dropped — invitations and codes can both be re-requested, so retrying forever gains nothing.

The queue is in-process today, which means a restart loses anything still queued. That is an acceptable trade for these message types and swaps for Service Bus behind the same interface when the Notification worker lands.

**Bodies are never logged.** They carry invitation links and OTP codes, so only the subject and recipient appear in logs.

## Inviting a user

`Settings → Users → Invite user`. Collects email, name, optional mobile, and a role.

1. Creates the user with **no password** and `EmailConfirmed = false`
2. Assigns the role for the current organization
3. Issues a **7-day invitation token** (only its hash is stored) and queues the email
4. Refuses with `409` when the licence's user limit is reached

The invitee follows the link to `/accept-invitation`, sets a password, and the email is confirmed in the same step. Until then the user cannot sign in — an empty password hash fails verification outright.

**No temporary password is ever created.** A resend issues a fresh token and invalidates the previous link.

> **Mobile verification is not implemented.** The mobile number is collected and stored, and the OTP tables model an SMS channel, but there is no SMS provider wired — only email delivers. The mobile option stays hidden until one is chosen.

## Revoking access

Revoking deactivates the organization assignment rather than deleting the user, so history and audit trails survive. Two guards: you cannot revoke yourself, and you cannot revoke the last active Owner of an organization.



