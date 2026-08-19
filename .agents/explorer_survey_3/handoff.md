# Survey and Analysis Report: Primitive Input Usages in Purchase UI, Sales UI, Apps, and Frontend Verification Setup

## 1. Observation

### Scope of Investigation
- Target libraries examined:
  - `C:\Users\Praba\Source\repos\Bill-Book\frontend\libs\purchase\purchase-ui`
  - `C:\Users\Praba\Source\repos\Bill-Book\frontend\libs\sales\sales-ui`
  - `C:\Users\Praba\Source\repos\Bill-Book\frontend\apps` (`admin`, `desktop`, `docs`, `portal`, `web`)
- Frontend verification and build configuration:
  - `frontend/package.json`
  - `frontend/nx.json`
  - `frontend/eslint.config.mjs`
  - `frontend/tsconfig.base.json`
  - `frontend/tsconfig.eslint.json`
  - `frontend/vitest.config.mts`
  - `frontend/vitest.setup.ts`

---

### Detailed Catalog of Primitive `<input>` Elements

#### A. Purchase UI (`frontend/libs/purchase/purchase-ui`)
Total `<input>` elements: **23** across 4 form pages (`purchase-list.page.html` contains 0 inputs). All forms in `purchase-ui` utilize Angular Signals (`signal<T>`) bound with template-driven `[ngModel]` and `(ngModelChange)`.

1. **`src/lib/bill-form/bill-form.page.html`** (6 inputs)
   - **Line 58–66**:
     - *Type*: `text`
     - *Purpose*: Vendor's bill number (`vendorBillNo`)
     - *Binding*: `[ngModel]="vendorBillNo()"` `(ngModelChange)="vendorBillNo.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="50"`, `[disabled]="readonlyDoc()"`, `name="vendorBillNo"`
   - **Line 76–83**:
     - *Type*: `date`
     - *Purpose*: Vendor bill date (`vendorBillDate`)
     - *Binding*: `[ngModel]="vendorBillDate()"` `(ngModelChange)="vendorBillDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="vendorBillDate"`
   - **Line 89–96**:
     - *Type*: `date`
     - *Purpose*: Document date / received date (`documentDate`)
     - *Binding*: `[ngModel]="documentDate()"` `(ngModelChange)="documentDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="documentDate"`
   - **Line 101–108**:
     - *Type*: `date`
     - *Purpose*: Payment due date (`dueDate`)
     - *Binding*: `[ngModel]="dueDate()"` `(ngModelChange)="dueDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="dueDate"`
   - **Line 114–122**:
     - *Type*: `text`
     - *Purpose*: Vendor GSTIN (`contactGstin`)
     - *Binding*: `[ngModel]="contactGstin()"` `(ngModelChange)="contactGstin.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="15"`, `[disabled]="readonlyDoc()"`, `name="contactGstin"`
   - **Line 127–136**:
     - *Type*: `text`
     - *Purpose*: Place of supply state code (`placeOfSupplyStateCode`)
     - *Binding*: `[ngModel]="placeOfSupplyStateCode()"` `(ngModelChange)="placeOfSupplyStateCode.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="2"`, `[disabled]="readonlyDoc()"`, `name="placeOfSupplyStateCode"`, `placeholder="2-digit state code"`

2. **`src/lib/debit-note-form/debit-note-form.page.html`** (3 inputs)
   - **Line 76–83**:
     - *Type*: `date`
     - *Purpose*: Debit note document date (`documentDate`)
     - *Binding*: `[ngModel]="documentDate()"` `(ngModelChange)="documentDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="documentDate"`
   - **Line 104–113**:
     - *Type*: `text`
     - *Purpose*: Place of supply state code (`placeOfSupplyStateCode`)
     - *Binding*: `[ngModel]="placeOfSupplyStateCode()"` `(ngModelChange)="placeOfSupplyStateCode.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="2"`, `[disabled]="readonlyDoc()"`, `name="placeOfSupplyStateCode"`, `placeholder="2-digit state code"`
   - **Line 159–168**:
     - *Type*: `number`
     - *Purpose*: Return / credited quantity in lines data-grid template (`row.quantity`)
     - *Binding*: `[ngModel]="row.quantity"` `(ngModelChange)="onQuantityChange(index, $event)"`
     - *Attributes & Styling*: `class="input input--qty"`, `min="0"`, `[max]="row.available"`, `[disabled]="readonlyDoc()"`, `[name]="'qty' + index"`

