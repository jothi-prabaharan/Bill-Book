# Tenancy model

Two nested boundaries, enforced by different mechanisms. Getting these confused is the most damaging mistake available in this codebase.

## Head office and branch

Two levels, and only two.

- **Customer** — the **head office**. The account, the billing relationship, the licence. Shares **one physical database** with every other customer.
- **Organization** — a **branch**. One place you trade from, and one complete set of books: its own code, GSTIN, address and currency. A head office owns **many**, all sharing its database.

| Boundary | Enforced by |
|---|---|
| Head office ↔ head office | `CustomerId` + EF Core global query filter + Postgres RLS |
| Branch ↔ branch | `CustomerId` + `OrgId` + EF Core global query filter + Postgres RLS |

Both boundaries are enforced the same way now — the only difference is which column the filter checks. Before, a customer's own database was the isolation; now every table also carries `CustomerId`, checked alongside `OrgId` in the same query filter, the same row-level security policy and the same `set_config` call. Defence in depth rather than a single physical wall: `OrgId` alone was already globally unique, so `CustomerId` doubles the check rather than replacing it.

**A branch is a hard data boundary, not a tag on a transaction.** Each branch keeps its own items, contacts, stock, chart of accounts and numbering series, and nothing crosses between them. Chennai cannot see Bangalore's rows, because the query filter and the row-level security policy both stop it.

That is the trade this model makes. It gives each branch clean, independent books and makes cross-branch leakage structurally impossible — at the cost of maintaining master data per branch, and of consolidated reporting being a deliberate read across organizations rather than a default.

**There is no separate branch table, and no `BranchId` column.** `OrgId` *is* the branch. One briefly existed and was removed: it duplicated the organization almost column for column while only `OrgId` scoped anything.

Branches are created and switched between from **Settings › Branches** — see [Branches](organizations).

## Which database holds what

Two physical databases, and only two — both shared by every customer:

- **The master database** (`mst` — countries, states, currencies and the seeded reference masters; customers, organizations, licences, SMTP, configuration; users, roles, permissions, tokens. `rat` — currency and metal rate history.)
- **The tenant database** — every customer's branches, together: `con` `cus` `inv` `sal` `pur` `acc` `rpt` `ntf`.

There is no longer a database per customer. Every table in the tenant database carries `CustomerId` and `OrgId`, and both are checked on every query.

## Per-request resolution

```
JWT → customer_id, org_id
    → set_config('app.current_customer_id', <customer>, true)
    → set_config('app.current_org_id', <org>, true)
```

Both are **transaction-local**, never connection-level. Connections are pooled and reused across requests; setting tenant context on the connection would leak it to the next caller. There is no connection to resolve first — every service opens the one tenant database at startup, the same way it already opened the one master database.

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

## The branch's trade

Each branch declares its trade: **General**, **Pharma** or **Jewellery**. The trade decides what the branch is set up with and which screens its menu offers.

| | General | Pharma | Jewellery |
|---|---|---|---|
| Metal purities seeded | yes | no | yes |
| **Settings › Metal purity** in the menu | yes | no | yes |

Everything else is the same for every trade. General is the branch that has everything: a shop selling a little of everything should not be missing the one screen it needs.

**The trade can be changed at any time, and changing it deletes nothing.** Switching a Pharma branch to Jewellery adds the metal purities it did not have, once. Switching back only hides the Metal purity screen; the purities stay, because stock may still be priced in them.

## Branch code

Short, up to ten characters: `HO`, `CHN`, `BLR2`. It is read aloud and typed.

It also goes into generated document numbers when a numbering series is set to include it, so `INV/2526/CHN/00042` says where it was written. The code is **copied onto the series** rather than read back each time, so renaming a branch later does not restyle numbers already issued.

## GSTIN and state

Set a GSTIN on a branch that holds its own registration — typically because it is in another state.

**Its first two digits must match the branch's state**, and saving is refused otherwise. Same rule as a contact's GSTIN, for the same reason: those digits are the state code, and a mismatch splits every document's tax the wrong way — CGST + SGST where IGST belongs — with nothing complaining until filing.

## Base currency and financial year

**The base currency is fixed once the branch exists.** Every amount posted in that branch is converted to it, so changing it later would restate the entire set of books. It is editable only while creating.

The financial year start month drives the year segment in generated numbers — April for India.

## How dates and amounts are displayed

Every screen draws dates, quantities and amounts the way the branch expects, and none of them decides that for itself. The settings come from the server when the app loads:

| What | Where it comes from |
|---|---|
| Date pattern | The `format.date` setting, `dd/MM/yyyy` by default |
| Currency symbol, and which side it sits | The branch's base currency |
| Digit grouping | The base currency's grouping mask |
| Decimal places on money | The base currency |
| Decimal places on quantities and unit prices | The `quantity.decimals` and `unitPrice.decimals` settings |

**Grouping follows the currency rather than a global preference.** The rupee groups by lakh and crore — ₹12,34,567.00 — and the dollar groups in thousands, and that difference is carried on the currency itself. A branch that changes its base currency gets the right grouping without anyone changing a setting.

