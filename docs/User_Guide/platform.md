# Tenancy & Multi-Branch Model

Bill-Book provides a structured two-tier organization model designed for businesses operating across multiple locations, retail outlets, or business units.

## Head Office and Branches

The platform structures operations into two clean operational boundaries:

- **Customer (Head Office)**: Represents your account, billing relationship, subscription licence, and corporate entity. The head office manages company-wide settings, user accounts, and billing tiers.
- **Organization (Branch)**: Represents an individual trading location, warehouse, retail store, or operating unit. Each branch maintains its own complete, independent set of books: its own GSTIN, business address, local currency, numbering series, chart of accounts, and inventory stock.

| Entity | Functional Scope | Managed Under |
|---|---|---|
| **Head Office** | Global subscription, company profile, global users & roles | Account Settings |
| **Branch / Organization** | Local books, GSTIN, transactions, local warehouse stock, pricing | Settings › Branches |

### Complete Data Isolation Between Branches

A branch is a strict operational and financial boundary. Each branch retains its own:

- **Chart of Accounts and General Ledger**: Every journal entry, invoice, and bill belongs to a specific branch's ledger. Financial books balance independently per branch.
- **Inventory and Warehouses**: Stock levels, stock movements, and item valuations remain distinct.
- **Document Numbering**: Each branch maintains independent sequential numbering prefixes (such as `INV-CHE-0001` for Chennai and `INV-BLR-0001` for Bangalore).
- **Contacts and Balances**: Customer and vendor ledger balances remain strictly segmented by branch.

This architecture ensures that branches cannot accidentally view or cross-contaminate each other's transactions, while the head office retains consolidated visibility across all locations.

Branches are created, managed, and configured under **Settings › Branches**.



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

![Authentication & Sign In](assets/screenshots/login-screen.png)

- **A sign-in is for a branch.** You are offered every branch where you hold a role, once, whatever roles you hold in it.
- **The token carries the branch's scope**: its `business_type` and `add_ons` claims, the permissions of every role you hold in that branch, and the customer's licence. It carries no `app` claim. A token minted before 5 October 2026 that still names an app, or says `General`, reads as a Retail branch until it expires.
- **`GET /api/me/context`** returns your name and email, the branch, its business type and add-ons, the licence and your permissions. It returns no internal ids.
- **Keep me signed in.** Ticked, you stay signed in after closing the browser, for as long as you come back within the refresh token's seven days; each visit renews it. Unticked, a reload or a new tab keeps you signed in, but closing the browser signs you out. Either way the access token lasts fifteen minutes and is renewed quietly, including when the app is reopened after a break.

### Switching branch, and the frame around every page

![Application Navigation & Rail](assets/screenshots/app-navigation.png)

- **The branch switcher groups branches by organisation (TK-189).** The group holding your current branch is open, and opening another closes it. Each branch shows its business type, organisation and currency. Searching matches the branch, organisation, role, GSTIN or city, and opens every group so nothing is hidden. `GET /api/auth/organizations`, `GET /api/auth/session` and the login response carry `customerName`, `businessType` (a branch with none reads as `Retail`), `baseCurrency`, `gstin` and `city` for each branch, read in one query.
- **The rail** is 64px wide. Its account button shows your initials and your role in the current branch, and opens a menu with your name, role, email and **Logout**. Escape, a click outside or moving to another page closes it.
- **The rail opens a module's menu; a row opens a screen.** Clicking a module that has a secondary menu shows that menu beside the rail and marks the module, and the screen stays where it is until you pick a row. Clicking the module whose menu is open hides it, and clicking again shows it; picking any other module brings a hidden menu back. Home, and any module with a single screen, goes straight to its screen. A Ctrl- or middle-click opens the module's first screen in a new tab.
- **The menu follows the screen.** After any navigation — a row, a search hit, a link — the menu, the rail and the breadcrumb all show the module that owns the screen. A screen filed under another module's menu counts as that module's: **HSN and SAC codes** lives at `/settings/hsn-sac` but belongs to Inventory. The row stays marked on its form and its records (`/sales/invoices/new`, `/sales/invoices/42`), and the section holding it opens on a reload or a link too.
- **The breadcrumb is built from the menu**: Home › module › screen, then *New invoice*, *Edit invoice* or the record's kind. A screen the menu does not list falls back to its URL.
- **The + beside a row starts a new record** in the same place as the screen's own New button: the document's form (`/sales/invoices/new`, `/contacts/new`), or the list's in-place form, asked for with `?action=create`. A list answers that when it is opened and when it is already on screen, and waits until it has loaded, as its own button does. The top bar's **New** menu uses the same rule. **Fee demands** and **Recipes** have no +: demands are raised in bulk from a fee structure, and every dish already has a recipe to edit.
- **Searching the Reports menu** for something it does not have keeps the menu and its search box, and says *Nothing matches that*.
- **On a phone** the tab bar holds Home, Purchase, Sales and Banking, and **More** holds your account and the rest. A tab, or a module in More, opens that module's menu as a sheet from the bottom of the screen; picking a row or tapping above the sheet closes it, and **Back** from a module in More returns to More. The top bar keeps the branch switcher, notifications, help and sign out. New, Search, Favourites and the financial-year tag are desktop only.

