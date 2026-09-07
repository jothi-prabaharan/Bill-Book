# Input components

Every field in Bill-Book is one of nineteen common inputs. They live in
`libs/shared/ui-components` and are exported from `@bill-book/ui-components`.

A module should never write its own version of one of these. The point is not
tidiness: it is that a field's precision, its validation, the way its error
reads and whether a screen reader can name it are all decided **once**, so a
discount box on the invoice screen behaves exactly like the one on the purchase
order.

---

## Which component for which field

| Business field | Component |
|---|---|
| Amount, payment, discount amount, tax amount, opening balance, debit, credit | `bb-money-input` |
| Sales rate, purchase rate, MRP, price-list entry | `bb-unit-price-input` |
| Invoice quantity, ordered, received, rejected, stock on hand, a weight | `bb-quantity-input` |
| Discount %, tax %, margin % | `bb-percentage-input` |
| Exchange rate | `bb-exchange-rate-input` |
| A count with no business meaning — days, a reorder cycle, an id | `bb-number-input` |
| Customer name, code, reference, GSTIN | `bb-text-input` |
| Email | `bb-email-input` |
| Mobile, landline | `bb-phone-input` |
| Password | `bb-password-input` |
| Website | `bb-url-input` |
| Invoice date, due date | `bb-date-input` |
| A timestamp — when a ticket was raised | `bb-datetime-input` |
| Notes, terms, an address | `bb-textarea` |
| A long description that needs emphasis or a list | `bb-rich-text-input` |
| Active, is customer, use SSL, a permission | `bb-checkbox` |
| One of a small fixed set | `bb-radio-group` |
| One of a list | `bb-select` |
| Filtering a list | `bb-search-input` |
| An attachment, a statement to import | `bb-file-input` |

---

## The five financial controls, and why there are five

They share one implementation — `BbNumericControlBase` — and differ in
precision, range, affix and meaning. Those differences are the reason they are
separate components rather than one number box configured five ways: a
percentage capped at 100 is wrong for a quantity, and a quantity rounded to two
places is wrong for a unit price.

| | Precision | Range | Affix | Column |
|---|---|---|---|---|
| **Money** | 2 | ≥ 0 unless `allowNegative` | currency symbol | `decimal(28,2)` / `decimal(18,2)` |
| **Unit price** | 2 shown, up to 6 kept | ≥ 0 | symbol, `/unit` | `decimal(28,6)` |
| **Quantity** | 2, or 0 in `integerOnly` | ≥ 0 | unit of measure | `decimal(18,6)` |
| **Percentage** | 2 | 0–100 by default | `%` | `decimal(9,6)` / `decimal(9,4)` |
| **Exchange rate** | 8 | > 0 | `USD → INR` | `decimal(18,8)` |

### Ten per cent is `10`, not `0.1`

`bb-percentage-input` neither multiplies nor divides. What is typed is what is
stored, and the `%` at the field's edge is a label. That matches the schema
(`DiscountPercent` and a tax `Rate` are both percents), `line-math.ts`, which
divides by a hundred, and `GstCalculator` on the C# side. Changing the
convention would restate every rate in the product.

### `minorDigits` — the one thing to get right

Bill-Book holds money two ways, and the control publishes exactly the units the
caller declares:

- **`minorDigits="0"` (the default) — the value is the decimal itself.**
  Accounting works this way: a journal line's debit is `1250.5`.
- **`minorDigits="2"` — the value is an integer count of paise.** The sales and
  purchase line grid works this way, because a document total that ties to a
  ledger cannot be summed in binary floating point. ₹1250.50 is `125050`.
- **`minorDigits="6"` — millionths.** The line grid's quantity: one unit is
  `1000000`, matching `decimal(18,6)`.

Conversion between the typed text and either form is **string surgery** in
`forms/decimal.ts` — nothing is multiplied or divided — so `1.15` at scale 2 is
`115` and never `114.99999999999999`. Excess precision rounds **half away from
zero**, matching `roundHalfAwayFromZero` in `line-math.ts` and
`MidpointRounding.AwayFromZero` in `LedgerPostingService`; half *up* differs on
exactly the negative halves, and a round-off line is signed.

`min` and `max` are stated in the same units as the value. Only the native
attributes are converted to what the field displays, because the browser
compares `step`, `min` and `max` against the decimal on screen.

