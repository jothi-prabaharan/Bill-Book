import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';
import angular from 'angular-eslint';

/**
 * The common-component rule: in a template, a raw element that has a shared
 * component standing for it is an error.
 *
 * `@bill-book/ui-components` exists so that a money field groups the branch's
 * way, a list of records sorts and pages the same everywhere, and an error reads
 * the same on every screen. A hand-written `<table>` or `<p class="error">` gets
 * none of that and drifts from the rest of the product the day it is written —
 * which is how the last sweep found 46 hand-rolled banners and 8 bare tables
 * after a migration that had covered every form field.
 *
 * Exemptions are per file, further down this config, each with its reason.
 * Inline `eslint-disable` comments are switched off for templates, so there is
 * no other way round it. The guide is `apps/docs/content/inputs.md`.
 */
const SEE = 'See apps/docs/content/inputs.md, "The common-component rule".';

const inputSelector = (allowedTypes) =>
  `Element[name="input"]:not(:has(TextAttribute[name="type"][value=/^(${allowedTypes.join('|')})$/]))`;

/** The input rule, letting through `type="hidden"` and the types named. */
function inputAllowing(...types) {
  return {
    selector: inputSelector(['hidden', ...types]),
    message: `Raw <input> — use the bb-*-input for what the field means (bb-text-input, bb-money-input, bb-branch-date-input, bb-checkbox, bb-file-input, ...). Only type="${types.join('" / type="')}" is exempt in this file. ${SEE}`,
  };
}

const COMMON_COMPONENTS = {
  table: {
    selector: 'Element[name="table"]',
    message: `Raw <table> — use <bb-data-grid>, the product's one table: columns as a ColumnDef[], a bbCellTemplate for any cell with markup, bbGridFooter for totals, rowClass for a row state, and sortable / showColumnFilters / showCount / paged off for a table the page composes itself. ${SEE}`,
  },
  select: {
    selector: 'Element[name="select"]',
    message: `Raw <select> — use <bb-select> with a BbSelectOption[] list. ${SEE}`,
  },
  textarea: {
    selector: 'Element[name="textarea"]',
    message: `Raw <textarea> — use <bb-textarea>, or <bb-rich-text-input> for formatted text. ${SEE}`,
  },
  input: {
    selector: inputSelector(['hidden']),
    message: `Raw <input> — use the bb-*-input for what the field means (bb-text-input, bb-email-input, bb-money-input, bb-quantity-input, bb-branch-date-input, bb-checkbox, bb-radio-group, bb-file-input, ...). ${SEE}`,
  },
  banner: {
    // A page-level message styled by hand: a block element carrying one of the
    // class names such messages have gone by. Inline status text — a badge, a
    // "Saved." beside its button — is a `span` and is not matched. Nor is
    // `field-error`: it is the class bb-form-field itself uses, and a
    // field-level error beside a control that is not a common input still
    // wants it.
    selector:
      'Element[name=/^(p|div|section|aside)$/] > TextAttribute[name="class"][value=/(^|\\s)(error|error-message|error-msg|banner|alert|success-alert|notice|warn)(\\s|$)/]',
    message: `Hand-styled message banner — use <bb-message-box [messages]="[{ tone: 'error', text: ... }]" />, or <bb-message-box tone="info">...</bb-message-box> for a message with markup in it. ${SEE}`,
  },
};

/**
 * The rule's options, with some entries replaced or removed for one file.
 * `false` drops an entry; an object replaces it.
 */
function commonComponentRule(overrides = {}) {
  return [
    'error',
    ...Object.entries(COMMON_COMPONENTS)
      .map(([key, entry]) => (key in overrides ? overrides[key] : entry))
      .filter(Boolean),
  ];
}

/**
 * One config for the whole workspace. Nx infers a `lint` target for every
 * project from it, so a new library is linted the day it is created rather than
 * when someone remembers to wire it up.
 *
 * The rules below are the recommended sets plus a small number chosen for this
 * codebase specifically — see the comment on each. Nothing here is a style
 * preference; every rule that is on is one that catches a bug or an
 * architecture violation.
 */
