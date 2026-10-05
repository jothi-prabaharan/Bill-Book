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

Each branch declares its trade, its **business type**: **General**, **Pharma**, **Jewellery**, **School**, **Service** or **Restaurant**. The trade decides what the branch is set up with, which screens its menu offers and which add-ons it can switch on. It is chosen on **Settings › Branches** and on **Settings › Organization settings**.

| | General | Pharma | Jewellery | School | Service | Restaurant |
|---|---|---|---|---|---|---|
| Metal purities and **Settings › Metal purity** | yes | no | yes | no | no | no |
| The Restaurant menu and **Settings › Restaurant** | no | no | no | no | no | yes |
| The school screens | no | no | no | yes | no | no |

General is the branch that has everything a trading business needs: a shop selling a little of everything should not be missing the one screen it needs. Pharma, Jewellery and Restaurant are trading branches too, with items, sales, purchases and stock. A Service branch is set up like a General one for now; hiding the stock screens it does not need is still to be built.

**The trade can be changed at any time, and changing it deletes nothing.** Switching a Pharma branch to Jewellery adds the metal purities it did not have, once. Switching back only hides the Metal purity screen; the purities stay, because stock may still be priced in them.

### Add-ons

A branch switches on the add-ons its trade allows. Turning one off hides its screens and deletes nothing.

| Add-on | Allowed for |
|---|---|
| HRMS, Payroll | every trade, and a customer with no trade at all |
| CRM, Fixed assets, Client portal, Budget, Recurring billing | every trade |
| POS | General, Pharma and Jewellery |
| E-invoicing | General, Pharma, Jewellery and Restaurant |
| Savings scheme | Jewellery |

Recurring billing is free. The trade and the add-ons are in the access token, so every service refuses a route that is not for the branch.

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