### Signing up

- **Signup asks what the business does.** Choose the first branch's kind of business and its add-ons. Pick **HRMS or Payroll only** for a customer that trades nothing, and then tick HRMS, Payroll or both. An add-on the business type does not allow cannot be ticked.
- **You prove your email or mobile number before anything is created (TK-196, TK-197).** The last step sends a 6-digit code **by email** to the address from the first step, unless you choose **WhatsApp** or **SMS**, which send it to your mobile number. The mobile number is optional unless you choose one of those two; if it is missing, the field appears on the last step. Nothing exists until the code is entered: no customer, branch, licence, owner or trial. A wrong code says how many tries are left, and five wrong tries need a new code. A code works for ten minutes. A new one can be asked for after a minute, five an hour per address or number. Changing the channel, the email or the number needs a new code. If no code can be sent that way, the page says so and nothing is created.
- Signing up with the right code creates the customer, the first branch, the owner and a 14-day trial. The owner gets the **Owner** role, and the email or mobile number the code went to counts as confirmed.
- A platform operator adding a customer from the admin **Customers** page is not sent a code.

### Forgot password

- **Choose how the code comes:** **Email** (the default), **WhatsApp** or **SMS** (TK-197). An email code goes to the account's address; a WhatsApp or SMS code goes to the mobile number on the account.
- **The page always moves on to the code**, whether or not the email has an account, the account has a mobile number, or the code could be sent. It never says which, so the page cannot be used to find out who has an account.
- A code works for ten minutes. **Send a new code** comes back after a minute; **Send it another way** goes back to choose another channel. Five wrong tries need a new code. Resetting ends every other session, and the reset marks where the code went as confirmed: the email, or the mobile number for WhatsApp or SMS.
- Ten code requests or checks a minute from one connection are allowed, across signup and reset.
- **What a branch is set up with follows its scope.** Accounting and print templates are set up for every branch, because payroll and fees post to the books and every branch prints. Items, sales, purchases, reports, support and contacts are set up for a branch with a trade. School's services are set up for a School branch, and the employee master for a School branch or one with HRMS or Payroll.

### HRMS, Payroll and School pages

HRMS, Payroll and School are no longer web apps of their own (TK-134). Their pages are part of the web app, shown to a branch whose business type or add-ons allow them: the School pages to a School branch, HRMS and Payroll to a branch with that add-on. The client portal is the web app's `/portal` pages and the platform admin pages are `/admin`.

### Pages check what you may open

- **The menu shows only the screens the branch's scope reaches**, and only those you hold a permission for.
- **A page you cannot open says why.** A typed URL or an old bookmark to a page you lack the permission for opens a *No access* page, which names the permission to ask your administrator for. A page for another kind of business, or for an add-on the branch has not switched on, says that instead. An expired or suspended licence goes to the expired page.
- **Settings › Audit log** offers the logs the branch's scope reaches: the trading services for a trading branch, School's for a School branch, and HRMS's and Payroll's for their add-ons.
- **Settings › Number series is a settings screen.** Opening or changing it takes the settings permissions instead of the accounting ones, matching the menu, which lists it under settings.

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
- Each owner's sign-in is a plus-address of one mailbox, `Bootstrap:DemoOwnerEmail` (or `Bootstrap:OwnerEmail`): `you@example.com` becomes `you+demo-retail@example.com`, so every reset lands in one inbox. Owners are created with no password; set one with **Forgot password**. No password is kept in the source. The platform admin's **Customers** page shows each customer's kind, filters by it, and lets an operator choose the kind and the first branch's business type when adding one.

## The licence

One row per customer (`GET /api/customers/{id}/licenses`), created automatically at signup:

