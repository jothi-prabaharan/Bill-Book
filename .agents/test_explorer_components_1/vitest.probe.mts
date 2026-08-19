import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['.agents/test_explorer_components_1/**/*.spec.ts'],
    setupFiles: ['./vitest.setup.ts'],
    reporters: ['default'],
  },
  esbuild: {
    tsconfigRaw: {
      compilerOptions: {
        experimentalDecorators: true,
        useDefineForClassFields: false,
      },
    },
  },
});
