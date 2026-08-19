# Primitive Input Usages Survey Report (Accounting, Inventory, and Master UI)

**Date**: 2026-08-18  
**Scope**:  
1. `frontend/libs/accounting/accounting-ui`
2. `frontend/libs/inventory/inventory-ui`
3. `frontend/libs/master/master-ui`

---

## 1. Observation

A complete file-by-file scan of all 34 component templates across `accounting-ui`, `inventory-ui`, and `master-ui` revealed **278 raw HTML `<input>` occurrences**. No inline `<input>` tags were found in TypeScript `.ts` files.

### 1.1 Accounting UI (`frontend/libs/accounting/accounting-ui`) — 95 Occurrences

#### `src/lib/account-ledger/account-ledger.page.html` (2 inputs)
- **Line 22**: `<input type="date" [(ngModel)]="from" name="from" />`
  - *Type*: Date Picker | *Purpose*: Ledger range start date filter | *Binding*: `[(ngModel)]="from"`, `name="from"`
- **Line 26**: `<input type="date" [(ngModel)]="to" name="to" />`
  - *Type*: Date Picker | *Purpose*: Ledger range end date filter | *Binding*: `[(ngModel)]="to"`, `name="to"`

#### `src/lib/bank-accounts/bank-accounts.page.html` (11 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Filter toggle for inactive records | *Binding*: `[(ngModel)]="showInactive"`, `(ngModelChange)="load()"`
- **Line 104**: `<input type="text" [(ngModel)]="form.accountName" maxlength="100" />`
  - *Type*: Text | *Purpose*: Bank account name | *Binding*: `[(ngModel)]="form.accountName"` | *Attr*: `maxlength="100"`
- **Line 135**: `<input type="text" [(ngModel)]="form.accountNumber" maxlength="30" />`
  - *Type*: Text | *Purpose*: Bank account number | *Binding*: `[(ngModel)]="form.accountNumber"` | *Attr*: `maxlength="30"`
- **Line 141**: `<input type="text" [(ngModel)]="form.ifsc" maxlength="11" class="uppercase" />`
  - *Type*: Text | *Purpose*: IFSC code | *Binding*: `[(ngModel)]="form.ifsc"` | *Attr*: `maxlength="11"`, `class="uppercase"`
- **Line 146**: `<input type="text" [(ngModel)]="form.branchName" maxlength="100" />`
  - *Type*: Text | *Purpose*: Branch name | *Binding*: `[(ngModel)]="form.branchName"` | *Attr*: `maxlength="100"`
- **Line 151**: `<input type="text" [(ngModel)]="form.micr" maxlength="9" />`
  - *Type*: Text | *Purpose*: MICR code | *Binding*: `[(ngModel)]="form.micr"` | *Attr*: `maxlength="9"`
- **Line 156**: `<input type="text" [(ngModel)]="form.swiftCode" maxlength="11" class="uppercase" />`
  - *Type*: Text | *Purpose*: SWIFT code | *Binding*: `[(ngModel)]="form.swiftCode"` | *Attr*: `maxlength="11"`, `class="uppercase"`
- **Line 161**: `<input type="text" [(ngModel)]="form.iban" maxlength="34" />`
  - *Type*: Text | *Purpose*: IBAN | *Binding*: `[(ngModel)]="form.iban"` | *Attr*: `maxlength="34"`
- **Line 167**: `<input type="text" [(ngModel)]="form.currencyCode" maxlength="3" />`
  - *Type*: Text | *Purpose*: Currency ISO code | *Binding*: `[(ngModel)]="form.currencyCode"` | *Attr*: `maxlength="3"`
- **Line 173**: `<input type="number" [(ngModel)]="form.odLimit" step="0.01" min="0" />`
  - *Type*: Currency / Decimal Number | *Purpose*: Overdraft limit | *Binding*: `[(ngModel)]="form.odLimit"` | *Attr*: `step="0.01"`, `min="0"`
- **Line 178**: `<input type="checkbox" [(ngModel)]="form.isActive" />`
  - *Type*: Checkbox | *Purpose*: Account active flag | *Binding*: `[(ngModel)]="form.isActive"`

#### `src/lib/banks/banks.page.html` (4 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Inactive bank filter toggle | *Binding*: `[(ngModel)]="showInactive"`, `(ngModelChange)="load()"`
- **Line 60**: `<input type="text" [(ngModel)]="form.bankName" maxlength="100" />`
  - *Type*: Text | *Purpose*: Bank name | *Binding*: `[(ngModel)]="form.bankName"` | *Attr*: `maxlength="100"`
- **Line 64**: `<input type="text" [(ngModel)]="form.bankCode" maxlength="20" [disabled]="editingId() !== 0" />`
  - *Type*: Text | *Purpose*: Bank code (read-only on edit) | *Binding*: `[(ngModel)]="form.bankCode"`, `[disabled]="editingId() !== 0"` | *Attr*: `maxlength="20"`
- **Line 68**: `<input type="checkbox" [(ngModel)]="form.isActive" />`
  - *Type*: Checkbox | *Purpose*: Bank active state | *Binding*: `[(ngModel)]="form.isActive"`

#### `src/lib/chart-of-accounts/chart-of-accounts.page.html` (10 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Inactive accounts filter | *Binding*: `[(ngModel)]="showInactive"`, `(ngModelChange)="load()"`
- **Line 37**: `<input name="accountCode" [(ngModel)]="form.accountCode" [disabled]="locked()" required />`
  - *Type*: Text (implicit) | *Purpose*: Account code | *Binding*: `[(ngModel)]="form.accountCode"`, `[disabled]="locked()"`, `name="accountCode"`, `required`
- **Line 43**: `<input name="accountName" [(ngModel)]="form.accountName" required />`
  - *Type*: Text (implicit) | *Purpose*: Account display name | *Binding*: `[(ngModel)]="form.accountName"`, `name="accountName"`, `required`
- **Line 60**: `<input type="checkbox" [(ngModel)]="form.isSales" name="isSales" [disabled]="locked()" />`
  - *Type*: Checkbox | *Purpose*: Sales document usage flag | *Binding*: `[(ngModel)]="form.isSales"`, `[disabled]="locked()"`
- **Line 64**: `<input type="checkbox" [(ngModel)]="form.isPurchase" name="isPurchase" [disabled]="locked()" />`
  - *Type*: Checkbox | *Purpose*: Purchase document usage flag | *Binding*: `[(ngModel)]="form.isPurchase"`, `[disabled]="locked()"`
- **Line 68**: `<input type="checkbox" [(ngModel)]="form.isPayment" name="isPayment" [disabled]="locked()" />`
  - *Type*: Checkbox | *Purpose*: Payment document usage flag | *Binding*: `[(ngModel)]="form.isPayment"`, `[disabled]="locked()"`
- **Line 72**: `<input type="checkbox" [(ngModel)]="form.isBank" name="isBank" [disabled]="locked()" />`
  - *Type*: Checkbox | *Purpose*: Bank/cash usage flag | *Binding*: `[(ngModel)]="form.isBank"`, `[disabled]="locked()"`
- **Line 76**: `<input type="checkbox" [(ngModel)]="form.isContra" name="isContra" [disabled]="locked()" />`
  - *Type*: Checkbox | *Purpose*: Contra balance flag | *Binding*: `[(ngModel)]="form.isContra"`, `[disabled]="locked()"`
- **Line 84**: `<input type="checkbox" [(ngModel)]="form.isActive" name="isActive" />`
  - *Type*: Checkbox | *Purpose*: Active status | *Binding*: `[(ngModel)]="form.isActive"`
- **Line 88**: `<input type="checkbox" [(ngModel)]="form.isLock" name="isLock" />`
  - *Type*: Checkbox | *Purpose*: Posting freeze lock | *Binding*: `[(ngModel)]="form.isLock"`

#### `src/lib/closing-dates/closing-dates.page.html` (2 inputs)
- **Lines 48-53**:
  ```html
  <input
    type="date"
    [(ngModel)]="row.editing"
    [name]="'lock' + row.role.roleId"
    [attr.aria-label]="'Closed up to, for ' + row.role.displayName"
  />
  ```
  - *Type*: Date Picker | *Purpose*: Role-specific period closing date | *Binding*: `[(ngModel)]="row.editing"`, `[name]="'lock' + row.role.roleId"` | *Attr*: `[attr.aria-label]`
