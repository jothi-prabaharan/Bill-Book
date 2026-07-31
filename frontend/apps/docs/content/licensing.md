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