3. **`src/lib/goods-receipt-form/goods-receipt-form.page.html`** (9 inputs)
   - **Line 75–82**:
     - *Type*: `date`
     - *Purpose*: Goods receipt date (`documentDate`)
     - *Binding*: `[ngModel]="documentDate()"` `(ngModelChange)="documentDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="documentDate"`
   - **Line 87–95**:
     - *Type*: `text`
     - *Purpose*: Vendor delivery note / docket number (`vendorDeliveryNoteNo`)
     - *Binding*: `[ngModel]="vendorDeliveryNoteNo()"` `(ngModelChange)="vendorDeliveryNoteNo.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="50"`, `[disabled]="readonlyDoc()"`, `name="vendorDeliveryNoteNo"`
   - **Line 101–108**:
     - *Type*: `date`
     - *Purpose*: Vendor delivery note date (`vendorDeliveryNoteDate`)
     - *Binding*: `[ngModel]="vendorDeliveryNoteDate()"` `(ngModelChange)="vendorDeliveryNoteDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="vendorDeliveryNoteDate"`
   - **Line 113–121**:
     - *Type*: `text`
     - *Purpose*: Vendor GSTIN (`contactGstin`)
     - *Binding*: `[ngModel]="contactGstin()"` `(ngModelChange)="contactGstin.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="15"`, `[disabled]="readonlyDoc()"`, `name="contactGstin"`
   - **Line 126–135**:
     - *Type*: `text`
     - *Purpose*: Place of supply state code (`placeOfSupplyStateCode`)
     - *Binding*: `[ngModel]="placeOfSupplyStateCode()"` `(ngModelChange)="placeOfSupplyStateCode.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="2"`, `[disabled]="readonlyDoc()"`, `name="placeOfSupplyStateCode"`, `placeholder="2-digit state code"`
   - **Line 174–183**:
     - *Type*: `number`
     - *Purpose*: Rejected quantity in receiving grid template (`row.rejectedQuantity`)
     - *Binding*: `[ngModel]="row.rejectedQuantity"` `(ngModelChange)="onRejectedChange(index, $event)"`
     - *Attributes & Styling*: `class="input input--qty"`, `min="0"`, `[max]="deliveredOf(index)"`, `[disabled]="readonlyDoc()"`, `[name]="'rejected' + index"`
   - **Line 189–198**:
     - *Type*: `text`
     - *Purpose*: Rejection reason in receiving grid template (`row.rejectionReason`)
     - *Binding*: `[ngModel]="row.rejectionReason"` `(ngModelChange)="onReceivingField(index, 'rejectionReason', $event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="300"`, `placeholder="Required when rejecting"`, `[disabled]="readonlyDoc() || row.rejectedQuantity === 0"`, `[name]="'reason' + index"`
   - **Line 201–209**:
     - *Type*: `text`
     - *Purpose*: Batch / lot number in receiving grid template (`row.batchNumber`)
     - *Binding*: `[ngModel]="row.batchNumber"` `(ngModelChange)="onReceivingField(index, 'batchNumber', $event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="50"`, `placeholder="Lot"`, `[disabled]="readonlyDoc()"`, `[name]="'batch' + index"`
   - **Line 213–220**:
     - *Type*: `date`
     - *Purpose*: Batch expiry date in receiving grid template (`row.batchExpiryDate`)
     - *Binding*: `[ngModel]="row.batchExpiryDate"` `(ngModelChange)="onReceivingField(index, 'batchExpiryDate', $event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `[name]="'expiry' + index"`

4. **`src/lib/purchase-order-form/purchase-order-form.page.html`** (5 inputs)
   - **Line 63–71**:
     - *Type*: `text`
     - *Purpose*: Vendor GSTIN (`contactGstin`)
     - *Binding*: `[ngModel]="contactGstin()"` `(ngModelChange)="contactGstin.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="15"`, `[disabled]="readonlyDoc()"`, `name="contactGstin"`
   - **Line 76–83**:
     - *Type*: `date`
     - *Purpose*: Purchase order date (`documentDate`)
     - *Binding*: `[ngModel]="documentDate()"` `(ngModelChange)="documentDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="documentDate"`
   - **Line 88–95**:
     - *Type*: `date`
     - *Purpose*: Expected delivery date (`expectedDate`)
     - *Binding*: `[ngModel]="expectedDate()"` `(ngModelChange)="expectedDate.set($event)"`
     - *Attributes & Styling*: `class="input"`, `[disabled]="readonlyDoc()"`, `name="expectedDate"`
   - **Line 101–109**:
     - *Type*: `text`
     - *Purpose*: Place of supply state code (`placeOfSupplyStateCode`)
     - *Binding*: `[ngModel]="placeOfSupplyStateCode()"` `(ngModelChange)="placeOfSupplyStateCode.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="2"`, `[disabled]="readonlyDoc()"`, `name="placeOfSupplyStateCode"`, `placeholder="2-digit state code"`
   - **Line 247–254**:
     - *Type*: `text`
     - *Purpose*: Void reason for order cancellation modal (`voidReason`)
     - *Binding*: `[ngModel]="voidReason()"` `(ngModelChange)="voidReason.set($event)"`
     - *Attributes & Styling*: `class="input"`, `maxlength="300"`, `name="voidReason"`