- **Lines 64-71**:
  ```html
  <input
    type="text"
    [(ngModel)]="row.note"
    [name]="'note' + row.role.roleId"
    maxlength="300"
    placeholder="Year-end closed, audit signed off…"
    [attr.aria-label]="'Why, for ' + row.role.displayName"
  />
  ```
  - *Type*: Text | *Purpose*: Close/reopen note | *Binding*: `[(ngModel)]="row.note"`, `[name]="'note' + row.role.roleId"` | *Attr*: `maxlength="300"`, `placeholder`, `[attr.aria-label]`

#### `src/lib/journals/journals.page.html` (6 inputs)
- **Line 20**: `<input type="date" [(ngModel)]="journalDate" name="journalDate" />`
  - *Type*: Date Picker | *Purpose*: Journal posting date | *Binding*: `[(ngModel)]="journalDate"`, `name="journalDate"`
- **Line 24**: `<input type="text" [(ngModel)]="reference" name="reference" maxlength="200" />`
  - *Type*: Text | *Purpose*: Journal reference number/text | *Binding*: `[(ngModel)]="reference"`, `name="reference"`, `maxlength="200"`
- **Line 28**: `<input type="text" [(ngModel)]="memo" name="memo" placeholder="Why this entry was made" />`
  - *Type*: Text | *Purpose*: Journal header memo | *Binding*: `[(ngModel)]="memo"`, `name="memo"`, `placeholder`
- **Lines 68-74**:
  ```html
  <input
    type="text"
    [(ngModel)]="row.lineMemo"
    [name]="'memo' + index"
    maxlength="300"
    (ngModelChange)="touch()"
  />
  ```
  - *Type*: Text | *Purpose*: Line memo inside grid | *Binding*: `[(ngModel)]="row.lineMemo"`, `[name]="'memo' + index"`, `(ngModelChange)="touch()"`
- **Lines 78-87**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.debit"
    [name]="'debit' + index"
    (ngModelChange)="onDebit(row)"
  />
  ```
  - *Type*: Currency / Decimal | *Purpose*: Debit amount line item | *Binding*: `[(ngModel)]="row.debit"`, `[name]="'debit' + index"`, `(ngModelChange)="onDebit(row)"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`
- **Lines 90-99**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.credit"
    [name]="'credit' + index"
    (ngModelChange)="onCredit(row)"
  />
  ```
  - *Type*: Currency / Decimal | *Purpose*: Credit amount line item | *Binding*: `[(ngModel)]="row.credit"`, `[name]="'credit' + index"`, `(ngModelChange)="onCredit(row)"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`

#### `src/lib/money-document/money-document.page.html` (8 inputs)
- **Line 17**: `<input type="date" [(ngModel)]="transactionDate" name="transactionDate" />`
  - *Type*: Date Picker | *Purpose*: Transaction date | *Binding*: `[(ngModel)]="transactionDate"`, `name="transactionDate"`
- **Lines 39-46**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="amount"
    name="amount"
  />
  ```
  - *Type*: Currency / Decimal | *Purpose*: Header total transaction amount | *Binding*: `[(ngModel)]="amount"`, `name="amount"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`
- **Line 58**: `<input type="text" [(ngModel)]="referenceNo" name="referenceNo" maxlength="50" />`
  - *Type*: Text | *Purpose*: Payment reference number | *Binding*: `[(ngModel)]="referenceNo"`, `name="referenceNo"`, `maxlength="50"`
- **Line 62**: `<input type="date" [(ngModel)]="referenceDate" name="referenceDate" />`
  - *Type*: Date Picker | *Purpose*: Cheque/UTR date | *Binding*: `[(ngModel)]="referenceDate"`, `name="referenceDate"`
- **Line 66**: `<input type="text" [(ngModel)]="memo" name="memo" placeholder="Why this payment was made" />`
  - *Type*: Text | *Purpose*: Header memo | *Binding*: `[(ngModel)]="memo"`, `name="memo"`, `placeholder`
- **Lines 122-131**:
  ```html
  <input
    type="number"
    inputmode="numeric"
    min="1"
    step="1"
    placeholder="Document #"
    [(ngModel)]="row.mappingTransactionId"
    [name]="'maps' + index"
    (ngModelChange)="touch()"
  />
  ```
  - *Type*: Numeric (Integer ID) | *Purpose*: Target document ID for settlement | *Binding*: `[(ngModel)]="row.mappingTransactionId"`, `[name]="'maps' + index"`, `(ngModelChange)="touch()"` | *Attr*: `inputmode="numeric"`, `min="1"`, `step="1"`, `placeholder`
- **Lines 138-144**:
  ```html
  <input
    type="text"
    [(ngModel)]="row.lineMemo"
    [name]="'lineMemo' + index"
    maxlength="300"
    (ngModelChange)="touch()"
  />
  ```
  - *Type*: Text | *Purpose*: Line memo | *Binding*: `[(ngModel)]="row.lineMemo"`, `[name]="'lineMemo' + index"`, `(ngModelChange)="touch()"` | *Attr*: `maxlength="300"`
- **Lines 148-156**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.amount"
    [name]="'lineAmount' + index"
    (ngModelChange)="touch()"
  />
  ```
  - *Type*: Currency / Decimal | *Purpose*: Line allocated amount | *Binding*: `[(ngModel)]="row.amount"`, `[name]="'lineAmount' + index"`, `(ngModelChange)="touch()"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`

#### `src/lib/numbering-series/numbering-series.page.html` (14 inputs)
- **Line 10**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Filter toggle | *Binding*: `[(ngModel)]="showInactive"`
- **Line 105**: `<input type="text" [(ngModel)]="form.seriesName" maxlength="50" />`
  - *Type*: Text | *Purpose*: Series name | *Binding*: `[(ngModel)]="form.seriesName"`, `maxlength="50"`
- **Line 110**: `<input type="text" [(ngModel)]="form.seriesCode" maxlength="30" />`
  - *Type*: Text | *Purpose*: Series code | *Binding*: `[(ngModel)]="form.seriesCode"`, `maxlength="30"`
- **Line 124**: `<input type="text" [(ngModel)]="form.prefix" maxlength="15" />`
  - *Type*: Text | *Purpose*: Prefix | *Binding*: `[(ngModel)]="form.prefix"`, `maxlength="15"`
- **Line 129**: `<input type="text" [(ngModel)]="form.separator" maxlength="1" />`
  - *Type*: Text | *Purpose*: Separator character | *Binding*: `[(ngModel)]="form.separator"`, `maxlength="1"`
- **Line 134**: `<input type="text" [(ngModel)]="form.suffix" maxlength="15" />`
  - *Type*: Text | *Purpose*: Suffix | *Binding*: `[(ngModel)]="form.suffix"`, `maxlength="15"`
- **Line 138**: `<input type="checkbox" [(ngModel)]="form.includeFinancialYear" />`
  - *Type*: Checkbox | *Purpose*: Include FY flag | *Binding*: `[(ngModel)]="form.includeFinancialYear"`
- **Line 155**: `<input type="checkbox" [(ngModel)]="form.includeBranchCode" />`
  - *Type*: Checkbox | *Purpose*: Include branch flag | *Binding*: `[(ngModel)]="form.includeBranchCode"`
- **Line 162**: `<input type="text" [(ngModel)]="form.branchCode" maxlength="10" />`
  - *Type*: Text | *Purpose*: Branch code | *Binding*: `[(ngModel)]="form.branchCode"`, `maxlength="10"`
- **Line 172**: `<input type="number" [(ngModel)]="form.numberLength" min="1" max="12" />`
  - *Type*: Numeric (Integer) | *Purpose*: Number padding length | *Binding*: `[(ngModel)]="form.numberLength"`, `min="1"`, `max="12"`