| Field | Trial default |
|---|---|
| `LicenseType` | Trial |
| `ExpiryDate` | signup + 14 days |
| `MaxUsers` | 3 (see **Plans** below) |
| `MonthlySalesTransactionLimit`, `MonthlyPurchaseTransactionLimit` | 1,000 each (see **Plans** below) |
| `MaxOrganizations` | 1 |
| `GraceDays` | 0 |

A licence is expired when `today > ExpiryDate + GraceDays`. Expiry is evaluated **lazily**, the first time an org context is resolved, and stamped onto the customer — no nightly job is required.

## Plans

A customer is on one of three plans (D-68, TK-209). The plan is the licence's type: **Trial**, **Professional** (stored as `Pro`; an older `Standard` licence reads as the same thing) or **Elite**. What a plan allows is stored on the licence, so an operator can settle a different figure for one customer:

| Plan | Users in a branch | Sales documents a month | Purchase documents a month |
|---|---|---|---|
| **Trial** (14 days) | 3 | 1,000 | 1,000 |
| **Professional** | 5 | 1,000 | 1,000 |
| **Elite** | no limit | no limit | no limit |

An empty figure on the licence means **no limit**, which only Elite has. Every start fills a missing figure on a Trial or Professional licence with its plan's own and clears the figures on an Elite one; a figure an operator set on anything else is left alone. An Internal or Demo customer is Elite and is never held to a limit. The 10,000 users such a licence once carried in place of "no limit" are gone.

### Documents a month

A Trial or Professional branch may create **1,000 Sales documents and 1,000 Purchase documents in a calendar month** (D-69, TK-210). The two are counted apart: a branch that has filled its Sales month still has its thousand purchase documents. Elite is never counted or refused.

| Counted | Not counted |
|---|---|
| Sales: quotes, sales orders, invoices (a POS sale is an invoice) and credit notes | Delivery challans |
| Purchase: purchase orders, goods receipts, bills and debit notes | Changing a document that already exists |

- **When a document counts.** The moment it is created, **drafts and voided ones included**, in the **Indian calendar month** of its creation: the month turns at midnight in India, not in UTC. Converting a document counts as the new document it creates: an order from a quote, an invoice from an order, a bill from a goods receipt.
- **Every way of creating one is counted.** The count is taken when the document is saved, so the form, a conversion, a till and recurring billing all pass through it, and a way added later does too.
- **The document past the limit is refused**, with `409` and the plan's words: *Professional plan Sales transaction limit reached (maximum 1,000 Sales transactions this month). Upgrade to the Elite Plan for unlimited transactions.* The answer carries the figures (`code` `PlanLimitReached`, `kind` `SalesTransactions` or `PurchaseTransactions`, `plan`, `limit`, `used`), and the form shows the sentence like any refusal. **The upgrade prompt opens as well**, from one place in the app, so no form has handling of its own.
- **Counted inside the document's own save.** Two people creating the last document at once cannot both get it, and a document that fails to save gives its place back: the count and the document stand or fall together.
- **Recurring billing skips and logs.** A schedule whose occurrence would pass the limit raises nothing: the schedule stays due with the plan's sentence on it, the run reports the occurrence as *skipped*, and it is raised on the first run after a place is free, which is the first of next month.
- **The limits are read from the licence through Master, cached for a minute**, so a plan an operator has just switched is in force within the minute. If Master cannot be reached the limit is unknown, and unknown is never a limit: nothing is counted or refused until it answers.

**What is left shows on the screens.** Beside the title of each Sales and Purchase register a badge reads, for example, **Sales: 88 of 1,000 left**. It is a warning when a fifth or less is left and turns red when none is; an Elite branch has no badge, and a register of delivery challans never does. `GET /api/sales/usage` and `GET /api/purchase/usage` (*Sales: view*, *Purchase: view*) return what the signed-in branch has used this month, its limit and what remains.

### The Plan screen

**Settings › Plan** (`/settings/plan`, *Settings: view*) shows the plan the signed-in branch is on and how much of it is used, beside the two paid plans and what they cost (D-70, D-71, TK-211). It is in every branch's Settings, whatever its trade.

- **Your usage.** A meter for each count: **Users** (everyone who has ever been given access, deactivated people included), and **Sales** and **Purchases** this month. A meter turns amber at four fifths of its limit and red when the limit is reached. On Elite it shows what has been used and that it is **Unlimited**, with no bar. A branch that cannot give a document count goes without that meter.
- **Compare plans.** Professional and Elite side by side, Elite raised. Each card lists what the plan allows, in the server's figures, and its price for the chosen billing cycle, **Quarterly** (three months) or **Bi-annual** (six months), with about what a month comes to. The card of the plan the branch is on says **Current plan**; the other offers **Request upgrade to …**. A trial may ask for either paid plan, Professional may ask for Elite, and Elite has nothing to ask for.
- **Asking.** The button opens the upgrade prompt for that plan (see **The upgrade prompt**). Someone without *Settings: edit* is told to ask an administrator of the business.