---

#### B. Sales UI (`frontend/libs/sales/sales-ui`)
Total `<input>` elements: **30** across 5 form components (`sales-list.component.html` contains 0 inputs). All forms in `sales-ui` utilize Reactive Forms (`[formGroup]` and `formControlName`).

1. **`src/lib/credit-note-form/credit-note-form.component.html`** (5 inputs)
   - **Line 10**:
     - *Type*: `date`
     - *Purpose*: Document date (`documentDate`)
     - *Binding*: `formControlName="documentDate"`
     - *Attributes & Styling*: `id="docDate"`, `type="date"`, `class="input"`
   - **Line 14**:
     - *Type*: `text`
     - *Purpose*: Invoice ID reference (`invoiceId`)
     - *Binding*: `formControlName="invoiceId"`
     - *Attributes & Styling*: `id="invoiceId"`, `type="text"`, `class="input"`
   - **Line 18**:
     - *Type*: `number`
     - *Purpose*: Contact ID (`contactId`)
     - *Binding*: `formControlName="contactId"`
     - *Attributes & Styling*: `id="contactId"`, `type="number"`, `class="input"`
   - **Line 34**:
     - *Type*: default (`text`)
     - *Purpose*: Currency Code (`currencyCode`)
     - *Binding*: `formControlName="currencyCode"`
     - *Attributes & Styling*: `id="currencyCode"`, `maxlength="3"` (no explicit type or class)
   - **Line 38**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Exchange Rate (`exchangeRate`)
     - *Binding*: `formControlName="exchangeRate"`
     - *Attributes & Styling*: `id="exchangeRate"`, `step="0.01"` (no explicit type or class)

2. **`src/lib/delivery-challan-form/delivery-challan-form.component.html`** (6 inputs)
   - **Line 10**:
     - *Type*: `date`
     - *Purpose*: Document Date (`documentDate`)
     - *Binding*: `formControlName="documentDate"`
     - *Attributes & Styling*: `id="docDate"`, `type="date"`, `class="input"`
   - **Line 14**:
     - *Type*: `number`
     - *Purpose*: Contact ID (`contactId`)
     - *Binding*: `formControlName="contactId"`
     - *Attributes & Styling*: `id="contactId"`, `type="number"`, `class="input"`
   - **Line 26**:
     - *Type*: default (`text` / ISO date string)
     - *Purpose*: Dispatch Date (`dispatchDate`)
     - *Binding*: `formControlName="dispatchDate"`
     - *Attributes & Styling*: `id="dispatchDate"` (no explicit type or class)
   - **Line 30**:
     - *Type*: default (`text`)
     - *Purpose*: Vehicle Number (`vehicleNo`)
     - *Binding*: `formControlName="vehicleNo"`
     - *Attributes & Styling*: `id="vehicleNo"` (no explicit type or class)
   - **Line 34**:
     - *Type*: default (`text`)
     - *Purpose*: Currency Code (`currencyCode`)
     - *Binding*: `formControlName="currencyCode"`
     - *Attributes & Styling*: `id="currencyCode"`, `maxlength="3"` (no explicit type or class)
   - **Line 38**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Exchange Rate (`exchangeRate`)
     - *Binding*: `formControlName="exchangeRate"`
     - *Attributes & Styling*: `id="exchangeRate"`, `step="0.01"` (no explicit type or class)