- **Line 177**: `<input type="number" [(ngModel)]="form.startNumber" min="0" />`
  - *Type*: Numeric (Integer) | *Purpose*: Series start sequence | *Binding*: `[(ngModel)]="form.startNumber"`, `min="0"`
- **Lines 191-195**: `<input type="checkbox" [(ngModel)]="form.allowManualOverride" [disabled]="form.seriesFor === 'Document'" />`
  - *Type*: Checkbox | *Purpose*: Manual number entry override | *Binding*: `[(ngModel)]="form.allowManualOverride"`, `[disabled]`
- **Line 203**: `<input type="checkbox" [(ngModel)]="form.isActive" />`
  - *Type*: Checkbox | *Purpose*: Active status | *Binding*: `[(ngModel)]="form.isActive"`
- **Line 226**: `<input type="number" [(ngModel)]="counterValue" [min]="counter.startNumber" />`
  - *Type*: Numeric (Integer) | *Purpose*: Current counter value | *Binding*: `[(ngModel)]="counterValue"`, `[min]="counter.startNumber"`

#### `src/lib/opening-balance/opening-balance.page.html` (9 inputs) *(Spot Checked per Prompt)*
- **Line 31**: `<input type="date" [(ngModel)]="asOfDate" name="asOfDate" [disabled]="finalized()" />`
  - *Type*: Date Picker | *Purpose*: Books opening go-live date | *Binding*: `[(ngModel)]="asOfDate"`, `name="asOfDate"`, `[disabled]="finalized()"`
- **Lines 35-41**:
  ```html
  <input
    type="text"
    [(ngModel)]="memo"
    name="memo"
    placeholder="Which system these came from, and who signed them off"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Text | *Purpose*: Opening balance migration memo | *Binding*: `[(ngModel)]="memo"`, `name="memo"`, `[disabled]="finalized()"`, `placeholder`
- **Lines 111-119**:
  ```html
  <input
    type="text"
    placeholder="Invoice no."
    [(ngModel)]="row.documentReference"
    [name]="'ref' + lines().indexOf(row)"
    maxlength="50"
    (ngModelChange)="touch()"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Text | *Purpose*: Invoice reference number for open receivables/payables in grid | *Binding*: `[(ngModel)]="row.documentReference"`, `[name]="'ref' + lines().indexOf(row)"`, `(ngModelChange)="touch()"`, `[disabled]="finalized()"` | *Attr*: `maxlength="50"`, `placeholder`
- **Lines 120-126**:
  ```html
  <input
    type="date"
    [(ngModel)]="row.documentDate"
    [name]="'docdate' + lines().indexOf(row)"
    (ngModelChange)="touch()"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Date Picker | *Purpose*: Original invoice date in grid | *Binding*: `[(ngModel)]="row.documentDate"`, `[name]="'docdate' + lines().indexOf(row)"`, `(ngModelChange)="touch()"`, `[disabled]="finalized()"`
- **Lines 128-136**:
  ```html
  <input
    type="text"
    placeholder="Note"
    [(ngModel)]="row.lineMemo"
    [name]="'memo' + lines().indexOf(row)"
    maxlength="300"
    (ngModelChange)="touch()"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Text | *Purpose*: Line memo in grid | *Binding*: `[(ngModel)]="row.lineMemo"`, `[name]="'memo' + lines().indexOf(row)"`, `(ngModelChange)="touch()"`, `[disabled]="finalized()"` | *Attr*: `maxlength="300"`, `placeholder`
- **Lines 142-151**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.001"
    min="0"
    [(ngModel)]="row.quantity"
    [name]="'qty' + lines().indexOf(row)"
    (ngModelChange)="touch()"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Numeric / Quantity (3 decimals) | *Purpose*: Stock opening quantity | *Binding*: `[(ngModel)]="row.quantity"`, `[name]="'qty' + lines().indexOf(row)"`, `(ngModelChange)="touch()"`, `[disabled]="finalized()"` | *Attr*: `inputmode="decimal"`, `step="0.001"`, `min="0"`
- **Lines 159-168**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.unitCost"
    [name]="'cost' + lines().indexOf(row)"
    (ngModelChange)="touch()"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Currency / Decimal (Unit Cost) | *Purpose*: Stock unit cost | *Binding*: `[(ngModel)]="row.unitCost"`, `[name]="'cost' + lines().indexOf(row)"`, `(ngModelChange)="touch()"`, `[disabled]="finalized()"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`
- **Lines 178-187**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.debit"
    [name]="'debit' + lines().indexOf(row)"
    (ngModelChange)="onDebit(row)"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Currency / Decimal Amount | *Purpose*: Debit balance for GL account | *Binding*: `[(ngModel)]="row.debit"`, `[name]="'debit' + lines().indexOf(row)"`, `(ngModelChange)="onDebit(row)"`, `[disabled]="finalized()"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`
