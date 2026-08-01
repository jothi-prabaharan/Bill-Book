# Release notes

Every user-visible change gets an entry here. This page is the changelog customers read, so it is written in product language — what changed for them, not which class was refactored.

## How entries get written

The rule: **the release-note entry lands in the same commit as the feature.** Not a sweep before a release, when the detail has evaporated and someone is reverse-engineering a month of commits from git log.

1. Build the feature.
2. Add a bullet under **Unreleased** below, in the right category.
3. Update the relevant documentation page and its status in the sidebar manifest.
4. Commit all of it together.

At release time, **Unreleased** is renamed to the version with a date and a fresh Unreleased block goes on top. No archaeology required.

### Categories

- **Added** — new capability
- **Changed** — different behaviour in something that already worked
- **Fixed** — a defect
- **Removed** — a capability taken away
- **Security** — anything affecting authentication, authorization or data isolation

### What earns an entry

Anything a user or an administrator would notice: a new screen, a changed rule, a new field, a different default. Internal refactors, test additions and documentation-only edits do not.

Breaking changes are prefixed **⚠ Breaking** and say what to do about it.

---

## Unreleased

### Added
- **Branches** — a Settings screen for the places you trade from. Adding one sets up its books as it is created — chart of accounts, GST rates, numbering series, payment terms, units and purities — and the branch stays marked *Setting up* until that finishes, so it is never handed over half-made. Switch between branches without signing out; you get the permissions you hold in the branch you move to. The number allowed comes from your licence, and the first branch cannot be suspended.
- **Trial signup** — public self-service signup that provisions a customer database, a 14-day Trial licence, the first organization and the owner account, with a progress screen that waits until the account is ready.
- **Two-step login** — sign in once, then choose an organization; skipped automatically when you only have one.
- **Password reset by email code** — a 6-digit code valid for 10 minutes, replacing reset links. Resetting signs out every other session.
- **User invitations** — invited users receive a link and set their own password; no temporary passwords are ever issued.
- **Per-organization currencies** — enable the currencies you trade in, deactivate ones you no longer use, and see only active ones by default. Amounts follow each currency's own format, including Indian lakh/crore grouping.
- **New organizations now arrive set up** — creating one writes its chart of accounts, GST rates, numbering series, payment terms, contact person roles, unit types and units, and metal purities. Previously an organization came up empty, and an item could not be saved at all because saving one requires a unit type. Provisioning now fails and waits for retry rather than handing over an account with no master data.
- **Banks and bank accounts** — each account creates its own account in the chart of accounts automatically, so there is nothing to reconcile by hand between the two. Overdrafts and credit cards are classified as liabilities rather than negative assets. Balances always come from the ledger; nothing is stored twice.
- **Inventory masters** — units, categories, metal purities, warehouses and the item master. Units convert through a single factor to their type's base unit, so kilos and grams are one stock number rather than two; pack sizes are units of their type. An item names its unit type plus its inventory, sales, purchase and report units, with stock and cost always held in the inventory unit.
- **Item profiles** — pharma and jewellery items carry their own tab and their own fields: salt, schedule, storage and minimum expiry on receipt for medicines; metal, purity, weights and making charges for ornaments. Choosing a profile presets the costing method and tracking that vertical needs.
- **Costing method per item** — weighted average, FIFO, LIFO, FEFO or specific identification, fixed once stock has moved because the earlier postings were made under it.
- **Contacts** — one master for customers, vendors, job workers and prescribers, with roles ticked per contact rather than a separate list for each. Addresses, people, bank details, licences and trading limits save together with the contact. GSTIN is checked against the place of supply on save, so a contact cannot be created that would split its tax the wrong way. Every contact gets its receivable and payable sub-accounts automatically.
- **Stock** — quantities and weighted average cost per item, with the full movement history behind them. One pool across every warehouse in the branch: a warehouse says where something moved, never how much sits there. Record openings, receipts, issues, adjustments and warehouse transfers, in whichever unit you counted in — enter 300 grams against an item held in kilos and it converts on the way in, keeping both figures on the record. Stock comes down through a single guarded statement, so two tills cannot both sell the last unit. Selling more than you hold is refused and changes nothing.
- **Costing engine** — what stock cost is now settled just after a sale rather than during it, so a till is never waiting on layer arithmetic. Quantities are still updated immediately and are never wrong; a movement whose cost has not settled says "costing…" rather than showing zero. The stock screen reports when the engine is behind, and anything that fails repeatedly stops and is flagged instead of retrying silently forever. Batch and serial numbers are still checked while you save, because those are yours to correct.
- **Batches, serial numbers and cost layers** — FIFO, LIFO, FEFO and specific identification now mean something. Every receipt records what it cost, and every issue records which receipts it drew from and how much from each, so the cost of a sale can be walked back to the purchases behind it. Batch-tracked items ask for the lot and its expiry, and a lot carries its own printed MRP. Serial-tracked items take one serial per unit, with the BIS HUID held against the piece rather than the design.
- **Backdated receipts restate the sales that came after them** — enter a purchase dated before sales that have already happened and their cost of goods sold is recalculated automatically, because under FIFO that stock should have gone out first. Quantities never change; only what they cost. Each restatement is listed with what the sale cost before, what it costs now, and which receipt caused it. The old figures are kept rather than overwritten.
- **Sales returns go back to the layers they came from** — name the sale being returned and the stock re-enters at the cost it left at, so buying, selling and returning leaves stock value exactly where it started. Partial returns are given back oldest allocation first, and cannot together return more than went out.
- **The item lock is live.** An item's unit type, inventory unit, costing method, profile and tracking options freeze the moment stock first moves, as they were always documented to. Until now nothing had moved, so nothing was ever locked.
- **Vendor payout accounts** — hold several bank accounts against a contact with exactly one default, including UPI ids. Kept separate from your own bank accounts: these are accounts you pay money to, and nothing about them posts to the ledger.
- **Contact licences** — drug licence, FSSAI, BIS and medical registration, each with an expiry. A licence shows as valid, expiring or expired the moment you open the tab, and an expiring-licences report lists everything lapsing across all contacts. Supplying against a lapsed drug licence is an offence, so the date is visible rather than buried.
- **Contact documents** — attach the GST certificate, PAN card, agreements and cancelled cheques to a contact. PDF or image up to 10 MB, with the limit and the accepted types configurable per deployment. Downloads go through the API so another organization can never fetch a file, and removing a document leaves the stored file intact.
- **Contact person roles** — a small master maintained from a popup on the contact list, so a missing role never means abandoning a half-filled contact. Reorderable by drag; built-in roles can be renamed but not deleted.
- **Payment terms** — Due on Receipt, Net 15/30/45/60 and End of Month out of the box, plus your own. Supports early-payment discounts, and shows the due date each term produces for a bill dated today so "end of month plus 15" is unambiguous.
- **Numbering series** — a Settings screen that defines how every generated code is built: prefix, financial-year segment, branch code, zero padding and suffix, with a live preview. Series reset yearly, monthly or daily on your financial year, can differ per branch, and are reorderable by drag. Document series are held to consecutive numbering, so a number is taken at save rather than when the form opens and cannot be typed by hand. Customer, vendor, item, warehouse and bank series are created with every new organization.
- **Documentation site** — this help app, versioned alongside the product.
- **Roles & permissions** — the five built-in roles now carry their actual permission grants, and you can create your own roles with a per-module permission matrix. Built-in roles can be renamed to suit your vocabulary without changing what they allow.
- **Configuration screen** — edit unit-price and quantity decimal places and default payment terms per organization, with a one-click reset back to the shipped default.
- **Email settings** — configure the mailbox invitations and verification codes are sent from, with a test-send button to confirm the credentials before relying on them. A customer can send from its own address instead of the platform default.
- **User management** — invite people by email with a role, resend an invitation, and revoke access. Invitations are links, so nobody is ever sent a temporary password.
- **Chart of accounts** — a per-organization chart grouped by account type, seeded with ten standard accounts when the organization is created. Accounts carry usage flags controlling which document pickers they appear in, and can be renamed freely; once an account has been used its type and code are fixed so existing postings cannot be reclassified.
- **HSN & SAC codes** — a searchable master of goods and service codes, with a CSV importer for the official CBIC list. Assigning a code to an item pre-selects its usual GST rate.
- **Tax master** — the six standard GST rates are created with each organization, including the 3% bullion rate. Enter a total and the CGST, SGST and IGST split fills itself in. Rates are effective-dated: changing one creates a new version and keeps the old, so invoices dated before the change still use the rate that applied then.
- **Sub-accounts** — per-contact, per-item and per-tax-rate detail beneath the control accounts, provisioned automatically by the master that owns them. GST rates get separate CGST, SGST and IGST sub-accounts in each direction, so tax reports break down by rate and component.
- **Sub-accounts screen** — see what is posting beneath each control account, grouped by account and filtered by owner type. Read-only: sub-accounts belong to the contact, item or tax rate that created them.
- **Development, Staging, UAT and Production environments** — every service and both Angular apps now build and run against a named environment. Development works from a fresh clone with no setup; the other three take their connection strings, signing keys and service addresses from the deployment environment, and refuse to start rather than fall back to a local default. Web and Docs can each be debugged in Development or Staging from VS Code.