### Editing against display

A numeric field holds plain digits while it has focus and re-formats to its
precision on blur. **Grouping separators are not inserted while editing** — a
caret that jumps when a comma appears is worse than an unseparated number. The
formatted form, with its symbol and its lakh-or-thousand grouping, is what
`formatMoney` and the read-only displays are for.

`decimals` is a **floor**, not a cap. A quantity field showing two places still
shows all six of `1.234567`: truncating the display of a value the field is
editing is how somebody saves a rounded figure believing they saw the whole one.

### Masks

**Email.** `bb-email-input` drops what an address cannot contain as it is typed:
whitespace, a second `@`, and upper case. The caret is put back by hand, so
typing a space mid-address does not throw you to the end of the field. Applied
to typing only, never to `writeValue` — rewriting a value the server sent would
put one address on screen and another in the form.

**Amount.** `bb-money-input` shows the figure grouped the way the branch's base
currency asks for it: `12,34,567.89` on the rupee, `1,234,567.89` on the dollar.
The grouping mask, the number of decimal places and (when `showCurrency` is on)
which side the symbol sits all come from `mst.Currency`, and none of them are
restated in the component.

Those details reach the front end two ways, both reading the same row:
`FormatSettingsService`, which the app shell loads once for every screen, and
the **`currency` block on the get-organization response** — a screen that
already holds the organization can pass it straight in through `formats`.

The grouping applies **only when the field is not being edited**. Focus strips
it back to plain digits, so no separator moves under the caret and there is no
caret arithmetic in the numeric controls at all. A masked field is
`type="text"`, because a `number` input treats a grouped value as invalid and
blanks it; `min`, `max` and `step` stop being emitted with it rather than
claiming a check the browser is not doing. Every other numeric control keeps the
native number input.

The grouping is applied to the exact decimal *text* by `groupDecimalText`, never
to a number divided back out of paise — the one path this codebase keeps free of
floating point stays that way.

---

## The layers

```
BbFormField                     label · required mark · hint · error · aria wiring
   |
   +-- BbControlBase            id generation · describedby · disabled/readonly · CVA
        |
        +-- BbTextControlBase   bb-text-input · bb-email-input · bb-phone-input
        |                       bb-url-input · bb-password-input
        |
        +-- BbNumericControlBase  bb-number-input · bb-money-input
        |                         bb-unit-price-input · bb-quantity-input
        |                         bb-percentage-input · bb-exchange-rate-input
        |
        +-- BbDateControlBase   bb-date-input · bb-datetime-input
        |
        +-- bb-checkbox · bb-radio-group · bb-select · bb-search-input
            bb-textarea · bb-file-input · bb-rich-text-input
```

`bb-form-field` is presentational: it holds no value and knows nothing about
what it wraps. Its host is `display: contents`, so it adds no box — the label,
control, hint and error land straight in the caller's `.field` and inherit the
house styling from `libs/shared/theming/_forms.scss`.

---

## The common API

Every control takes:

```
id  name  label  hint  error  required  disabled  readonly  ariaLabel  autocomplete
```

and emits `valueChange` beside its `ControlValueAccessor` binding, so both
`formControlName` and `[(ngModel)]` work.

Numeric controls add `min`, `max`, `step`, `decimals` and `minorDigits`. Text
controls add `placeholder`, `minlength`, `maxlength`, `pattern`, `uppercase`,
`prefix` and `suffix`.

### `error` is a string the page owns

Not a validator result. The page knows whether a control has been touched, what
the server said and which of several broken rules is worth naming; a control
guessing at that would show "Required" on an untouched form. The house pattern
is:

```html
<bb-money-input
  label="Payment amount"
  formControlName="amount"
  [required]="true"
  [error]="showError('amount') ? 'Give the payment an amount.' : ''"
/>
```

### `disabled` against `readonly`

`readonly` looks editable, is not, and **is still submitted** — reach for it
when the figure matters and cannot be changed here, such as on a posted
document. `disabled` removes the value from the form; reach for it when the
field does not apply at all.

There is no readonly checkbox in HTML, so `bb-checkbox` disables the element and
keeps the value in the form, which is what readonly is asking for.

---

## Validation

Two layers, and the frontend is **not** the boundary.