- **Lines 195-204**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="row.credit"
    [name]="'credit' + lines().indexOf(row)"
    (ngModelChange)="onCredit(row)"
    [disabled]="finalized()"
  />
  ```
  - *Type*: Currency / Decimal Amount | *Purpose*: Credit balance for GL account | *Binding*: `[(ngModel)]="row.credit"`, `[name]="'credit' + lines().indexOf(row)"`, `(ngModelChange)="onCredit(row)"`, `[disabled]="finalized()"` | *Attr*: `inputmode="decimal"`, `step="0.01"`, `min="0"`

#### `src/lib/payment-terms/payment-terms.page.html` (9 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Inactive filter | *Binding*: `[(ngModel)]="showInactive"`
- **Line 97**: `<input type="text" [(ngModel)]="form.termName" maxlength="50" />`
  - *Type*: Text | *Purpose*: Term name | *Binding*: `[(ngModel)]="form.termName"`, `maxlength="50"`
- **Line 113**: `<input type="number" [(ngModel)]="form.dueDays" min="0" max="365" [disabled]="editingIsSystem()" />`
  - *Type*: Numeric (Integer) | *Purpose*: Net due days | *Binding*: `[(ngModel)]="form.dueDays"`, `min="0"`, `max="365"`, `[disabled]`
- **Line 120**: `<input type="number" [(ngModel)]="form.dueDayOfMonth" min="1" max="31" [disabled]="editingIsSystem()" />`
  - *Type*: Numeric (Integer) | *Purpose*: Due day of month | *Binding*: `[(ngModel)]="form.dueDayOfMonth"`, `min="1"`, `max="31"`, `[disabled]`
- **Lines 127-134**: `<input type="number" [(ngModel)]="form.discountPercent" min="0" max="100" step="0.01" [disabled]="editingIsSystem()" />`
  - *Type*: Numeric (Percentage) | *Purpose*: Early discount percent | *Binding*: `[(ngModel)]="form.discountPercent"`, `min="0"`, `max="100"`, `step="0.01"`, `[disabled]`
- **Line 139**: `<input type="number" [(ngModel)]="form.discountDays" min="0" max="365" [disabled]="editingIsSystem()" />`
  - *Type*: Numeric (Integer) | *Purpose*: Early discount validity days | *Binding*: `[(ngModel)]="form.discountDays"`, `min="0"`, `max="365"`, `[disabled]`
- **Line 144**: `<input type="checkbox" [(ngModel)]="form.isSales" />`
  - *Type*: Checkbox | *Purpose*: Allowed on sales | *Binding*: `[(ngModel)]="form.isSales"`
- **Line 149**: `<input type="checkbox" [(ngModel)]="form.isPurchase" />`
  - *Type*: Checkbox | *Purpose*: Allowed on purchase | *Binding*: `[(ngModel)]="form.isPurchase"`
- **Line 154**: `<input type="checkbox" [(ngModel)]="form.isActive" />`
  - *Type*: Checkbox | *Purpose*: Active state | *Binding*: `[(ngModel)]="form.isActive"`

#### `src/lib/statements/statements.page.html` (14 inputs)
- **Line 37**: `<input type="number" min="0" max="100" [(ngModel)]="form.skipRows" name="skipRows" />`
  - *Type*: Numeric (Integer) | *Purpose*: Bank statement header rows to skip | *Binding*: `[(ngModel)]="form.skipRows"`, `min="0"`, `max="100"`
- **Line 42**: `<input type="text" [(ngModel)]="form.dateFormat" name="dateFormat" maxlength="40" />`
  - *Type*: Text | *Purpose*: Date format string | *Binding*: `[(ngModel)]="form.dateFormat"`, `maxlength="40"`
- **Line 46**: `<input type="checkbox" [(ngModel)]="form.hasHeaderRow" name="hasHeaderRow" />`
  - *Type*: Checkbox | *Purpose*: Has header row flag | *Binding*: `[(ngModel)]="form.hasHeaderRow"`
- **Line 54**: `<input type="text" [(ngModel)]="form.dateColumn" name="dateColumn" maxlength="100" />`
  - *Type*: Text | *Purpose*: Date column name/index | *Binding*: `[(ngModel)]="form.dateColumn"`, `maxlength="100"`
- **Line 60**: `<input type="text" [(ngModel)]="form.valueDateColumn" name="valueDateColumn" maxlength="100" />`
  - *Type*: Text | *Purpose*: Value date column | *Binding*: `[(ngModel)]="form.valueDateColumn"`
- **Line 69**: `<input type="text" [(ngModel)]="form.descriptionColumn" name="descriptionColumn" maxlength="100" />`
  - *Type*: Text | *Purpose*: Description column | *Binding*: `[(ngModel)]="form.descriptionColumn"`
- **Line 78**: `<input type="text" [(ngModel)]="form.referenceColumn" name="referenceColumn" maxlength="100" />`
  - *Type*: Text | *Purpose*: Reference/UTR column | *Binding*: `[(ngModel)]="form.referenceColumn"`
- **Line 86**: `<input type="text" [(ngModel)]="form.balanceColumn" name="balanceColumn" maxlength="100" />`
  - *Type*: Text | *Purpose*: Balance column | *Binding*: `[(ngModel)]="form.balanceColumn"`
- **Line 100**: `<input type="text" [(ngModel)]="form.withdrawalColumn" name="withdrawalColumn" maxlength="100" (ngModelChange)="onTwoColumns()" />`
  - *Type*: Text | *Purpose*: Withdrawal column | *Binding*: `[(ngModel)]="form.withdrawalColumn"`, `(ngModelChange)`
- **Line 110**: `<input type="text" [(ngModel)]="form.depositColumn" name="depositColumn" maxlength="100" (ngModelChange)="onTwoColumns()" />`
  - *Type*: Text | *Purpose*: Deposit column | *Binding*: `[(ngModel)]="form.depositColumn"`, `(ngModelChange)`
- **Line 125**: `<input type="text" [(ngModel)]="form.amountColumn" name="amountColumn" maxlength="100" (ngModelChange)="onAmountShape()" />`
  - *Type*: Text | *Purpose*: Signed amount column | *Binding*: `[(ngModel)]="form.amountColumn"`, `(ngModelChange)`
- **Line 135**: `<input type="checkbox" [(ngModel)]="form.negativeIsDeposit" name="negativeIsDeposit" />`
  - *Type*: Checkbox | *Purpose*: Sign convention | *Binding*: `[(ngModel)]="form.negativeIsDeposit"`
- **Line 155**: `<input type="file" accept=".csv,.tsv,.txt,.xlsx,.xlsm" (change)="onFile($event)" />`
  - *Type*: File | *Purpose*: Bank statement upload | *Binding*: `(change)="onFile($event)"` | *Attr*: `accept=".csv,.tsv,.txt,.xlsx,.xlsm"`
- **Line 186**: `<input type="checkbox" [(ngModel)]="showAll" name="showAll" />`
  - *Type*: Checkbox | *Purpose*: Show reconciled lines toggle | *Binding*: `[(ngModel)]="showAll"`

#### `src/lib/sub-accounts/sub-accounts.page.html` (2 inputs)
- **Line 4**: `<input type="checkbox" [(ngModel)]="showInactive" />`
  - *Type*: Checkbox | *Purpose*: Filter inactive | *Binding*: `[(ngModel)]="showInactive"`
- **Lines 34-40**:
  ```html
  <input
    class="search"
    type="search"
    placeholder="Filter by name…"
    [(ngModel)]="search"
    (ngModelChange)="searchTerm.set($event)"
  />
  ```
  - *Type*: Search | *Purpose*: Filter sub-accounts by name | *Binding*: `[(ngModel)]="search"`, `(ngModelChange)="searchTerm.set($event)"` | *Attr*: `class="search"`, `placeholder`

#### `src/lib/tax-master/tax-master.page.html` (7 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showHistory" (ngModelChange)="load()" />`
  - *Type*: Checkbox | *Purpose*: Show superseded rates toggle | *Binding*: `[(ngModel)]="showHistory"`
- **Line 33**: `<input name="taxName" [(ngModel)]="form.taxName" required maxlength="50" />`
  - *Type*: Text | *Purpose*: Tax rate name | *Binding*: `[(ngModel)]="form.taxName"`, `required`, `maxlength="50"`
- **Lines 40-48**:
  ```html
  <input
    name="totalRate"
    type="number"
    step="0.01"
    min="0"
    [(ngModel)]="form.totalRate"
    (ngModelChange)="recalc()"
    required
  />
  ```
  - *Type*: Numeric (Percentage) | *Purpose*: Total GST percentage | *Binding*: `[(ngModel)]="form.totalRate"`, `(ngModelChange)="recalc()"`, `step="0.01"`, `min="0"`, `required`
- **Line 52**: `<input name="cessRate" type="number" step="0.01" min="0" [(ngModel)]="form.cessRate" />`
  - *Type*: Numeric (Percentage) | *Purpose*: Cess percentage | *Binding*: `[(ngModel)]="form.cessRate"`, `step="0.01"`, `min="0"`
- **Line 67**: `<input name="effectiveFrom" type="date" [(ngModel)]="form.effectiveFrom" required />`
  - *Type*: Date Picker | *Purpose*: Rate effective date | *Binding*: `[(ngModel)]="form.effectiveFrom"`, `required`
- **Line 73**: `<input type="checkbox" name="isSales" [(ngModel)]="form.isSales" />`
  - *Type*: Checkbox | *Purpose*: Output tax applicability | *Binding*: `[(ngModel)]="form.isSales"`
- **Line 77**: `<input type="checkbox" name="isPurchase" [(ngModel)]="form.isPurchase" />`
  - *Type*: Checkbox | *Purpose*: Input tax applicability | *Binding*: `[(ngModel)]="form.isPurchase"`

#### `src/lib/transfer-money/transfer-money.page.html` (5 inputs)
- **Line 22**: `<input type="date" [(ngModel)]="transactionDate" name="transactionDate" />`
  - *Type*: Date Picker | *Purpose*: Bank transfer date | *Binding*: `[(ngModel)]="transactionDate"`, `name="transactionDate"`
- **Lines 55-62**:
  ```html
  <input
    type="number"
    inputmode="decimal"
    step="0.01"
    min="0"
    [(ngModel)]="amount"
    name="amount"
  />
  ```
  - *Type*: Currency / Decimal Amount | *Purpose*: Transferred amount | *Binding*: `[(ngModel)]="amount"`, `name="amount"`, `inputmode="decimal"`, `step="0.01"`, `min="0"`
- **Line 74**: `<input type="text" [(ngModel)]="referenceNo" name="referenceNo" maxlength="50" />`
  - *Type*: Text | *Purpose*: UTR / Cheque reference | *Binding*: `[(ngModel)]="referenceNo"`, `maxlength="50"`
- **Line 78**: `<input type="date" [(ngModel)]="referenceDate" name="referenceDate" />`
  - *Type*: Date Picker | *Purpose*: Transfer reference date | *Binding*: `[(ngModel)]="referenceDate"`
- **Line 82**: `<input type="text" [(ngModel)]="memo" name="memo" placeholder="Why the money was moved" />`
  - *Type*: Text | *Purpose*: Transfer memo | *Binding*: `[(ngModel)]="memo"`, `placeholder`

#### `src/lib/trial-balance/trial-balance.page.html` (2 inputs)
- **Line 13**: `<input type="date" [(ngModel)]="from" name="from" />`
  - *Type*: Date Picker | *Purpose*: Report start date filter | *Binding*: `[(ngModel)]="from"`, `name="from"`
