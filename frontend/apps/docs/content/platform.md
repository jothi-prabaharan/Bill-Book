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

Each branch declares its trade, its **business type**: **Retail**, **Pharma**, **Jewellery**, **School**, **Service** or **Restaurant**. The trade decides what the branch is set up with, which screens its menu offers and which add-ons it can switch on. It is chosen on **Settings › Branches** and on **Settings › Organization settings**. Retail was called **General** until 5 October 2026; branches that were General are Retail now, with nothing else changed.

| | Retail | Pharma | Jewellery | School | Service | Restaurant |
|---|---|---|---|---|---|---|
| Items, sales, purchases and the books | yes | yes | yes | no | yes | yes |
| Stock: warehouses, stock adjustments, goods receipts, delivery challans, stock-tracking reports | yes | yes | yes | no | no | yes |
| Metal purities and **Settings › Metal purity** | yes | no | yes | no | no | no |
| The Restaurant menu and **Settings › Restaurant** | no | no | no | no | no | yes |
| The school screens | no | no | no | yes | no | no |

Retail is the branch that has everything a trading business needs: a shop selling a little of everything should not be missing the one screen it needs. Pharma, Jewellery and Restaurant are trading branches too.

**A Service branch holds no stock.** It sells work and time: its items are services with a SAC code that track no inventory, so saving an item that tracks stock is refused there, and a new item starts as an untracked service. It keeps quotes, sales orders, invoices, credit notes, purchase orders, bills, debit notes, contacts and every accounting and reporting screen.

**The trade can be changed at any time, and changing it deletes nothing.** Switching a Pharma branch to Jewellery adds the metal purities it did not have, once. Switching back only hides the Metal purity screen; the purities stay, because stock may still be priced in them.

### Add-ons

A branch switches on the add-ons its trade allows. Turning one off hides its screens and deletes nothing.

| Add-on | Allowed for |
|---|---|
| HRMS, Payroll | every trade, and a customer with no trade at all |
| CRM, Fixed assets, Client portal, Budget, Recurring billing | every trade |
| POS | Retail, Pharma and Jewellery |
| E-invoicing | Retail, Pharma, Jewellery and Restaurant |
| Savings scheme | Jewellery |

Recurring billing is free. The trade and the add-ons are in the access token, so every service refuses a route that is not for the branch.

## One product, scoped by branch

There are no apps any more (TK-136). A customer buys one product, and what each branch shows follows its **business type** and **add-ons**, above. HRMS, Payroll and School are no longer bought or signed into separately: School is a business type, HRMS and Payroll are add-ons. Settings › Applications, where another app's trial used to be started, is gone.

### Roles

- **One role list per customer.** A role is not tied to a trade or an add-on, so the same Owner, Accountant or custom role works in every branch. The five system roles, and School's Principal, Office Admin, Teacher and Maintenance, are system roles every customer shares.
- **The roles screen offers what the branch can use.** The permission grid shows the permissions the current branch's business type and add-ons allow, by the same rule the menu uses: a School branch is not offered sales permissions, and a branch without Payroll is not offered payroll ones. A role keeps permissions it already holds when it is edited from a branch that does not offer them.
- **A permission does nothing where its screens are hidden.** Every service still checks the branch's business type and add-ons, so a sales permission held in a School branch opens nothing there.

### Signing in

- **A sign-in is for a branch.** You are offered every branch where you hold a role, once, whatever roles you hold in it.
- **The token carries the branch's scope**: its `business_type` and `add_ons` claims, the permissions of every role you hold in that branch, and the customer's licence. It carries no `app` claim. A token minted before 5 October 2026 that still names an app, or says `General`, reads as a Retail branch until it expires.
- **`GET /api/me/context`** returns your name and email, the branch, its business type and add-ons, the licence and your permissions. It returns no internal ids.
- **Keep me signed in.** Ticked, you stay signed in after closing the browser, for as long as you come back within the refresh token's seven days; each visit renews it. Unticked, a reload or a new tab keeps you signed in, but closing the browser signs you out. Either way the access token lasts fifteen minutes and is renewed quietly, including when the app is reopened after a break.