### Fixed
- Contacts, Inventory and Banking could not start in Development at all: none of them had a development configuration, so each refused to boot on a missing token signing key. All three now start from a fresh clone.
- An account that already had sub-accounts beneath it could still have its type, code and usage flags changed. The configuration lock now engages the moment anything is created under an account, as it was always documented to.
- Changing the currency of a locked account appeared to save but silently did nothing. It is now refused with an explanation, alongside the other frozen fields.
- An account could be made its own ancestor through a chain of parents, producing a chart of accounts that no report could total. Parent changes now reject cycles.

### Security
- **Branch isolation is now enforced by the database for the chart of accounts, sub-accounts and tax rates.** Those three were the last tables relying on the application alone to keep one branch's rows out of another's. They now carry the same row-level security policy every other table already had, so the boundary holds however the data is reached.
- Trial expiry pauses access without locking you out of your account: you can still sign in, see why access stopped and renew. Feature pages and their APIs both refuse access until the licence is renewed.
- Password reset never reveals whether an email address is registered.

### Changed
- **⚠ Breaking — a branch is now an organization.** The model is two levels, not three: your **customer account is the head office**, and each **organization is a branch**. The separate branch list has been removed — it duplicated the organization almost field for field, while only the organization ever separated any data. Each branch keeps its own items, contacts, stock, chart of accounts and numbering, so nothing leaks between them. Warehouses and numbering series no longer ask which branch they belong to, because the branch you are signed in to is the answer. Organizations now carry a short branch code, used in generated document numbers; existing organizations are set to `HO`.
- Licence tiers are Trial, Standard, Pro and Elite. Tier and state are tracked separately, so an Elite plan that lapses reports as expired without losing its tier.
- Invitation and verification emails now send in the background and retry automatically, so saving an invitation no longer waits on the mail server.
- Account types are now the only level above the chart of accounts; account sub-types were removed. Accounts carry a contra flag directly, and an account can be renamed for display without changing what it is.

---

## Versioning

Semantic versioning. **Major** for a breaking change to the API or data model, **minor** for new capability, **patch** for fixes.

Releases are cut from `main`; the version in `package.json` and the heading here must match.