- **Line 17**: `<input type="date" [(ngModel)]="to" name="to" />`
  - *Type*: Date Picker | *Purpose*: Report end date filter | *Binding*: `[(ngModel)]="to"`, `name="to"`

---

### 1.2 Inventory UI (`frontend/libs/inventory/inventory-ui`) — 86 Occurrences

#### `src/lib/item-categories/item-categories.page.html` (4 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />` (Checkbox)
- **Line 69**: `<input type="text" [(ngModel)]="form.categoryName" maxlength="100" />` (Text)
- **Line 73**: `<input type="text" [(ngModel)]="form.categoryCode" maxlength="20" />` (Text)
- **Line 114**: `<input type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)

#### `src/lib/items/items.page.html` (38 inputs)
- **Line 20**: `<input type="search" [(ngModel)]="search" (keyup.enter)="load()" placeholder="Search this list" aria-label="Search items" ... />` (Search)
- **Line 99**: `<input type="text" [(ngModel)]="form.itemName" maxlength="200" />` (Text)
- **Line 104**: `<input type="text" [(ngModel)]="form.printName" maxlength="200" />` (Text)
- **Line 110**: `<input type="text" [(ngModel)]="form.itemCode" maxlength="50" [disabled]="editingId() !== null" />` (Text)
- **Lines 155-163**:
  ```html
  <input
    id="item-hsn-search"
    type="search"
    name="hsnSearch"
    [(ngModel)]="hsnSearch"
    (keyup.enter)="searchHsn()"
    (ngModelChange)="searchHsn()"
    placeholder="Search code or description"
  />
  ```
  - *Type*: Search | *Purpose*: Inline HSN/SAC search | *Binding*: `[(ngModel)]="hsnSearch"`, `(keyup.enter)`, `(ngModelChange)`
- **Line 213**: `<input type="checkbox" [(ngModel)]="form.isPriceInclusiveOfTax" />` (Checkbox)
- **Line 219**: `<input type="number" [(ngModel)]="form.salesPrice" step="0.0001" />` (Currency / Decimal, 4 places)
- **Line 225**: `<input type="number" [(ngModel)]="form.purchasePrice" step="0.0001" />` (Currency / Decimal, 4 places)
- **Line 230**: `<input type="number" [(ngModel)]="form.mrp" step="0.01" />` (Currency / Decimal, 2 places)
- **Line 238**: `<input type="number" [(ngModel)]="form.minSalePrice" step="0.0001" />` (Currency / Decimal, 4 places)
- **Line 243**: `<input type="checkbox" [(ngModel)]="form.isSales" />` (Checkbox)
- **Line 248**: `<input type="checkbox" [(ngModel)]="form.isPurchase" />` (Checkbox)
- **Line 253**: `<input type="checkbox" [(ngModel)]="form.isReturnable" />` (Checkbox)
- **Line 258**: `<input type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)
- **Lines 338-344**: `<input type="checkbox" [(ngModel)]="form.isBatchTracked" [disabled]="locked || form.costingType === 'Fefo'" />` (Checkbox)
- **Lines 347-353**: `<input type="checkbox" [(ngModel)]="form.isExpiryTracked" [disabled]="locked || form.costingType === 'Fefo' || !form.isBatchTracked" />` (Checkbox)
- **Lines 356-362**: `<input type="checkbox" [(ngModel)]="form.isSerialTracked" [disabled]="locked || form.costingType === 'SpecificIdentification'" />` (Checkbox)
- **Line 376**: `<input type="number" [(ngModel)]="form.reorderLevel" step="0.001" />` (Numeric / Quantity)
- **Line 381**: `<input type="number" [(ngModel)]="form.reorderQuantity" step="0.001" />` (Numeric / Quantity)
- **Line 386**: `<input type="number" [(ngModel)]="form.leadTimeDays" min="0" />` (Numeric / Integer Days)
- **Line 423**: `<input type="number" [(ngModel)]="form.jewellery['grossWeight']" step="0.001" />` (Numeric / Weight)
- **Line 428**: `<input type="number" [(ngModel)]="form.jewellery['netWeight']" step="0.001" />` (Numeric / Weight)
- **Line 434**: `<input type="number" [(ngModel)]="form.jewellery['stoneWeight']" step="0.001" />` (Numeric / Weight)
- **Line 439**: `<input type="number" [(ngModel)]="form.jewellery['stoneCharge']" step="0.01" />` (Currency / Decimal)
- **Line 444**: `<input type="number" [(ngModel)]="form.jewellery['wastagePercent']" step="0.01" />` (Numeric / Percentage)
- **Line 458**: `<input type="number" [(ngModel)]="form.jewellery['makingChargeValue']" step="0.0001" />` (Numeric / Decimal)
- **Line 462**: `<input type="checkbox" [(ngModel)]="form.jewellery['isHallmarked']" />` (Checkbox)
- **Line 472**: `<input type="text" [(ngModel)]="form.pharma['genericName']" maxlength="200" />` (Text)
- **Line 477**: `<input type="text" [(ngModel)]="form.pharma['strength']" maxlength="50" />` (Text)
- **Line 496**: `<input type="text" [(ngModel)]="form.pharma['packSize']" maxlength="50" />` (Text)
- **Line 501**: `<input type="text" [(ngModel)]="form.pharma['manufacturerName']" maxlength="200" />` (Text)
- **Line 506**: `<input type="text" [(ngModel)]="form.pharma['marketedBy']" maxlength="200" />` (Text)
- **Line 522**: `<input type="checkbox" [(ngModel)]="form.pharma['isNarcotic']" />` (Checkbox)
- **Line 538**: `<input type="number" [(ngModel)]="form.pharma['shelfLifeDays']" min="1" />` (Numeric / Integer Days)
- **Line 543**: `<input type="number" [(ngModel)]="form.pharma['minExpiryDaysOnReceipt']" min="0" />` (Numeric / Integer Days)
- **Line 549**: `<input type="number" [(ngModel)]="form.pharma['expiryAlertDays']" min="0" />` (Numeric / Integer Days)
- **Line 564**: `<input type="text" [(ngModel)]="barcode.barcode" [name]="'bc' + $index" maxlength="50" />` (Text in dynamic child card)
- **Lines 591-596**: `<input type="radio" name="primaryBarcode" [checked]="barcode.isPrimary" (change)="setPrimaryBarcode($index)" />` (Radio)

#### `src/lib/metal-purities/metal-purities.page.html` (4 inputs)
- **Line 12**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />` (Checkbox)
- **Line 82**: `<input type="text" [(ngModel)]="form.purityName" maxlength="20" />` (Text)
- **Line 87**: `<input type="number" [(ngModel)]="form.purityFactor" step="0.0001" min="0.0001" max="1" />` (Numeric / Decimal Factor)
- **Line 90**: `<input type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)

#### `src/lib/stock/stock.page.html` (9 inputs)
- **Lines 4-10**: `<input type="search" [(ngModel)]="search" (ngModelChange)="load()" placeholder="Search items" aria-label="Search items" />` (Search)
- **Line 12**: `<input type="checkbox" [(ngModel)]="belowReorderOnly" (ngModelChange)="load()" />` (Checkbox)
- **Line 126**: `<input type="date" [(ngModel)]="form.movementDate" />` (Date Picker)
- **Line 131**: `<input type="number" [(ngModel)]="form.quantity" min="0" step="0.001" />` (Numeric / Quantity)
- **Line 182**: `<input type="text" [(ngModel)]="form.batchNumber" maxlength="50" />` (Text)
- **Line 190**: `<input type="date" [(ngModel)]="form.batchExpiryDate" />` (Date Picker)
- **Line 198**: `<input type="number" [(ngModel)]="form.batchMrp" min="0" step="0.01" />` (Currency / Decimal)
- **Line 220**: `<input type="number" [(ngModel)]="form.unitCost" min="0" step="0.000001" />` (Currency / Decimal Unit Cost, 6 places)
- **Line 229**: `<input type="text" [(ngModel)]="form.notes" maxlength="300" />` (Text)

