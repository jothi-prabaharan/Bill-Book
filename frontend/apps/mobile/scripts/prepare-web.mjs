// Copies the web build into apps/mobile/www for Capacitor (TK-184), with a
// config.js the app reads before it starts, and stamps the version from
// apps/mobile/package.json into the iOS project (Android's build.gradle reads
// it itself).
//
//   BILLBOOK_API_URL=https://books.example.com \
//   BILLBOOK_UPDATE_URL=https://updates.example.com/updates \
//   node apps/mobile/scripts/prepare-web.mjs
//
// A phone cannot forward /api the way the dev server and the desktop app do,
// so a device build without BILLBOOK_API_URL cannot sign in; the script warns.
import { cp, readFile, rm, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const mobile = path.join(root, 'apps/mobile');
const web = path.join(root, 'dist/apps/web/browser');
const www = path.join(mobile, 'www');

const env = (key) => (process.env[key] ?? '').trim().replace(/\/+$/, '');

try {
  await stat(path.join(web, 'index.html'));
} catch {
  console.error(`No web build at ${web}. Run: npx nx run web:build:production`);
  process.exit(1);
}

await rm(www, { recursive: true, force: true });
await cp(web, www, { recursive: true });

const apiBaseUrl = env('BILLBOOK_API_URL');
const updates = env('BILLBOOK_UPDATE_URL');
const updateManifestUrl = env('BILLBOOK_UPDATE_MANIFEST_URL') || (updates ? `${updates}/version.json` : '');

if (!apiBaseUrl) {
  console.warn('BILLBOOK_API_URL is not set: this build will not reach a server.');
}

if (!updateManifestUrl) {
  console.warn('BILLBOOK_UPDATE_URL is not set: this build will never check for updates.');
}

const settings = { ...(apiBaseUrl ? { apiBaseUrl } : {}), ...(updateManifestUrl ? { updateManifestUrl } : {}) };
await writeFile(
  path.join(www, 'config.js'),
  `// Written by apps/mobile/scripts/prepare-web.mjs for the Android and iOS apps.\nwindow.__BB_CONFIG__ = ${JSON.stringify(settings, null, 2)};\n`,
);

const { version } = JSON.parse(await readFile(path.join(mobile, 'package.json'), 'utf8'));
const [major = 0, minor = 0, patch = 0] = version.split(/[.-]/).map((part) => Number.parseInt(part, 10) || 0);
const build = major * 10000 + minor * 100 + patch;
const pbxproj = path.join(mobile, 'ios/App/App.xcodeproj/project.pbxproj');

try {
  const project = await readFile(pbxproj, 'utf8');
  await writeFile(
    pbxproj,
    project
      .replace(/MARKETING_VERSION = [^;]+;/g, `MARKETING_VERSION = ${version};`)
      .replace(/CURRENT_PROJECT_VERSION = [^;]+;/g, `CURRENT_PROJECT_VERSION = ${build};`),
  );
} catch {
  // No iOS project yet: `npx cap add ios` creates it.
}

console.log(`Mobile ${version} (build ${build}) web assets staged in apps/mobile/www.`);