**On the screen** — `forms/validators.ts` offers `bbEmail`, `bbUrl`, `bbPhone`,
`bbPercentage`, `bbExchangeRate`, `bbMoneyPrecision`, `bbRichTextRequired` and
`bbFileRequired`. Every regular expression in them is **imported from the
component that renders it**, so a field's HTML `pattern` and its Angular
validator cannot disagree — which is how a form goes green while the API refuses
it.

**On the server** — the same rules, in `Shared.Kernel.Validation` and the Data
Annotations on each request model. That is the one that counts. Nothing in the
API may be relaxed because the screen checks, and nothing on the screen may be
relaxed because the API does.

`bbPhone` mirrors the product's actual rule rather than assuming India:
a **landline** matches `LandlineAttribute`'s expression character for character,
and a **mobile number carries no pattern at all** — lengths vary too much by
country for one to be anything but a source of false rejections, and the leading
`+` is what marks a foreign number.

---

## Accessibility

Every control:

- renders a real `<label for>` tied to the control's id — generated when the
  caller gives none, so two of the same field on one screen never collide;
- points `aria-describedby` at its hint, or at its error when there is one;
- sets `aria-invalid` when there is an error, and **never signals validity by
  colour alone** — the message under the field is the primary signal and the
  border is the secondary one;
