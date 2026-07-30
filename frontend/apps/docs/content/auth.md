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
