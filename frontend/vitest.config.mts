import { defineConfig } from 'vitest/config';
import { fileURLToPath } from 'node:url';

/**
 * One Vitest config for the workspace, rather than one per library.
 *
 * **Deliberately not the Angular `unit-test` builder.** That builder is marked
 * EXPERIMENTAL by Angular itself and, as configured through Nx, insists on a
 * real browser — @vitest/browser plus Playwright — to run tests that do not
 * need a browser at all. Betting the project's only test infrastructure on an
 * experimental builder and a browser download was the worse trade.
 *
 * What this config can run: services, guards, interceptors and plain functions.
 * Angular compiles `@Injectable` and `inject()` at runtime, so those need no
 * template compiler.
 *
 * What it cannot run: component tests, which need templates compiled. When
 * those are wanted, the Angular Vite plugin is the thing to add — not a second
 * runner alongside this one.
 */
export default defineConfig({
  test: {
    // No `globals`. describe/it/expect are imported in each spec, so the lint
    // config needs no test-only globals and an undefined helper is a compile
    // error rather than a runtime one.
    environment: 'jsdom',
    include: ['{apps,libs}/**/*.spec.ts'],
    setupFiles: ['./vitest.setup.ts'],
    reporters: ['default'],
  },
  esbuild: {
    // Angular's decorators are TypeScript's legacy ones. Without this esbuild
    // strips them and every @Injectable becomes an ordinary class with no
    // provider, failing at inject() with an error that names nothing useful.
    tsconfigRaw: {
      compilerOptions: {
        experimentalDecorators: true,
        useDefineForClassFields: false,
      },
    },
  },
  resolve: {
    alias: {
      '@bill-book/auth': fileURLToPath(
        new URL('./libs/shared/auth/src/index.ts', import.meta.url),
      ),
      '@bill-book/api-client': fileURLToPath(
        new URL('./libs/shared/api-client/src/index.ts', import.meta.url),
      ),
      '@bill-book/ui-components': fileURLToPath(
        new URL('./libs/shared/ui-components/src/index.ts', import.meta.url),
      ),
      '@bill-book/app-shell': fileURLToPath(
        new URL('./libs/app-shell/src/index.ts', import.meta.url),
      ),
      '@bill-book/sales-core': fileURLToPath(
        new URL('./libs/sales/sales-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/sales-ui': fileURLToPath(
        new URL('./libs/sales/sales-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/purchase-core': fileURLToPath(
        new URL('./libs/purchase/purchase-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/purchase-ui': fileURLToPath(
        new URL('./libs/purchase/purchase-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/inventory-core': fileURLToPath(
        new URL('./libs/inventory/inventory-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/inventory-ui': fileURLToPath(
        new URL('./libs/inventory/inventory-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/accounting-core': fileURLToPath(
        new URL('./libs/accounting/accounting-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/accounting-ui': fileURLToPath(
        new URL('./libs/accounting/accounting-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/master-core': fileURLToPath(
        new URL('./libs/master/master-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/master-ui': fileURLToPath(
        new URL('./libs/master/master-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/reporting-core': fileURLToPath(
        new URL('./libs/reporting/reporting-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/reporting-ui': fileURLToPath(
        new URL('./libs/reporting/reporting-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/customer-core': fileURLToPath(
        new URL('./libs/customer/customer-core/src/index.ts', import.meta.url),
      ),
      '@bill-book/customer-ui': fileURLToPath(
        new URL('./libs/customer/customer-ui/src/index.ts', import.meta.url),
      ),
      '@bill-book/currency-format': fileURLToPath(
        new URL('./libs/shared/currency-format/src/index.ts', import.meta.url),
      ),
    },
  },
});