`GET /api/plan/prices` (*Settings: view*) returns what each paid plan allows (`tiers`) and what each costs for the signed-in branch's trade (`cycles`). A branch with no trade, or a trade nobody has priced, has no cycles: the screen says the price is agreed with the platform's team, and the request still goes through.

### Prices

The prices are rows in `mst.PlanPrices` (`BusinessType`, `Plan`, `BillingCycle`, `Amount`, `CurrencyCode`, `IsActive`), one for each trade, plan and cycle, so an operator can change one without a release. Every start adds the rows that are missing and **never touches one that is there**: a figure someone changed stays as they set it, and a row set to inactive takes its cycle off the screen.

The first figures come from the website's monthly price for each trade:

| Trade | Monthly price |
|---|---|
| Retail, Pharma, Service | ₹1,500 |
| Restaurant | ₹2,200 |
| Jewellery | ₹4,000 |
| School | ₹5,000 |

**Professional** costs that price for the months in the cycle, less **10% for a quarter and 15% for six months**. The website sells a quarter at 10% off and a year at 20% off but no half year, so the half year is taken midway between them. That is an assumption, and the first thing to change if it is priced differently. **Elite costs three times Professional** for the same trade and cycle. For Retail that is ₹4,050 and ₹12,150 a quarter, and ₹7,650 and ₹22,950 for six months. A price is shown in the currency it was set in, rupees, and not in the branch's own.

### Switching a customer's plan

There is no payment inside the app, so a platform operator moves a customer once the request has been dealt with (D-70). **Admin › Customers** shows each customer's plan, with a **Wants Elite** badge beside it while a request is waiting. **Switch plan**, on any Standard customer that has a licence, opens a prompt that starts on the plan they asked for (Elite for a Professional customer who has not asked, Professional for anyone else), does not offer the plan they are already on, and says the renewal date is not changed here.

`PUT /api/customers/{customerId}/licenses/plan` (*Platform: edit*) takes `{ "plan": "Pro" }` or `{ "plan": "Elite" }`:

- the licence takes that plan's own limits at once and `Customer.PlanTier` follows; a customer on trial becomes **Active**;
- the waiting requests the new plan answers are marked done, so a request for Elite stays waiting when the customer is moved to Professional;
- the user limit applies at once, and Sales and Purchase pick up the new document limits within the minute they cache them;
- it refuses a plan the customer is already on (`409`, Standard counting as Professional), an Internal or Demo customer, which is Elite for good and never billed (`409`), a plan that is not for sale, such as Trial (`400`), and a customer that does not exist (`404`).

Moving a customer **down** is allowed and takes nothing away: the people and the documents stay, and the lower limits apply to what is added next. A switch changes neither the expiry nor the branch cap, which **Renew** does, and it does not move a customer's books to a database of their own.

## Expiry blocks the app, never the login

This is the important rule, and it is deliberate.

An expired customer **still authenticates normally**. The tokens issue, and the access token carries `license_status: "Expired"`. What changes is what they can reach:

- A route guard sits above every feature route. An expired licence **cancels navigation and renders an empty "Trial expired" page** — so typing `/accounting/journal` directly lands there, not on the journal.
- The only live routes are the expiry page, billing/upgrade and logout.
- **Every feature API also returns `403 LicenseExpired`.** The guard is the UX half; the API check is the real boundary. Hand-crafting a request gets you nothing.

Letting login itself fail would have been simpler to build and worse to use — the customer could not see why they were locked out, or reach a renew button.

## User limit

![User Roles & Permission Management](assets/screenshots/users-management.png)

A branch holds as many users as its plan allows: **3 on a trial, 5 on Professional, no limit on Elite** (D-68, TK-209). The limit is counted **per branch**, and it counts **everyone who has ever been given access, deactivated people included.** Revoking someone does not free their place. The ways to more places are the Elite plan, or an operator changing the figure on the licence.

Inviting a sixth user to a Professional branch is refused with `409` and says why:

> Professional plan user limit reached (maximum 5 users including inactive accounts). Upgrade to the Elite Plan to add more users.