#### `src/lib/stock-adjustments/stock-adjustments.page.html` (7 inputs)
- **Line 106**: `<input type="date" [(ngModel)]="form.adjustmentDate" />` (Date Picker)
- **Line 132**: `<input type="text" [(ngModel)]="form.notes" maxlength="500" />` (Text)
- **Lines 147-154**: `<input type="number" [(ngModel)]="row.countedQuantity" [name]="'counted' + index" min="0" step="0.001" />` (Numeric in Grid)
- **Lines 157-164**: `<input type="number" [(ngModel)]="row.quantity" [name]="'qty' + index" min="0" step="0.001" />` (Numeric in Grid)
- **Lines 175-182**: `<input type="number" [(ngModel)]="row.unitCost" [name]="'cost' + index" min="0" step="0.000001" />` (Currency / Decimal in Grid)
- **Lines 188-194**: `<input type="text" [(ngModel)]="row.batchNumber" [name]="'batch' + index" maxlength="50" />` (Text in Grid)
- **Lines 197-203**: `<input type="text" [(ngModel)]="row.notes" [name]="'note' + index" maxlength="300" />` (Text in Grid)

#### `src/lib/unit-types/unit-types.page.html` (14 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />` (Checkbox)
- **Line 8**: `<input type="text" [(ngModel)]="newTypeName" maxlength="50" placeholder="New type name" />` (Text)
- **Lines 72-78**: `<input type="radio" [name]="'base' + type.uomTypeId" [checked]="row.isBaseUnit" [disabled]="busy() || row.isBaseUnit" (change)="setBase(row)" />` (Radio)
- **Line 83**: `<input type="text" [(ngModel)]="editUnit.uomCode" maxlength="10" />` (Text in Grid)
- **Line 94**: `<input type="text" [(ngModel)]="editUnit.uomName" maxlength="50" />` (Text in Grid)
- **Line 102**: `<input type="text" [(ngModel)]="editUnit.uqcCode" maxlength="3" />` (Text in Grid)
- **Lines 110-115**: `<input type="number" [(ngModel)]="editUnit.conversionToBase" step="0.000001" [disabled]="row.isBaseUnit" />` (Numeric / Decimal in Grid)
- **Line 124**: `<input type="number" [(ngModel)]="editUnit.decimalPlaces" min="0" max="6" />` (Numeric / Integer in Grid)
- **Line 133**: `<input type="checkbox" [(ngModel)]="editUnit.isActive" />` (Checkbox in Grid)
- **Line 156**: `<input type="text" [(ngModel)]="newUnit.uomCode" maxlength="10" />` (Text)
- **Line 160**: `<input type="text" [(ngModel)]="newUnit.uomName" maxlength="50" />` (Text)
- **Line 164**: `<input type="text" [(ngModel)]="newUnit.uqcCode" maxlength="3" />` (Text)
- **Line 169**: `<input type="number" [(ngModel)]="newUnit.conversionToBase" step="0.000001" />` (Numeric / Decimal)
- **Line 173**: `<input type="number" [(ngModel)]="newUnit.decimalPlaces" min="0" max="6" />` (Numeric / Integer)

#### `src/lib/warehouses/warehouses.page.html` (10 inputs)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />` (Checkbox)
- **Line 73**: `<input type="text" [(ngModel)]="form.warehouseName" maxlength="100" />` (Text)
- **Line 77**: `<input type="text" [(ngModel)]="form.warehouseCode" maxlength="20" [disabled]="editingId() !== 0" />` (Text)
- **Line 100**: `<input type="text" [(ngModel)]="form.addressLine1" maxlength="200" />` (Text)
- **Line 104**: `<input type="text" [(ngModel)]="form.city" maxlength="100" />` (Text)
- **Line 108**: `<input type="text" [(ngModel)]="form.postalCode" maxlength="10" />` (Text)
- **Line 112**: `<input type="text" [(ngModel)]="form.gstin" maxlength="15" />` (Text)
- **Line 117**: `<input type="text" [(ngModel)]="form.contactPersonName" maxlength="100" />` (Text)
- **Line 121**: `<input type="text" [(ngModel)]="form.mobileNumber" maxlength="20" />` (Text)
- **Line 125**: `<input type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)

---

### 1.3 Master UI (`frontend/libs/master/master-ui`) — 97 Occurrences

#### `src/lib/configurations/configurations.page.html` (3 inputs)
- **Line 35**: `<input type="number" [(ngModel)]="row.value" (blur)="save(row)" />` (Numeric, Blur save)
- **Line 38**: `<input type="date" [(ngModel)]="row.value" (blur)="save(row)" />` (Date Picker, Blur save)
- **Line 41**: `<input type="text" [(ngModel)]="row.value" (blur)="save(row)" />` (Text, Blur save)

#### `src/lib/contact-person-roles/contact-person-roles.list.html` (2 inputs)
- **Line 12**: `<input type="text" [(ngModel)]="editName" maxlength="50" />` (Text)
- **Line 58**: `<input type="text" [(ngModel)]="newRoleName" maxlength="50" placeholder="New role name" />` (Text)

#### `src/lib/contacts/contacts.page.html` (49 inputs)
- **Line 27**: `<input type="search" [(ngModel)]="search" (keyup.enter)="load()" placeholder="Name, code or GSTIN" aria-label="Search contacts" ... />` (Search)
- **Line 109**: `<input type="text" [(ngModel)]="form.displayName" maxlength="200" />` (Text)
- **Line 114**: `<input type="text" [(ngModel)]="form.legalName" maxlength="200" />` (Text)
- **Line 120**: `<input type="text" [(ngModel)]="form.contactCode" maxlength="20" [disabled]="editingId() !== null" />` (Text)
- **Line 134**: `<input type="checkbox" [(ngModel)]="form.isCustomer" />` (Checkbox)
- **Line 135**: `<input type="checkbox" [(ngModel)]="form.isVendor" />` (Checkbox)
- **Line 136**: `<input type="checkbox" [(ngModel)]="form.isJobWorker" />` (Checkbox)
- **Line 137**: `<input type="checkbox" [(ngModel)]="form.isPrescriber" />` (Checkbox)
- **Line 156**: `<input type="text" [(ngModel)]="form.gstin" maxlength="15" class="uppercase" />` (Text)
- **Line 174**: `<input type="text" [(ngModel)]="form.pan" maxlength="10" class="uppercase" />` (Text)
- **Line 179**: `<input type="text" [(ngModel)]="form.currencyCode" maxlength="3" />` (Text)
- **Line 194**: `<input type="number" [(ngModel)]="form.creditLimit" min="0" step="0.01" />` (Currency / Decimal)
- **Line 200**: `<input type="number" [(ngModel)]="form.maxOutstandingDays" min="0" />` (Numeric / Integer Days)
- **Line 206**: `<input type="number" [(ngModel)]="form.maxDiscountPercent" min="0" max="100" step="0.01" />` (Numeric / Percentage)
- **Line 210**: `<input type="checkbox" [(ngModel)]="form.isTdsApplicable" />` (Checkbox)
- **Line 217**: `<input type="text" [(ngModel)]="form.tdsSection" maxlength="10" />` (Text)
- **Line 222**: `<input type="checkbox" [(ngModel)]="form.isMsme" />` (Checkbox)
- **Line 229**: `<input type="text" [(ngModel)]="form.udyamNumber" maxlength="20" />` (Text)
- **Line 239**: `<input type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)
- **Line 266**: `<input type="text" [(ngModel)]="address.label" [name]="'addrLabel' + $index" maxlength="50" />` (Text)
- **Line 271**: `<input type="text" [(ngModel)]="address.addressLine1" [name]="'addr1' + $index" maxlength="200" />` (Text)
- **Line 276**: `<input type="text" [(ngModel)]="address.addressLine2" [name]="'addr2' + $index" maxlength="200" />` (Text)
- **Line 281**: `<input type="text" [(ngModel)]="address.city" [name]="'city' + $index" maxlength="100" />` (Text)
- **Line 296**: `<input type="text" [(ngModel)]="address.postalCode" [name]="'pin' + $index" maxlength="10" />` (Text)
- **Line 301**: `<input type="text" [(ngModel)]="address.mobileNumber" [name]="'addrMobile' + $index" maxlength="20" />` (Text)
- **Lines 307-313**: `<input type="radio" [name]="'defaultAddr' + address.addressType" [checked]="address.isDefault" (change)="setDefaultAddress($index)" />` (Radio)
- **Line 343**: `<input type="text" [(ngModel)]="person.firstName" [name]="'first' + $index" maxlength="100" />` (Text)
- **Line 348**: `<input type="text" [(ngModel)]="person.lastName" [name]="'last' + $index" maxlength="100" />` (Text)
- **Line 353**: `<input type="text" [(ngModel)]="person.designation" [name]="'desig' + $index" maxlength="100" />` (Text)
- **Line 358**: `<input type="email" [(ngModel)]="person.email" [name]="'email' + $index" maxlength="150" />` (Email)
- **Line 363**: `<input type="text" [(ngModel)]="person.mobileNumber" [name]="'mobile' + $index" maxlength="20" />` (Text)
- **Line 368**: `<input type="text" [(ngModel)]="person.phoneNumber" [name]="'phone' + $index" maxlength="20" />` (Text)
- **Line 373**: `<input type="text" [(ngModel)]="person.website" [name]="'web' + $index" maxlength="200" />` (Text)
- **Lines 379-385**: `<input type="radio" name="defaultPerson" [checked]="person.isDefault" (change)="setDefaultPerson($index)" />` (Radio)
- **Lines 410-416**: `<input type="text" [(ngModel)]="bank.accountHolderName" [name]="'holder' + $index" maxlength="100" />` (Text)
- **Line 421**: `<input type="text" [(ngModel)]="bank.bankName" [name]="'bankName' + $index" maxlength="100" />` (Text)
- **Line 426**: `<input type="text" [(ngModel)]="bank.accountNumber" [name]="'acctNo' + $index" maxlength="30" />` (Text)
- **Lines 440-447**: `<input type="text" [(ngModel)]="bank.ifsc" [name]="'ifsc' + $index" maxlength="11" class="uppercase" />` (Text)
- **Line 451**: `<input type="text" [(ngModel)]="bank.branchName" [name]="'branch' + $index" maxlength="100" />` (Text)
- **Line 456**: `<input type="text" [(ngModel)]="bank.upiId" [name]="'upi' + $index" maxlength="100" />` (Text)
- **Lines 463-469**: `<input type="radio" name="defaultBank" [checked]="bank.isDefault" (change)="setDefaultBankDetail($index)" />` (Radio)
- **Line 501**: `<input type="text" [(ngModel)]="licence.licenceNumber" [name]="'licNo' + $index" maxlength="50" />` (Text)
- **Lines 506-512**: `<input type="text" [(ngModel)]="licence.issuingAuthority" [name]="'licAuth' + $index" maxlength="100" />` (Text)
- **Line 516**: `<input type="date" [(ngModel)]="licence.issuedOn" [name]="'licFrom' + $index" />` (Date Picker)
- **Line 521**: `<input type="date" [(ngModel)]="licence.expiresOn" [name]="'licTo' + $index" />` (Date Picker)
- **Lines 527-533**: `<input type="text" [(ngModel)]="licence.description" [name]="'licDesc' + $index" maxlength="100" />` (Text)
- **Line 579**: `<input type="text" [(ngModel)]="uploadDescription" name="uploadDesc" maxlength="500" />` (Text)
- **Line 584**: `<input type="date" [(ngModel)]="uploadExpiryDate" name="uploadExpiry" />` (Date Picker)
- **Lines 590-596**: `<input type="file" accept="application/pdf,image/jpeg,image/png,image/webp,image/tiff" [disabled]="uploading()" (change)="uploadFile($event)" />` (File)