### Switching branch, and the frame around every page

- **The branch switcher groups branches by organisation (TK-189).** The group holding your current branch is open, and opening another closes it. Each branch shows its business type, organisation and currency. Searching matches the branch, organisation, role, GSTIN or city, and opens every group so nothing is hidden. `GET /api/auth/organizations`, `GET /api/auth/session` and the login response carry `customerName`, `businessType` (a branch with none reads as `Retail`), `baseCurrency`, `gstin` and `city` for each branch, read in one query.
- **The rail** is 64px wide. Its account button shows your initials and your role in the current branch, and opens a menu with your name, role, email and **Logout**. Escape, a click outside or moving to another page closes it.
- **On a phone** the tab bar holds Home, Purchase, Sales and Banking, and **More** holds the rest. The top bar keeps the branch switcher, notifications, help and sign out. New, Search, Favourites and the financial-year tag are desktop only.

### Signing up

- **Signup asks what the business does.** Choose the first branch's kind of business and its add-ons. Pick **HRMS or Payroll only** for a customer that trades nothing, and then tick HRMS, Payroll or both. An add-on the business type does not allow cannot be ticked.
- Signing up creates the customer, the first branch, the owner and a 14-day trial. The owner gets the **Owner** role.
- **What a branch is set up with follows its scope.** Accounting and print templates are set up for every branch, because payroll and fees post to the books and every branch prints. Items, sales, purchases, reports, support and contacts are set up for a branch with a trade. School's services are set up for a School branch, and the employee master for a School branch or one with HRMS or Payroll.

### HRMS, Payroll and School pages

HRMS, Payroll and School are no longer web apps of their own (TK-134). Their pages are part of the web app, shown to a branch whose business type or add-ons allow them: the School pages to a School branch, HRMS and Payroll to a branch with that add-on. The client portal is the web app's `/portal` pages and the platform admin pages are `/admin`.

### Pages check what you may open

- **The menu shows only the screens the branch's scope reaches**, and only those you hold a permission for.
- **A page you cannot open says why.** A typed URL or an old bookmark to a page you lack the permission for opens a *No access* page, which names the permission to ask your administrator for. A page for another kind of business, or for an add-on the branch has not switched on, says that instead. An expired or suspended licence goes to the expired page.
- **Settings › Audit log** offers the logs the branch's scope reaches: the trading services for a trading branch, School's for a School branch, and HRMS's and Payroll's for their add-ons.
- **Settings › Number series is a settings screen.** Opening or changing it takes the settings permissions instead of the accounting ones, matching the menu, which lists it under settings.
- For developers: every page under the shell must declare `data.access`, either `{ permission: 'x.view' }` or `{ signedIn: true }`, optionally with `businessTypes` or `addOns`. A page that declares nothing is refused, and the route spec (`auditShellRoutes`) fails the build. The web app mounts the shell with `shellRoutes({ children })`, which attaches the guards. `*bbIfCan="'x.edit'"` hides a button the same way.

## Kinds of customer

Every customer is one of three kinds (TK-152):

| Kind | What it is | How it is made | Licence |
|---|---|---|---|
| **Standard** | A paying customer | Public signup, or a platform operator | A 14-day trial, then a paid licence that can lapse |
| **Internal** | Our own organisation, with real books | Only a platform operator, or the first start of an install | Elite, never lapses |
| **Demo** | A customer for testing and showing the product | Only a platform operator | Elite, never lapses |

Public signup always makes a Standard customer, whatever the request says. An Internal or Demo customer is never expired, never stamped *Expired*, never billed or reminded to pay, and is not held to a user or branch limit: its new branches carry no expiry and are never trial branches.

**The Internal customer is the one an install creates on its first start** (TK-153), from `Bootstrap:OwnerEmail`. Its head office, branch `HO`, is a **Service** business. Its add-ons at that first start come from `Bootstrap:InternalAddOns` (for example `Hrms, Payroll`; none by default); any a Service branch cannot have are left off and logged. An install from before 5 October 2026 already has that first customer as a Standard one: the next start makes it Internal, makes its head office a Service branch and removes every expiry, once. Later starts change nothing, so a business type or add-on changed afterwards on **Settings › Branches** stays as it was set.