On a trial the sentence names the Trial plan and points at Professional or Elite. The answer carries the figures with the words (`code` `PlanLimitReached`, `kind` `Users`, `plan`, `limit`, `used`), so a screen can open its upgrade prompt without reading the sentence. Nothing is created, so no user is left who cannot sign in.

Inviting someone back who was revoked takes no new place: they are already counted, and their assignment to the role they had is switched on again. Inviting someone who is already active in the branch is told so (`409`), not that the plan is full. Two invitations racing for the last place cannot both succeed.

`GET /api/plan` (*Settings: view*) returns the plan's name, its limits, and how many places the signed-in branch has used.

### On the Users screen

**Settings › Users** says how many places are taken: **5 of 5 User Slots Used (Includes Deactivated Users)**. It is a note while a place is left and a warning when none is. A plan with no user limit shows no banner.

When none is left, **Invite user** is off and an information icon beside it gives the reason in the server's own sentence. **Upgrade to Elite for Unlimited Users**, in the banner, opens the **upgrade prompt**. If the branch fills up between the screen opening and an invitation being sent, the refusal is shown as a warning in those same words and the banner catches up. **Revoke** warns that the person still counts towards the limit.

### The upgrade prompt

One prompt serves every limit, opened from a button or from a refused request. It shows the limit's sentence, a comparison of what the plan allows against what Elite does (users in a branch, Sales documents a month, Purchase documents a month), an optional note, and **Request upgrade to Elite**. A trial asking for Professional from the Plan screen gets the same prompt, titled **Upgrade to Professional**, without the Elite comparison, and the request names Professional. Once the request is sent the prompt says so and offers nothing more. If a request is already waiting it says that instead; someone without *Settings: edit* is told to ask an administrator of the business; a refusal is shown in the server's words and the request can be sent again. On a phone the prompt takes the whole screen.

### Asking for a higher plan

There is no payment inside the app (D-70). `POST /api/plan/upgrade-requests` (*Settings: edit*) records the request, with who asked, from which branch and what sent them, in `mst.UpgradeRequests`, and mails it to the address in `Licensing:UpgradeRequestEmail`, from the platform's own mailbox. The plan asked for is Elite unless a trial asks for Professional. A platform operator changes the plan (see **Switching a customer's plan**). A customer with a request waiting does not make a second one: it is told so and nothing more is sent. With no address set the request is still recorded and a warning is logged. Elite, and an Internal or Demo customer, have nothing to ask for.



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

### Encrypted Credential Security

All sensitive mail server passwords are encrypted at rest using strong AES authenticated encryption. The platform interface never reveals existing passwords; the password field displays masked characters `••••••` and is only replaced if a user explicitly types a new password.

**Send test email** allows immediate verification of your SMTP configuration before business operations depend on it, giving direct feedback if credentials or host settings need adjustment.

## Asynchronous Delivery

Outgoing system emails (user invitations, password resets, and transaction alerts) are queued and delivered asynchronously. This ensures that user actions in the application remain responsive and are never blocked waiting for external mail server responses. Transient connection retries are handled automatically. Email subject and destination are recorded in the audit trail, while email bodies and verification links remain confidential.

## Inviting a user

`Settings → Users → Invite user`. Collects email, name, optional mobile, and a role.

1. Creates the user with **no password** and `EmailConfirmed = false`
2. Assigns the role for the current organization
3. Issues a **7-day invitation token** (only its hash is stored) and queues the email
4. Refuses with `409` when the plan's user limit is reached (see **User limit**)

The invitee follows the link to `/accept-invitation`, sets a password, and the email is confirmed in the same step. Until then the user cannot sign in — an empty password hash fails verification outright.

**No temporary password is ever created.** A resend issues a fresh token and invalidates the previous link.

> **Signup and reset codes by email come from the platform's own mailbox** (TK-197): the mail settings with no customer, which a platform operator sets. With none set, a signup by email says codes cannot be sent by email and creates nothing.
>
> **WhatsApp and SMS codes need a messaging provider, and none is chosen yet (D-66).** Signup and Forgot password both offer WhatsApp and SMS (TK-196). Until a provider is configured, a development install writes each code to the Master service's log, and every other install sends nothing: a signup by WhatsApp or SMS says codes cannot be sent that way and creates nothing, and a reset sends nothing. Email works meanwhile. An invited user's mobile number is not verified by the invitation.

## Revoking access

Revoking deactivates the organization assignment rather than deleting the user, so history and audit trails survive. The person **keeps their place** under the plan's user limit. Two guards: you cannot revoke yourself, and you cannot revoke the last active Owner of an organization.




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