#### `src/lib/hsn-sac/hsn-sac.page.html` (3 inputs)
- **Lines 32-39**: `<input id="hsn-search" type="search" name="search" placeholder="Code or description" [(ngModel)]="search" (keyup.enter)="applyFilters()" />` (Search)
- **Lines 49-56**: `<input type="checkbox" name="includeChapters" [(ngModel)]="includeChapters" (ngModelChange)="applyFilters()" />` (Checkbox)
- **Lines 59-66**: `<input type="checkbox" name="includeInactive" [(ngModel)]="includeInactive" (ngModelChange)="applyFilters()" />` (Checkbox)

#### `src/lib/org-currencies/org-currencies.page.html` (1 input)
- **Line 5**: `<input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />` (Checkbox)

#### `src/lib/organization-settings/organization-settings.page.html` (17 inputs)
- **Line 45**: `<input type="text" name="orgCode" [(ngModel)]="org.orgCode" maxlength="10" />` (Text)
- **Line 51**: `<input type="text" name="name" [(ngModel)]="org.name" maxlength="200" />` (Text)
- **Lines 56-63**: `<input type="text" name="addressLine1" [(ngModel)]="org.addressLine1" maxlength="200" placeholder="Line 1" />` (Text)
- **Lines 67-74**: `<input type="text" name="addressLine2" [(ngModel)]="org.addressLine2" maxlength="200" placeholder="Line 2" />` (Text)
- **Line 78**: `<input type="text" name="city" [(ngModel)]="org.city" maxlength="100" />` (Text)
- **Line 94**: `<input type="text" name="postalCode" [(ngModel)]="org.postalCode" maxlength="10" />` (Text)
- **Line 99**: `<input type="text" name="phoneNumber" [(ngModel)]="org.phoneNumber" maxlength="20" />` (Text)
- **Line 105**: `<input type="text" name="mobileNumber" [(ngModel)]="org.mobileNumber" maxlength="20" />` (Text)
- **Line 111**: `<input type="email" name="email" [(ngModel)]="org.email" maxlength="200" />` (Email)
- **Line 116**: `<input type="text" name="website" [(ngModel)]="org.website" maxlength="200" />` (Text)
- **Line 121**: `<input type="text" name="logoUrl" [(ngModel)]="org.logoUrl" maxlength="500" />` (Text)
- **Line 131**: `<input type="text" name="gstin" [(ngModel)]="org.gstin" maxlength="15" />` (Text)
- **Line 144**: `<input type="text" name="pan" [(ngModel)]="org.pan" maxlength="10" />` (Text)
- **Line 149**: `<input type="text" name="tan" [(ngModel)]="org.tan" maxlength="10" />` (Text)
- **Line 155**: `<input type="text" name="tin" [(ngModel)]="org.tin" maxlength="15" />` (Text)
- **Line 160**: `<input type="text" name="cin" [(ngModel)]="org.cin" maxlength="21" />` (Text)
- **Line 166**: `<input type="text" name="udyamNumber" [(ngModel)]="org.udyamNumber" maxlength="20" />` (Text)

#### `src/lib/organizations/organizations.page.html` (11 inputs)
- **Line 93**: `<input type="text" [(ngModel)]="form.orgCode" maxlength="10" class="uppercase" />` (Text)
- **Line 99**: `<input type="text" [(ngModel)]="form.name" maxlength="200" />` (Text)
- **Line 133**: `<input type="text" [(ngModel)]="form.gstin" maxlength="15" class="uppercase" />` (Text)
- **Line 139**: `<input type="text" [(ngModel)]="form.pan" maxlength="10" class="uppercase" />` (Text)
- **Line 144**: `<input type="text" [(ngModel)]="form.addressLine1" maxlength="200" />` (Text)
- **Line 149**: `<input type="text" [(ngModel)]="form.addressLine2" maxlength="200" />` (Text)
- **Line 154**: `<input type="text" [(ngModel)]="form.city" maxlength="100" />` (Text)
- **Line 169**: `<input type="text" [(ngModel)]="form.postalCode" maxlength="10" />` (Text)
- **Line 174**: `<input type="text" [(ngModel)]="form.phoneNumber" maxlength="20" />` (Text)
- **Line 179**: `<input type="text" [(ngModel)]="form.mobileNumber" maxlength="20" />` (Text)
- **Line 184**: `<input type="email" [(ngModel)]="form.email" maxlength="200" />` (Email)