**Demo customers** (TK-154) are one per business type — `DEMO-RTL` (Retail), `DEMO-PHA` (Pharma), `DEMO-JWL` (Jewellery), `DEMO-SCH` (School), `DEMO-SVC` (Service) and `DEMO-RST` (Restaurant). Each has one head-office branch of that type with every add-on the type allows switched on, and is set up with **master data only**: the chart of accounts, tax rates, units, numbering series, reports, print templates and the walk-in customer every branch gets, and no items, contacts or documents.

- A platform operator makes them with **Create demo customers** on the admin **Customers** page. Running it again creates nothing new; it finishes any demo a service was too busy to set up, and the page says which.
- An install can make them on start with `Bootstrap:SeedDemoCustomers` (off by default, so a production install never gets them by accident). Master tries a minute after starting, and again each minute until every service has answered, up to ten times.
- For local development there is also the **sample customer** (TK-185), `SAMPLE`. It is a single Demo customer with one branch per business type and one sample user who is Owner of all of them, made on start by `Bootstrap:SeedSampleData`. See [Running locally](development).
- Each owner's sign-in is a plus-address of one mailbox, `Bootstrap:DemoOwnerEmail` (or `Bootstrap:OwnerEmail`): `you@example.com` becomes `you+demo-retail@example.com`, so every reset lands in one inbox. Owners are created with no password; set one with **Forgot password**. No password is kept in the source. The platform admin's **Customers** page shows each customer's kind, filters by it, and lets an operator choose the kind and the first branch's business type when adding one.

## The licence

One row per customer (`GET /api/customers/{id}/licenses`), created automatically at signup:

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




# Approvals

**Status: partial.** The approval engine is built. Leave is the first request that uses it. Purchase, accounting and sales documents, and the screens for workflows and the approvals inbox, come next.

Every app uses one approval engine. A leave request in HRMS and a purchase bill in RetailErp go through the same steps, with the same rules and the same messages.

## Workflows

A **workflow** says who approves one kind of request in a branch. It is a list of **levels**, approved in order. Each level names who approves it:

| Approver | Who that is |
|---|---|
| **Manager** | The employee's manager, or their manager's manager, and so on up the chain. |
| **Relationship** | The person named as the employee's *Lead* or *Project Lead* (a branch can add more). |
| **Department head** | The head of the employee's department. A head's own request goes to the head of the department above. |
| **Role** | Anyone with a role in the branch. The first of them to act takes the step. |
| **Named employee** or **named user** | One fixed person. RetailErp, which has no employees, uses named users and roles. |

A level can apply only **above an amount**, can be **optional**, and can **require a comment** to approve.

A branch can keep several workflows for one kind of request. The one that names the most of the requester's department, grade and location is used. When two match equally, the one that took effect most recently is used. An HRMS branch starts with one workflow for leave and one for leave encashment, each approved by the employee's manager.

The workflows are managed through `api/approval-workflows` (*Settings: view* and *Settings: edit*). The screen for them comes with the approvals inbox.

## When a request is submitted

The workflow is turned into a list of steps, and the steps are stored with the request. **Changing a workflow afterwards never moves a request already submitted.** It keeps the approvers it was given.

Some levels are skipped when the steps are made:

- a level whose approver is the person asking, because **nobody approves their own request**;
- a level whose approver has just approved the level before, because **nobody approves twice in a row**;
- an **optional** level with no one to approve it, such as a manager for someone who has none, or a manager who has left or has no login.

A **required** level with no one to approve it stops the submission. The message names the level, so an administrator can fix it.

## Approving

One step is waiting for a decision at a time. The approver can:

- **approve**: the next level is asked, and the last approval approves the request;
- **reject**: the request ends. A reason is required;
- **send back**: the level before looks at it again, or the person who asked, if it was the first level. A reason is required.

Someone away can name a **delegate** for a range of dates. The delegate can then act on their steps, but never on a step that any holder of a role may take.
