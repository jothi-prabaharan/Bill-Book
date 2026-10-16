// Builds the desktop app's main process and stages it beside the web build
// (TK-184). electron-builder then packages dist/apps/desktop/app.
//
//   node apps/desktop/scripts/build.mjs              main + preload + web build
//   node apps/desktop/scripts/build.mjs --main-only  main + preload, for `nx run desktop:serve`
//
// BILLBOOK_SERVER_URL and BILLBOOK_UPDATE_URL, when set, are baked into the
// installer's desktop-config.json; an operator can still override them per
// computer. See apps/docs/content/deployment.md, "Desktop and mobile apps".
import { build } from 'esbuild';
import { cp, mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const app = path.join(root, 'dist/apps/desktop/app');
const web = path.join(root, 'dist/apps/web/browser');
const mainOnly = process.argv.includes('--main-only');

await rm(app, { recursive: true, force: true });
await mkdir(app, { recursive: true });

await build({
  absWorkingDir: root,
  entryPoints: { main: 'apps/desktop/src/main.ts', preload: 'apps/desktop/src/preload.ts' },
  outdir: app,
  bundle: true,
  platform: 'node',
  target: 'node22',
  format: 'cjs',
  external: ['electron'],
  tsconfig: 'apps/desktop/tsconfig.json',
  sourcemap: mainOnly ? 'inline' : false,
  minify: !mainOnly,
  legalComments: 'none',
  logLevel: 'warning',
});

const pkg = JSON.parse(await readFile(path.join(root, 'apps/desktop/package.json'), 'utf8'));
const { name, productName, desktopName, version, description, author, homepage, license } = pkg;
await writeFile(
  path.join(app, 'package.json'),
  JSON.stringify({ name, productName, desktopName, version, description, author, homepage, license, main: 'main.js' }, null, 2),
);

const env = (key) => (process.env[key] ?? '').trim() || null;
await writeFile(
  path.join(app, 'desktop-config.json'),
  JSON.stringify({ serverUrl: env('BILLBOOK_SERVER_URL'), updateUrl: env('BILLBOOK_UPDATE_URL') }, null, 2),
);

// Stage the application icons for the window taskbar, title bar and tray
await cp(path.join(root, 'apps/desktop/resources/icon.png'), path.join(app, 'icon.png'));
await cp(path.join(root, 'apps/desktop/resources/icon.ico'), path.join(app, 'icon.ico'));
await cp(path.join(root, 'apps/desktop/resources/bill-book-logo.svg'), path.join(app, 'bill-book-logo.svg'));

if (!mainOnly) {
  try {
    await stat(path.join(web, 'index.html'));
  } catch {
    console.error(`No web build at ${web}. Run: npx nx run web:build:production`);
    process.exit(1);
  }

  await cp(web, path.join(app, 'web'), { recursive: true });
}

console.log(`Desktop ${version} staged in ${path.relative(root, app)}${mainOnly ? ' (main process only)' : ''}.`);