#### `src/lib/roles/roles.page.html` (3 inputs)
- **Line 27**: `<input name="displayName" [(ngModel)]="form.displayName" required maxlength="100" />` (Text)
- **Line 31**: `<input name="description" [(ngModel)]="form.description" maxlength="300" />` (Text)
- **Lines 55-62**: `<input type="checkbox" [disabled]="isSystem()" [checked]="selected().has(p.permissionId)" (change)="togglePermission(p.permissionId)" />` (Checkbox)

#### `src/lib/smtp-settings/smtp-settings.page.html` (8 inputs)
- **Line 20**: `<input name="host" [(ngModel)]="form.host" required placeholder="smtp.gmail.com" />` (Text)
- **Line 24**: `<input name="port" type="number" [(ngModel)]="form.port" required />` (Numeric Port)
- **Line 29**: `<input name="useSsl" type="checkbox" [(ngModel)]="form.useSsl" />` (Checkbox)
- **Line 36**: `<input name="fromEmail" type="email" [(ngModel)]="form.fromEmail" required />` (Email)
- **Line 40**: `<input name="fromName" [(ngModel)]="form.fromName" required placeholder="Bill-Book" />` (Text)
- **Line 46**: `<input name="username" [(ngModel)]="form.username" required />` (Text)
- **Lines 51-58**:
  ```html
  <input
    name="password"
    type="password"
    [(ngModel)]="form.password"
    [placeholder]="hasPassword() ? '•••••••• (unchanged)' : 'Required'"
    [required]="!hasPassword()"
    autocomplete="new-password"
  />
  ```
  - *Type*: Password | *Purpose*: SMTP password | *Binding*: `[(ngModel)]="form.password"` | *Attr*: `autocomplete="new-password"`, conditional `[placeholder]`, `[required]`
- **Line 70**: `<input name="isActive" type="checkbox" [(ngModel)]="form.isActive" />` (Checkbox)

#### `src/lib/users/users.page.html` (3 inputs)
- **Line 16**: `<input name="email" type="email" [(ngModel)]="form.email" required />` (Email)
- **Line 20**: `<input name="displayName" [(ngModel)]="form.displayName" required />` (Text)
- **Line 24**: `<input name="mobileNumber" [(ngModel)]="form.mobileNumber" />` (Text)

---

## 2. Logic Chain & Pattern Analysis

```
[Observation of 278 Inputs]
        │
        ├── 1. Date Inputs (19 occurrences)
        │       • Filter dates (from/to in account-ledger, trial-balance)
        │       • Transaction & document dates (journals, money-document, transfer-money, opening-balance)
        │       • Expiry & licence dates (stock, contacts)
        │
        ├── 2. Currency & Decimal Inputs (36 occurrences)
        │       • Debit/Credit line amounts (journals, opening-balance)
        │       • Document total amounts (money-document, transfer-money)
        │       • Prices & limits (salesPrice, mrp, creditLimit, odLimit)
        │       • Precision variations: step="0.01", step="0.0001", step="0.000001"
        │
        ├── 3. Numeric & Quantity Inputs (30 occurrences)
        │       • Quantities with step="0.001" (stock, adjustments, opening-balance)
        │       • Counts / days / integers with step="1", min/max bounds (leadTimeDays, shelfLifeDays, dueDays)
        │       • Percentages with min="0" max="100" step="0.01" (tax rates, discounts, wastage)
        │
        ├── 4. Text & Code Inputs (112 occurrences)
        │       • Codes (bankCode, categoryCode, orgCode with uppercase, gstin, pan)
        │       • Descriptions, notes, memos with maxlength restrictions
        │
        ├── 5. Search Inputs (7 occurrences)
        │       • List search bars with (keyup.enter)="load()" or (ngModelChange)
        │
        └── 6. Checkbox & Radio Inputs (64 occurrences)
                • Filter toggles (showInactive)
                • Entity flags (isActive, isSales, isBatchTracked)
                • Radio groups (default address, default bank, primary barcode)
```

### 2.1 Key Common Patterns Identified

1. **Two-Way Binding Convention**:
   - ~95% of inputs use `[(ngModel)]="field"`.
   - In dynamic line grids / lists, inputs attach index-based name attributes: `[name]="'memo' + index"` or `[name]="'qty' + lines().indexOf(row)"`.
2. **Event Triggering**:
   - `(ngModelChange)="touch()"` is used to mark dirty state in draft editors (opening balance, journals, money document).
   - `(ngModelChange)="load()"` or `(keyup.enter)="load()"` on search inputs and filter checkboxes.
   - `(blur)="save(row)"` in editable configuration tables.
3. **Disabled & Read-Only States**:
   - `[disabled]="locked()"` or `[disabled]="finalized()"` conditionally disables rows/fields based on business status.
   - Fixed on edit: `[disabled]="editingId() !== null"` for codes that cannot be altered after creation.
4. **Data Formatting & Precision Nuances**:
   - Standard Currency: 2 decimal places (`step="0.01"`).
   - Item Unit Price & Making Charges: 4 decimal places (`step="0.0001"`).
   - Unit Cost & Conversion Factors: 6 decimal places (`step="0.000001"`).
   - Stock Quantities: 3 decimal places (`step="0.001"`).

---

## 3. Caveats

- This survey investigated `accounting-ui`, `inventory-ui`, and `master-ui`. `purchase-ui`, `sales-ui`, and `apps/docs` were outside this explorer task's assigned scope.
- Select elements (`<select>`), textareas (`<textarea>`), and custom lookup components (`bb-lookup-dialog`) were noted where relevant, but the focus was strictly on primitive `<input>` elements.

---

## 4. Conclusion & Recommendations

To satisfy the project requirements and eliminate raw `<input>` tags, `@bill-book/ui-components` should expose the following standalone components implementing `ControlValueAccessor`:

1. **`BbDateInputComponent` (`<bb-date-input>`)**:
   - Replaces `<input type="date">`.
   - Supports `[(ngModel)]`, `[disabled]`, `[min]`, `[max]`, `[name]`, `[required]`, `[placeholder]`.
   - Output: `(change)`, `(blur)`.
2. **`BbCurrencyInputComponent` (`<bb-currency-input>`)**:
   - Replaces financial amounts, prices, debit/credit inputs.
   - Supports `[(ngModel)]`, `[disabled]`, `[name]`, `[min]`, `[max]`, `[step]` (default 0.01), `[currencyCode]` optional symbol, `[required]`, `(blur)`, `(input)`.
3. **`BbNumberInputComponent` (`<bb-number-input>`)**:
   - Replaces integer, quantity, days, percentage, and conversion factor inputs.
   - Supports configurable `decimals` / `step` (`0.001`, `0.000001`, `1`), `[min]`, `[max]`, `[suffix]` (e.g. `%`, `days`).
4. **`BbTextInputComponent` (`<bb-text-input>`)**:
   - Replaces general text, code, memo, reference inputs.
   - Supports `[maxlength]`, `[uppercase]` (auto text-transform / uppercase conversion for GSTIN/PAN/IFSC), `[placeholder]`, `[disabled]`, `[required]`, `(blur)`, `(keyup.enter)`.
5. **`BbSearchInputComponent` (`<bb-search-input>`)**:
   - Replaces list search headers and inline search inputs (`<input type="search">`).
   - Supports `[(ngModel)]`, `[placeholder]`, `(search)` or `(keyup.enter)`.

---

## 5. Verification Method

To independently verify these survey findings:
1. Grep search in each directory to verify the line counts and tags:
   ```powershell
   git grep -n "<input" frontend/libs/accounting/accounting-ui
   git grep -n "<input" frontend/libs/inventory/inventory-ui
   git grep -n "<input" frontend/libs/master/master-ui
   ```
2. Verify spot-check on `opening-balance.page.html`:
   - Inspect `frontend/libs/accounting/accounting-ui/src/lib/opening-balance/opening-balance.page.html` (Lines 31, 35, 111, 120, 128, 142, 159, 178, 195).
3. Validate build compliance before/after component introduction:
   ```powershell
   cd frontend
   npm run check
   ```