3. **`src/lib/invoice-form/invoice-form.component.html`** (5 inputs)
   - **Line 23**:
     - *Type*: `date`
     - *Purpose*: Document Date (`documentDate`)
     - *Binding*: `formControlName="documentDate"`
     - *Attributes & Styling*: `id="docDate"`, `type="date"`, `class="input"`
   - **Line 27**:
     - *Type*: `date`
     - *Purpose*: Due Date (`dueDate`)
     - *Binding*: `formControlName="dueDate"`
     - *Attributes & Styling*: `id="dueDate"`, `type="date"`, `class="input"`
   - **Line 31**:
     - *Type*: `number`
     - *Purpose*: Contact ID (`contactId`)
     - *Binding*: `formControlName="contactId"`
     - *Attributes & Styling*: `id="contactId"`, `type="number"`, `class="input"`
   - **Line 35**:
     - *Type*: default (`text`)
     - *Purpose*: Currency Code (`currencyCode`)
     - *Binding*: `formControlName="currencyCode"`
     - *Attributes & Styling*: `id="currencyCode"`, `maxlength="3"` (no explicit type or class)
   - **Line 39**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Exchange Rate (`exchangeRate`)
     - *Binding*: `formControlName="exchangeRate"`
     - *Attributes & Styling*: `id="exchangeRate"`, `step="0.01"` (no explicit type or class)

4. **`src/lib/quote-form/quote-form.component.html`** (7 inputs)
   - **Line 10**:
     - *Type*: default (`text` / ISO date string)
     - *Purpose*: Document Date (`documentDate`)
     - *Binding*: `formControlName="documentDate"`
     - *Attributes & Styling*: `id="documentDate"` (no explicit type or class)
   - **Line 14**:
     - *Type*: default (`text` / ISO date string)
     - *Purpose*: Valid Until Date (`validUntil`)
     - *Binding*: `formControlName="validUntil"`
     - *Attributes & Styling*: `id="validUntil"` (no explicit type or class)
   - **Line 18**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Contact ID (`contactId`)
     - *Binding*: `formControlName="contactId"`
     - *Attributes & Styling*: `id="contactId"` (no explicit type or class)
   - **Line 22**:
     - *Type*: default (`text`)
     - *Purpose*: Contact GSTIN (`contactGstin`)
     - *Binding*: `formControlName="contactGstin"`
     - *Attributes & Styling*: `id="contactGstin"`, `maxlength="15"` (no explicit type or class)
   - **Line 26**:
     - *Type*: default (`text`)
     - *Purpose*: Place of Supply State Code (`placeOfSupplyStateCode`)
     - *Binding*: `formControlName="placeOfSupplyStateCode"`
     - *Attributes & Styling*: `id="placeOfSupplyStateCode"`, `maxlength="2"` (no explicit type or class)
   - **Line 30**:
     - *Type*: default (`text`)
     - *Purpose*: Currency Code (`currencyCode`)
     - *Binding*: `formControlName="currencyCode"`
     - *Attributes & Styling*: `id="currencyCode"`, `maxlength="3"` (no explicit type or class)
   - **Line 34**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Exchange Rate (`exchangeRate`)
     - *Binding*: `formControlName="exchangeRate"`
     - *Attributes & Styling*: `id="exchangeRate"`, `step="0.01"` (no explicit type or class)

5. **`src/lib/sales-order-form/sales-order-form.component.html`** (7 inputs)
   - **Line 10**:
     - *Type*: default (`text` / ISO date string)
     - *Purpose*: Document Date (`documentDate`)
     - *Binding*: `formControlName="documentDate"`
     - *Attributes & Styling*: `id="documentDate"` (no explicit type or class)
   - **Line 14**:
     - *Type*: default (`text` / ISO date string)
     - *Purpose*: Delivery Date (`deliveryDate`)
     - *Binding*: `formControlName="deliveryDate"`
     - *Attributes & Styling*: `id="deliveryDate"` (no explicit type or class)
   - **Line 18**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Contact ID (`contactId`)
     - *Binding*: `formControlName="contactId"`
     - *Attributes & Styling*: `id="contactId"` (no explicit type or class)
   - **Line 22**:
     - *Type*: default (`text`)
     - *Purpose*: Contact GSTIN (`contactGstin`)
     - *Binding*: `formControlName="contactGstin"`
     - *Attributes & Styling*: `id="contactGstin"`, `maxlength="15"` (no explicit type or class)
   - **Line 26**:
     - *Type*: default (`text`)
     - *Purpose*: Place of Supply State Code (`placeOfSupplyStateCode`)
     - *Binding*: `formControlName="placeOfSupplyStateCode"`
     - *Attributes & Styling*: `id="placeOfSupplyStateCode"`, `maxlength="2"` (no explicit type or class)
   - **Line 30**:
     - *Type*: default (`text`)
     - *Purpose*: Currency Code (`currencyCode`)
     - *Binding*: `formControlName="currencyCode"`
     - *Attributes & Styling*: `id="currencyCode"`, `maxlength="3"` (no explicit type or class)
   - **Line 34**:
     - *Type*: default (`text`/numeric)
     - *Purpose*: Exchange Rate (`exchangeRate`)
     - *Binding*: `formControlName="exchangeRate"`
     - *Attributes & Styling*: `id="exchangeRate"`, `step="0.01"` (no explicit type or class)