- keeps native semantics. A checkbox is an `<input type="checkbox">`, a radio
  group is a `<fieldset>` with a `<legend>` (which is what gives it arrow-key
  navigation and a single tab stop), and a select is a `<select>` (which is what
  gives it type-ahead and, on a phone, the operating system's own picker);
- gives every icon-only button an accessible name — the password reveal toggle
  says *Show password* / *Hide password*, naming the action rather than the
  state.

`bb-rich-text-input`'s editable region is `role="textbox"` with
`aria-multiline`; because a `<label for>` cannot address a `contenteditable`
div, the region carries its name itself.

---

## Rich text and safety

There was no rich-text editor in the repository and no package was added.
`bb-rich-text-input` is a `contenteditable` region with a small toolbar over the
browser's own editing commands.

**Every value is filtered by an allowlist** — `sanitizeRichText` — on the way
in, after every paste, and on the way out. An allowlist rather than a denylist,
because a denylist of `<script>` and `onerror` fails to the attacker's advantage
the first time somebody invents an attribute nobody listed. Emphasis, headings,
lists and links survive; images, tables, styles, classes and ids do not. An
unknown tag is **unwrapped rather than deleted**, so the text somebody wrote is
kept.

Links may only be `http`, `https`, `mailto`, `tel` or relative, and the value is
stripped of whitespace and control characters before the scheme is read —
`java<tab>script:` is ignored by the browser's URL parser and would otherwise
get past a check that reads the raw string. A surviving link gains
`rel="noopener noreferrer"`.

The read-only rendering binds `[innerHTML]` **without** `bypassSecurityTrustHtml`,
so Angular's own sanitiser filters it a second time. The word *bypass* appears
nowhere in that component on purpose: the value is safe because it was filtered,
not because the component asserted it was.

---

## Files

`bb-file-input` **chooses** files. It never uploads one, and it never reads a
file's contents — the consuming feature owns the upload, because it is the only
thing that knows the endpoint, the org context and what to do when the server
refuses.

It re-checks `accept` itself: a browser only filters its own dialogue by it, and
a file dragged in or chosen through "All files" reaches the element regardless.
Refused files come back on `selectionChange` with a reason, so the page can say
what happened rather than dropping them silently.

---

## Migration rules

1. **Never change what reaches the API.** A migrated field publishes the same
   value in the same units as the hand-written one it replaced. If the units
   were wrong before, say so in the release notes rather than fixing it quietly.
2. **Keep the page's messages.** The wording and the moment it appears belong to
   the page; only where it renders moves.
3. **Pick the semantic control.** A contact id is `bb-number-input`; a rate per
   kilogram is `bb-unit-price-input`. Reaching for the generic one where a
   semantic one exists is the mistake this system is meant to prevent.
4. **Declare `minorDigits` wherever the value is scaled.** The default of zero
   is right for accounting and wrong for the line grid.

---

## Known limitations

- **`bb-date-input` shows the browser's locale, not the branch's.** It is a
  native `<input type="date">`, whose placeholder and on-screen format come from
  the browser and cannot be overridden by any attribute — so a branch configured
  for `dd/MM/yyyy` still sees `mm/dd/yyyy` in the field. The value it stores and
  publishes is ISO either way, so nothing downstream is wrong, but the field
  disagrees with every date the product *displays* through
  `FormatSettingsService`. Fixing it needs a written-from-scratch date
  component, which affects every date field in the product and is a larger
  decision than one screen.
- **`bb-select` is a native `<select>`**, so a list too long to scroll needs
  `bb-search-input` beside a `bb-lookup-dialog` instead, which is what the sales
  and purchase screens already do.
- **`bb-rich-text-input` uses `document.execCommand`**, which is deprecated and
  has no replacement for applying formatting to a selection. The alternative is
  a selection-range implementation of our own, which is a far larger surface to
  get wrong.
- **Component rendering is not unit-tested.** This workspace's Vitest runs
  without the Angular Vite plugin and cannot compile a `templateUrl`, so every
  spec drives the component class directly. Anything visual has to be checked by
  building, serving `dist/apps/web/browser` and driving it with Playwright.
- **`bb-file-input` has no drag-and-drop.** The bank statement import sheet has
  a purpose-built drop zone and keeps it.

---

## What is migrated, and what is not

A repository search for every `<input>`, `<select>` and `<textarea>` was run
before this work and again after it. **Every select and every textarea outside
the common components themselves is now a common component**, and typed inputs
fell from 133 to 13.

### Migrated

| Module | What moved |
|---|---|
| **Shared** | The document line grid: quantity, unit price, discount %, description, HSN, the inclusive-price tick and the account picker |
| **Sales** | Invoice, sales order, quote, credit note and delivery challan — dates, customer, GSTIN, place of supply, currency, exchange rate, addresses, notes, terms, void and short-close reasons; both list filters and their search boxes; both conversion dialogs |
| **Purchase** | Goods receipt and debit note quantities, the debit-note reason, and every notes and address block across the four documents |
| **Accounting** | Journal debit, credit and the account and sub-account pickers; money-in, money-out and transfer — bank account, contact, method, source and status; opening balance — line type, account, item and contact; account ledger; chart of accounts; bank accounts; numbering series; payment terms; statements and their import |
| **Inventory** | The item master's 21 selects and 10 flags, its four prices and two reorder levels, jewellery weights; stock movement and adjustments in full; categories, purities, warehouses, unit types, price lists |
| **Master** | Contacts — 11 selects, roles, flags, notes, credit limit, contact-person email and mobile, and the attachment picker; organization settings and branches; users; roles; HSN/SAC; org currencies; configurations; SMTP; API clients |
| **Customer** | Lead and ticket forms in full |
| **Auth** | Sign-in, sign-up (all three steps), forgot-password and accept-invitation |
| **Admin** | Customer creation |

### The 13 direct inputs that remain, and why

- **Five radios** (contacts ×3, item barcodes, unit types) are a *single* radio
  per card or per grid row, sharing a name, marking "this one is the default".
  `bb-radio-group` renders a list of options together and is not that shape;
  wrapping one radio in a group component would be a worse fit, not a better one.
- **The bank statement drop zone** is a drag-and-drop target with its own visual
  states. `bb-file-input` deliberately has no drag-and-drop, so migrating it
  would lose behaviour.
- **The data grid's boolean cell** is a disabled tick rendering a value. It is a
  display, not a control.
- **The mobile nav toggle** is a CSS-only checkbox behind a `:checked` selector.
  It is not a form field.
- **`bb-search-input`, `bb-lookup-dialog` and the column chooser** each own the
  native `<input type="search">` they are built on. Something has to be the
  element.
- **`bb-currency-input`** is deprecated in favour of `bb-money-input` and has no
  callers left; it keeps its own markup until it is deleted.
- **`bb-allocation-grid`'s amount cell** is a grid cell sized to its column and
  formatted from the `formats` it is passed. Folding it into `bb-money-input`
  would mean the grid rendering two formatting paths at once — the exact defect
  that component's own history records.

### Not built, because nothing needs one

**Multi-select** and **searchable select** are absent by choice, not oversight:
a search of the repository finds no `<select multiple>` anywhere, and the
searchable-picker pattern already exists as `bb-search-input` beside
`bb-lookup-dialog`, which six screens use. Building either speculatively would
add a component with no caller to keep it honest.