**Only the date pattern is a setting of its own**, because a date belongs to the branch rather than to a currency. Everything else was already recorded against the currency or the existing decimal settings, and duplicating it would have given two places to change one answer.

Until the server answers, screens show the shipped defaults — Indian grouping, the rupee, `dd/MM/yyyy` — rather than blanks.

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

The SMTP password is the only encrypted secret in the system, and that is deliberate.



# Signup & provisioning

**Status: built.** Two ways in — public self-service signup, and platform admin — both provisioning a customer into the one shared tenant database rather than creating one of its own.

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
4. Create the owner user with the Owner role
5. Seed every service's master data into the shared tenant database, scoped to the new `CustomerId` and `OrgId` — chart of accounts, GST rates, numbering series, payment terms, contact roles, unit types, units and metal purities
6. Flip the Customer and its Organization to Active/Trial

Steps 4–6 run **synchronously**, in the same request — there is no database to create and wait on any more, so there is nothing left that has to happen in the background. The response still says `202 Accepted` because seeding several services is not instant, and the client still polls rather than assuming success.

## Why login is blocked until it finishes

The screen polls:

```
GET /api/customers/{id}/status   → { canLogin: false | true }
```

`canLogin` only becomes true once every service has confirmed its seed. If a service could not be reached mid-seed, the customer is left at **Provisioning** — or, for a public signup with no way back in to retry, at a terminal **Failed** — rather than handed a login that leads to a chart of accounts with nothing in it.

## Retrying a stuck signup

A public signup that lands at `Failed` has no second visit of its own — there is nobody signed in to press a retry button. **Platform admin** is where that gets fixed:

`GET /api/admin/customers` lists every customer with its status; a customer at **Provisioning** or **Failed** shows a **Retry setup** action that re-runs the seed. Every seed is idempotent, so retrying is safe no matter how far a previous attempt got — it only adds what a service is still missing. `GET /api/admin/customers/{id}/organizations` shows a customer's branches read-only, for diagnosing a stuck account without needing its owner's password.

The same screen can also create a customer directly — the same `SignupAsync` the public form calls, so the two can never seed a customer differently, minus the public form's own statutory and address fields, which are the customer's to fill in once they can sign in.

**platform.view and platform.edit gate this API**, and only a **platform operator** holds them. Being an operator is a flag on the user, never a role. Roles are shared system rows rather than per-customer copies, so a role carrying `platform.*` would grant it to that role's holders in every customer. An operator's token carries every `platform.*` permission beside their role's own. Nobody else's does, a customer's Owner included, even if a role row somehow names one.

There are two ways to become an operator:
- **At startup.** Every existing user whose address is in `Bootstrap:OperatorEmails` (comma- or semicolon-separated) becomes one at each Master start. This is how the first operator exists. On a single PC (`deploy/local`) it defaults to the owner's address. The setting only grants: removing an address revokes nobody, so a typo cannot lock every operator out.
- **From another operator**, through `GET` and `PUT api/admin/platform-operators/{userId}`. An operator cannot revoke themselves. A change takes effect at the user's next sign-in or token refresh.

## Exchange and metal rates

The **Rates** screen in the admin app keeps the history of exchange rates and metal rates per gram. The rates live in the `rat` schema of the master database: `rat.ExchangeRates` and `rat.MetalRates`.

- **They are global.** Every customer's documents read the same rows, so only a platform operator can enter or remove a rate. Listing the history needs `platform.view` and entering needs `platform.edit`.
- **Rates are looked up on or before a date.** `GET api/rates/exchange?from=USD&to=INR&on=2026-09-24` and `GET api/rates/metal?metal=Gold&purity=22K&on=…` return the latest rate on or before that date, or 404 when there is none. Any signed-in user may call them. A rate entered for Monday answers for Tuesday, and for every later day, until a newer one is entered.
- **A hand-entered rate outranks a fetched one on the same date.** A wrong figure from RBI or IBJA is corrected by entering the right one. Nothing needs deleting. Only a hand-entered rate can be removed.
- **A pair has a direction.** `USD → INR` is one US dollar in rupees. Nothing is inverted automatically.
- **A document keeps the rate it used.** It does not look the rate up again later, so correcting a rate never reprices a document already raised.

**The daily RBI fetch is written but not yet trusted (TK-26).** `RateSync.Worker` reads RBI's reference-rate page each afternoon, after 13:45 India time. It adds each rate against INR, dated as the page dates it, with the source `Rbi`. A rate already on file for that pair, date and source is left alone, so a second run that day writes nothing. Every attempt is recorded in `rat.RateFetchRuns`. A failed attempt writes no rate and is left with follow-up status **Open**, and the next hourly check tries again. **The parser has only been tested against an imitation of the page**, because the page could not be fetched when it was written. It is not deployed anywhere until it has been checked against a real copy.

IBJA's metal rates (TK-25) are not built. Until they are, metal rates are entered here by hand.

## Concurrency

`CustomerCode` is generated read-max-then-increment, which races under simultaneous signups. A unique index on the column arbitrates, and the insert retries on conflict — so two signups landing in the same millisecond get different codes rather than one failing.



