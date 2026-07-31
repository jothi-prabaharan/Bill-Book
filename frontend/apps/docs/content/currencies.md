# Currencies

**Status: built.**

## The master

`mst.Currencies` is the single source for code, symbol and display formatting.

| Column | Purpose |
|---|---|
| `Code` | ISO 4217 |
| `Symbol` | ₹ $ £ |
| `Format` | Display grouping mask |
| `DecimalPlaces` | **Drives rounding, not just display** |
| `SymbolPosition` | Prefix or suffix |

### Why `Format` is a column and not a constant

Western grouping is in threes — `###,###,##0.00` renders `1,234,567.89`. **Indian grouping is lakh/crore** — `##,##,##0.00` renders `12,34,567.89`. A single hard-coded mask would render every rupee amount wrong, so each currency carries its own.

`DecimalPlaces` is separate from the mask because money rounding must never be parsed out of a display string — and it is not always 2. JPY and KRW are 0; KWD and BHD are 3.

## Per-organization activation

An organization transacts in a subset of the ~180 world currencies, held in `plt.OrgCurrencies`.

- The **base currency is enabled and active from organization creation** and **cannot be deactivated** — every posting converts to it, so switching it off would break base-currency amounts on every future transaction.
- The list page shows **active currencies only** by default; a "Show inactive" toggle reveals the rest.
- **Add** offers a dropdown of currencies not yet enabled, and adds the chosen one active.
- Each row has an **active toggle**, disabled on the base currency.

```
GET  /api/organizations/{orgId}/currencies?includeInactive=false
GET  /api/organizations/{orgId}/currencies/available
POST /api/organizations/{orgId}/currencies          { currencyId }
PUT  /api/organizations/{orgId}/currencies/{id}/active   { isActive }
```

Deactivating rather than deleting keeps history intact: a currency that was used last year stops appearing in pickers but its old transactions still resolve their symbol and format.

## Exchange rates

Every transaction stores `CurrencyCode`, `ExchangeRate` and a computed base-currency amount. **The rate is a snapshot at the transaction date and is never looked up live** — otherwise historical documents would silently reprice every time rates moved.

Rate history lives in `rat.CurrencyRates`, dated, not just today's value. *(Not yet built.)*