---

#### C. Frontend Apps (`frontend/apps`)
All applications were inspected:
- `apps/admin`: 0 inputs (stub project, src only contains `.gitkeep`)
- `apps/portal`: 0 inputs (stub project, src only contains `.gitkeep`)
- `apps/desktop`: 0 inputs (POS terminal template only has buttons, cart container, and router-outlet)
- `apps/docs`: 0 inputs (doc viewer rendering sidebar navigation and markdown articles)
- `apps/web`: 0 inputs (dashboard page with static KPI cards, SVG charts, and read-only tables)

Total inputs in `apps`: **0**

---

### Summary Table of Surveyed Inputs

| Package / App | Component / Page | Date Inputs | Number / Qty Inputs | Text / GSTIN / Currency Inputs | Total Inputs | Form Paradigm |
|---|---|---|---|---|---|---|
| `purchase-ui` | `bill-form.page` | 3 | 0 | 3 | 6 | Signal + `[ngModel]` |
| `purchase-ui` | `debit-note-form.page` | 1 | 1 | 1 | 3 | Signal + `[ngModel]` |
| `purchase-ui` | `goods-receipt-form.page` | 3 | 1 | 5 | 9 | Signal + `[ngModel]` |
| `purchase-ui` | `purchase-order-form.page` | 2 | 0 | 3 | 5 | Signal + `[ngModel]` |
| `purchase-ui` | `purchase-list.page` | 0 | 0 | 0 | 0 | Navigation / Grid |
| `sales-ui` | `credit-note-form.component` | 1 | 1 | 3 | 5 | Reactive Forms (`formControlName`) |
| `sales-ui` | `delivery-challan-form.component` | 2 | 1 | 3 | 6 | Reactive Forms (`formControlName`) |
| `sales-ui` | `invoice-form.component` | 2 | 1 | 2 | 5 | Reactive Forms (`formControlName`) |
| `sales-ui` | `quote-form.component` | 2 | 1 | 4 | 7 | Reactive Forms (`formControlName`) |
| `sales-ui` | `sales-order-form.component` | 2 | 1 | 4 | 7 | Reactive Forms (`formControlName`) |
| `sales-ui` | `sales-list.component` | 0 | 0 | 0 | 0 | Navigation / Grid |
| `apps/admin` | (stub) | 0 | 0 | 0 | 0 | - |
| `apps/portal` | (stub) | 0 | 0 | 0 | 0 | - |
| `apps/desktop` | `pos-terminal.component` | 0 | 0 | 0 | 0 | Desktop App |
| `apps/docs` | `doc-viewer.component` | 0 | 0 | 0 | 0 | Docs App |
| `apps/web` | `dashboard.page` | 0 | 0 | 0 | 0 | Web App Shell |
| **TOTAL** | **All 16 Scanned Targets** | **16** | **6** | **31** | **53** | **Dual (Signals + Reactive Forms)** |

---

### Verification and Build Setup

1. **`npm run check` Structure** (`frontend/package.json` line 13):
   ```json
   "check": "npm run lint && npm run typecheck && npm run test && npm run build"
   ```
   - **`lint`**: `nx run-many -t lint`
     - Uses `@nx/eslint/plugin` driven by `eslint.config.mjs` and `tsconfig.eslint.json`.
     - Validates component selector prefixes (`bb-*`), `@typescript-eslint/no-unused-vars`, floating promises (`@typescript-eslint/no-floating-promises`), and Angular template accessibility.
   - **`typecheck`**: `tsc --noEmit -p tsconfig.eslint.json`
     - Uses unified `tsconfig.eslint.json` covering all `apps/**/*.ts`, `libs/**/*.ts`, and `*.mjs` with `strict: true`.
     - Ensures spec files and non-app files are fully typechecked.
   - **`test`**: `vitest run`
     - Uses `vitest.config.mts` in `jsdom` environment with `vitest.setup.ts`.
     - Fast execution without spinning up full Angular browser harnesses.
   - **`build`**: `nx run-many -t build`
     - Builds all applications (`web`, `desktop`, `docs`, `admin`, `portal`) and their dependent libraries with Angular build optimization.