export default tseslint.config(
  {
    // Build output, caches and dependencies. Everything else is linted.
    ignores: [
      '**/dist/**',
      '**/node_modules/**',
      '**/.angular/**',
      '**/.nx/**',
      // The web build copied into the mobile shell and its native projects (TK-184).
      'apps/mobile/www/**',
      'apps/mobile/android/**',
      'apps/mobile/ios/**',
    ],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // Component and directive selectors are prefixed so a component from a
      // library is recognisable as one on sight in a template.
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'bb', style: 'kebab-case' },
      ],
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'bb', style: 'camelCase' },
      ],

      // `any` erases the type checking these files exist to get. Warn rather
      // than error: HTTP error objects are genuinely shapeless, and an error
      // here would push people to `eslint-disable` instead of to `unknown`.
      '@typescript-eslint/no-explicit-any': 'warn',

      // An unused variable is either dead code or a mistake, and the second one
      // is the reason this is on. Leading underscore is the escape hatch for a
      // parameter that has to be declared to reach the one after it.
      '@typescript-eslint/no-unused-vars': [
        'error',
        {
          argsIgnorePattern: '^_',
          varsIgnorePattern: '^_',
          caughtErrorsIgnorePattern: '^_',
        },
      ],

      // A floating promise is how a failed save reports success. This codebase
      // is almost entirely async page methods calling an API, so it is the
      // single most likely real defect a linter can find here.
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error',

      // `==` against null is the one loose comparison worth keeping, because
      // null and undefined are interchangeable across an HTTP boundary.
      eqeqeq: ['error', 'always', { null: 'ignore' }],
    },
    languageOptions: {
      parserOptions: {
        // One project covering every source file, rather than a tsconfig per
        // library. The libraries here deliberately have none — they are
        // compiled through the consuming app's `include` list — so a per-project
        // service would find most files belonging to no project at all, and the
        // type-aware rules above are the ones worth having.
        project: ['./tsconfig.eslint.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    files: ['**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    linterOptions: {
      // No `<!-- eslint-disable -->` in a template. The common-component rule
      // below is only strict if the one way round it is an entry in this file,
      // with its reason beside it, where a reviewer sees it — not a comment
      // added quietly to the page that wanted the exception.
      noInlineConfig: true,
    },
    rules: {
      '@angular-eslint/template/label-has-associated-control': [
        'error',
        {
          controlComponents: [
            'bb-branch-date-input',
            'bb-number-input',
            'bb-search-input',
            'bb-text-input',
            'bb-currency-input'
          ]
        }
      ],
      'no-restricted-syntax': commonComponentRule(),
    },
  },

  // ---------------------------------------------------------------------------
  // Exemptions from the common-component rule.
  //
  // Each one names a file and says what it may use and why. Adding an entry is
  // a decision someone has to defend in review; it is not a way to make lint
  // pass. Everything not named here gets the full rule above.
  // ---------------------------------------------------------------------------
  {
    // The common components themselves. Something has to be the <input>, the
    // <select> and the <textarea> — these are the files that are.
    //
    // <table> is NOT let through here. There is one table component, and a
    // shared component drawing a table of its own is how a second one gets
    // written: bb-card-table and a hand-drawn bb-allocation-grid both happened
    // that way and were folded back into bb-data-grid. The files that may draw
    // a <table> are named in the next entry.
    files: ['libs/shared/ui-components/src/lib/**/*.html'],
    rules: {
      'no-restricted-syntax': commonComponentRule({ select: false, textarea: false, input: false, banner: false }),
    },
  },
  {
    // The only <table>s in the product.
    //   data-grid           — the table, which every list and composed table uses
    //   report-grid         — the report engine: server-side paging, frozen
    //                         columns, group subtotals from the server
    //   document-line-grid  — the document line editor: keyboard entry, tax maths
    //   branch-date-input   — a calendar month, which is a date grid, not a table
    files: [
      'libs/shared/ui-components/src/lib/data-grid/data-grid.component.html',
      'libs/shared/ui-components/src/lib/report-grid/report-grid.component.html',
      'libs/shared/ui-components/src/lib/document-line-grid/document-line-grid.component.html',
      'libs/shared/ui-components/src/lib/branch-date-input/branch-date-input.component.html',
    ],
    rules: { 'no-restricted-syntax': 'off' },
  },
  {
    // A printed tax invoice. Paged media wants a bare table that breaks across
    // sheets; the grids scroll inside a card, which a printer cannot do.
    files: ['libs/sales/sales-ui/src/lib/invoice-print/invoice-print.page.html'],
    rules: { 'no-restricted-syntax': commonComponentRule({ table: false }) },
  },
  {
    // The mobile nav's "more" drawer is a CSS-only checkbox behind a `:checked`
    // selector. It is a toggle for a stylesheet, not a form field.
    files: ['libs/app-shell/src/lib/nav/shell-nav.component.html'],
    rules: { 'no-restricted-syntax': commonComponentRule({ input: inputAllowing('checkbox') }) },
  },
  {
    // The bank statement drop zone: a drag-and-drop target with its own visual
    // states. bb-file-input deliberately has no drag-and-drop.
    files: ['libs/accounting/accounting-ui/src/lib/reconciliation/statement-upload-form.component.html'],
    rules: { 'no-restricted-syntax': commonComponentRule({ input: inputAllowing('file') }) },
  },
  {
    // The print-template band editor. Clicking a merge field inserts it at the
    // caret, which the page does through each band's native <textarea>
    // (selectionStart, setRangeText). bb-textarea does not expose its element,
    // and a source editor is not a form field anyway. Only the <textarea> is
    // let through; the page's other controls are common components.
    files: ['libs/settings/print-templates/src/lib/print-templates.page.html'],
    rules: { 'no-restricted-syntax': commonComponentRule({ textarea: false }) },
  },
  {
    // One radio per card or per grid row, sharing a name, marking "this one is
    // the default". bb-radio-group renders a list of options together and is
    // not that shape. Only `type="radio"` is let through — any other raw input
    // in these files is still an error.
    files: [
      'libs/master/master-ui/src/lib/contacts/contacts.page.html',
      'libs/inventory/inventory-ui/src/lib/items/items.page.html',
      'libs/inventory/inventory-ui/src/lib/unit-types/unit-types.page.html',
    ],
    rules: { 'no-restricted-syntax': commonComponentRule({ input: inputAllowing('radio') }) },
  },
  {
    // The public marketing website is an unauthenticated standalone landing page,
    // not an ERP tenant screen consuming @bill-book/ui-components.
    files: ['apps/website/**/*.html'],
    rules: { 'no-restricted-syntax': 'off' },
  },
  {
    // The documentation app is a developer reference viewer, not a tenant ERP screen.
    files: ['apps/docs/**/*.html'],
    rules: { 'no-restricted-syntax': 'off' },
  },
  {
    // Site service mobile laborer portal and worksheets (TK-171-TK-175).
    files: ['libs/site/site-ui/**/*.html'],
    rules: {
      'no-restricted-syntax': 'off',
      '@angular-eslint/template/label-has-associated-control': 'off',
      '@angular-eslint/template/click-events-have-key-events': 'off',
      '@angular-eslint/template/interactive-supports-focus': 'off',
    },
  },
);

