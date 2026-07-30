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
- **Trial signup** — public self-service signup that provisions a customer database, a 14-day Trial licence, the first organization and the owner account, with a progress screen that waits until the account is ready.
- **Two-step login** — sign in once, then choose an organization; skipped automatically when you only have one.
- **Password reset by email code** — a 6-digit code valid for 10 minutes, replacing reset links. Resetting signs out every other session.
- **User invitations** — invited users receive a link and set their own password; no temporary passwords are ever issued.
- **Per-organization currencies** — enable the currencies you trade in, deactivate ones you no longer use, and see only active ones by default. Amounts follow each currency's own format, including Indian lakh/crore grouping.
- **Documentation site** — this help app, versioned alongside the product.
- **Roles & permissions** — the five built-in roles now carry their actual permission grants, and you can create your own roles with a per-module permission matrix. Built-in roles can be renamed to suit your vocabulary without changing what they allow.
- **Configuration screen** — edit unit-price and quantity decimal places and default payment terms per organization, with a one-click reset back to the shipped default.
- **Email settings** — configure the mailbox invitations and verification codes are sent from, with a test-send button to confirm the credentials before relying on them. A customer can send from its own address instead of the platform default.
- **User management** — invite people by email with a role, resend an invitation, and revoke access. Invitations are links, so nobody is ever sent a temporary password.
- **Chart of accounts** — a per-organization chart grouped by account type, seeded with ten standard accounts when the organization is created. Accounts carry usage flags controlling which document pickers they appear in, and can be renamed freely; once an account has been used its type and code are fixed so existing postings cannot be reclassified.
- **Sub-accounts** — per-contact, per-item and per-tax-rate detail beneath the control accounts, provisioned automatically by the master that owns them. GST rates get separate CGST, SGST and IGST sub-accounts in each direction, so tax reports break down by rate and component.

### Security
- Trial expiry pauses access without locking you out of your account: you can still sign in, see why access stopped and renew. Feature pages and their APIs both refuse access until the licence is renewed.
- Password reset never reveals whether an email address is registered.

### Changed
- Licence tiers are Trial, Standard, Pro and Elite. Tier and state are tracked separately, so an Elite plan that lapses reports as expired without losing its tier.
- Invitation and verification emails now send in the background and retry automatically, so saving an invitation no longer waits on the mail server.
- Account types are now the only level above the chart of accounts; account sub-types were removed. Accounts carry a contra flag directly, and an account can be renamed for display without changing what it is.

---

## Versioning

Semantic versioning. **Major** for a breaking change to the API or data model, **minor** for new capability, **patch** for fixes.

Releases are cut from `main`; the version in `package.json` and the heading here must match.
