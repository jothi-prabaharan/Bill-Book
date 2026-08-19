import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';
import angular from 'angular-eslint';

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
    ignores: ['**/dist/**', '**/node_modules/**', '**/.angular/**', '**/.nx/**'],
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
    rules: {
      '@angular-eslint/template/label-has-associated-control': [
        'error',
        {
          controlComponents: [
            'bb-date-input',
            'bb-number-input',
            'bb-search-input',
            'bb-text-input',
            'bb-currency-input'
          ]
        }
      ],},
  },
);
