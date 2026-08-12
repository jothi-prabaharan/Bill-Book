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