# Licensing & trial expiry

**Status: built.**

## Apps

A customer can buy four apps: **RetailErp**, **School**, **HRMS** and **Payroll**. Each is licensed on its own. The branches, users and settings are shared by every app.

- **A role belongs to one app.** Owner of RetailErp and Owner of Payroll are different roles. Every role that existed before apps were added is a RetailErp role.
- **A permission can belong to several apps.** The settings permissions (users, roles, branches, organization settings, currencies, configuration, email, API keys and numbering) belong to every app. Every other permission belongs to RetailErp for now.
- **A role can be given only permissions that belong to its app.** Saving a role with another app's permission is refused, and nothing is changed.
- **The menu marks each screen with the apps that show it.** The Home and Settings screens are in every app. Everything else is RetailErp's.

### Signing in is per app

- **A sign-in is for one app.** The login and branch-selection requests name the app, and a request that names none is for RetailErp. Only branches where you hold a role in that app are offered.
- **The token names its app** in an `app` claim. It carries only the permissions of your roles in that app, and the status and expiry of that app's licence.
- **Each app has its own licence.** If the RetailErp trial lapses, Payroll keeps working, and the account is marked expired only when every app's licence has lapsed. Signing in to an app the customer has no licence for works, but the licence status is *NotLicensed*, so the app is closed, like an expired one.
- **Switching app is switching branch.** `POST /api/auth/switch-organization` with an `app` mints a token for the same branch in the other app. Without an `app`, it stays in the current one. Each app's refresh tokens are a family of their own.
- **Every service checks the app.** Each controller names the apps it serves, and a token from any other app gets **403**, even when it holds a permission with the same name. This matters because permissions are shared: `settings.view` is every app's. The settings, users, roles, branches, numbering and print-template services are open to every app. Contacts are open to RetailErp and School. Everything else is RetailErp's.
- **`GET /api/me/context`** returns the signed-in session for pages that don't read the token: your name and email, the branch, the app, that app's licence, your permissions, and the apps you can switch to in this branch. It returns no internal ids.
- **Inside an app, the roles screen shows only that app's roles**, and a role created there belongs to that app. Inviting a user counts against the user limit of the invited role's app.

### Signing up, and starting another app

- **Each app has its own signup.** Signing up creates the customer, the first branch, the owner, and a 14-day trial of the app you signed up from. The owner gets that app's **Owner** role. Each app has a seeded Owner role, and it holds every permission the app allows.
- **Start another app from Settings › Applications.** **Start trial** adds that app's 14-day trial and makes you its Owner in every branch. It also sets up what the app needs in every existing branch. Nothing is created twice: it is the same customer, branches and users. If a branch cannot be set up, nothing is started and you can try again.
- **What a branch is set up with follows the apps you hold.** Accounting and print templates are set up for every app, because payroll and fees post to the books and every app prints. Items, sales, purchases, reports, support and contacts are set up for RetailErp. HRMS, Payroll and School add their own as they are built.

### The HRMS and Payroll apps

`apps/hrms` and `apps/payroll` are their own web apps. Each signs in to its own app, draws its own menu, and mounts the same shared settings pages as RetailErp: users, roles, branches, organization settings, currencies, configuration, email, API keys, print templates, number series and applications. Their own screens arrive with each HRMS and Payroll stage. For now each opens on a home page that says so.

- **Switching between apps** is under the branch switcher. The addresses come from the deployment's `config.js` (`appUrls: { RetailErp: '…', Hrms: '…', Payroll: '…' }`). On a developer's machine they default to the dev servers: web on 4200, HRMS on 4203 and Payroll on 4204. An app served from the same address as another shares its sign-in, and the shell switches the token to its own app on the same branch. An app at a different address asks you to sign in.

### Pages check what you may open

- **Each app's menu shows only that app's screens**, and only those you hold a permission for.
- **A page you cannot open says why.** A typed URL or an old bookmark to a page you lack the permission for opens a *No access* page, which names the permission to ask your administrator for. It no longer sends you silently to the dashboard. An expired, suspended or unlicensed app goes to the expired page, as before.
- **Settings › Applications** lists the four apps and your licence for each: *Active*, *Trial*, *Expired*, *Suspended* or *Not started*. Someone who can edit settings can press **Start trial** on an app you don't have yet.
- **Switching app** is under the branch switcher at the top: it lists the other apps you hold a role in, in this branch.
- **Settings › Number series is now a settings screen for every app.** Opening or changing it takes the settings permissions instead of the accounting ones, matching the menu, which already listed it under settings. An accountant without settings permissions no longer reaches it by a typed URL.
- For developers: every page under the shell must declare `data.access`, either `{ permission: 'x.view' }` or `{ signedIn: true }`. A page that declares nothing is refused, and each app's route spec (`auditShellRoutes`) fails the build. Apps mount the shell with `shellRoutes({ app, children })`, which attaches the guards. `*bbIfCan="'x.edit'"` hides a button the same way.

## The licence

One row per customer per app (`GET /api/customers/{id}/licenses/apps` lists them), created automatically at signup:

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