---

## 2. Logic Chain

1. **Requirement R1 & R2 Context**: The project objective is to identify all primitive input usages across the frontend and replace raw HTML `<input>` tags with reusable components in `@bill-book/ui-components`.
2. **Dual Form Paradigms Observed**:
   - `purchase-ui` uses **Signal-based state** with template-driven `[ngModel]="sig()"` and `(ngModelChange)="sig.set($event)"`.
   - `sales-ui` uses **Reactive Forms** with `[formGroup]="form"` and `formControlName="..."`.
3. **Implication for Component Design**:
   - Every reusable input component created in `@bill-book/ui-components` (e.g. `bb-date-input`, `bb-number-input`, `bb-currency-input`, `bb-text-input`) **MUST implement `ControlValueAccessor`** (providing `NG_VALUE_ACCESSOR` and `forwardRef`) and `FormsModule` / `ReactiveFormsModule` compatibility so it seamlessly binds with BOTH `[ngModel]`/`(ngModelChange)` and `formControlName`.
   - It must handle `disabled` states both via `setDisabledState` (Reactive Forms) and `@Input() disabled: boolean` / Signal inputs.
4. **Style and Attribute Consistency**:
   - In `purchase-ui`, inputs have standard styling `class="input"` or `class="input input--qty"`.
   - In `sales-ui` (e.g., `quote-form`, `sales-order-form`, `delivery-challan-form`), several date and exchange rate inputs lack `type="date"` or `class="input"`. Refactoring to unified `@bill-book/ui-components` will directly eliminate this visual and functional inconsistency across the ERP.
5. **No Inputs in Apps**:
   - `frontend/apps` contains zero raw input elements because user interactions in `web` route to library feature modules (`purchase-ui`, `sales-ui`, `accounting-ui`, `inventory-ui`, `master-ui`), while `desktop` and `docs` are specialized shells.

---

## 3. Caveats

- `libs/accounting/accounting-ui`, `libs/inventory/inventory-ui`, and `libs/master/master-ui` were surveyed by peer explorer agents and are outside this specific survey assignment, though they share the same `@bill-book/ui-components` library.
- Some inputs in `sales-ui` (like `quote-form.component.html` lines 10 & 14) store ISO dates (`YYYY-MM-DD`) in form controls without specifying `type="date"` on the HTML input. Replacing them with a unified `bb-date-input` will enhance consistency and date validation.
- The `delivery-challan-form` currently exists in `sales-ui` source files but is not routed in `sales.routes.ts`. However, its input tags were fully cataloged so that any future routing or usage remains 100% compliant with the new component standard.

---

## 4. Conclusion

1. Exactly **53 raw `<input>` elements** exist across `purchase-ui` (23) and `sales-ui` (30). No raw inputs exist in `frontend/apps`.
2. The inputs fall into four primary categories:
   - **Date inputs** (16 occurrences): Document dates, due dates, receipt dates, expiry dates, dispatch/delivery dates.
   - **Number / Quantity inputs** (6 occurrences): Rejection quantities, return quantities, contact IDs, exchange rates.
   - **Text / Code inputs** (31 occurrences): Vendor bill numbers, GSTIN numbers (15-char), state place-of-supply codes (2-char), currency codes (3-char), delivery notes, batch numbers, void/rejection reasons.
3. Universal `ControlValueAccessor` support is mandatory for all new global UI components in `@bill-book/ui-components` to support both `purchase-ui`'s Signal-based `ngModel` bindings and `sales-ui`'s Reactive Forms `formControlName` bindings without requiring architectural rewrites in either domain.

---

## 5. Verification Method

To verify these observations and validate changes downstream:

1. **Verify Input Locations**:
   - Search for inputs in `purchase-ui`:
     ```powershell
     git grep -n "<input" -- frontend/libs/purchase/purchase-ui
     ```
   - Search for inputs in `sales-ui`:
     ```powershell
     git grep -n "<input" -- frontend/libs/sales/sales-ui
     ```
   - Confirm zero inputs in `frontend/apps`:
     ```powershell
     git grep -n "<input" -- frontend/apps
     ```
2. **Frontend Quality & Build Verification**:
   ```powershell
   cd frontend
   npm run check
   ```
   This executes all four validation phases (`lint`, `typecheck`, `test`, `build`) in sequence.
