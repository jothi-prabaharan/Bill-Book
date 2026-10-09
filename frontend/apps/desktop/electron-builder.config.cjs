// electron-builder configuration for the desktop app (TK-184).
//
// Packages dist/apps/desktop/app, which scripts/build.mjs stages. Installers
// and the update files (latest.yml, latest-linux.yml, latest-mac.yml) land in
// dist/apps/desktop/release; copy all of them, with version.json, to the
// folder BILLBOOK_UPDATE_URL names.
const path = require('node:path');

const updateUrl = (process.env.BILLBOOK_UPDATE_URL || 'http://YOUR_SERVER_IP:PORT/updates').replace(/\/+$/, '');

module.exports = {
  appId: 'com.billbook.desktop',
  productName: 'Bill Book',
  electronVersion: require(path.join(__dirname, '../../node_modules/electron/package.json')).version,
  directories: {
    app: '../../dist/apps/desktop/app',
    output: '../../dist/apps/desktop/release',
    buildResources: 'resources',
  },
  files: ['**/*'],
  asar: true,
  npmRebuild: false,
  icon: '../web/public/icons/icon-512.png',
  artifactName: 'BillBook-${version}-${os}-${arch}.${ext}',
  publish: [{ provider: 'generic', url: updateUrl }],
  win: {
    target: [{ target: 'nsis', arch: ['x64'] }],
  },
  nsis: {
    oneClick: false,
    perMachine: false,
    allowToChangeInstallationDirectory: true,
    createDesktopShortcut: true,
    artifactName: 'BillBook-Setup-${version}.${ext}',
  },
  linux: {
    target: ['deb', 'AppImage'],
    category: 'Office',
    maintainer: 'Bill Book',
    synopsis: 'Billing, stock and accounts',
    syncDesktopName: true,
  },
  mac: {
    // The zip is what electron-updater installs from; the dmg is for first installs.
    target: ['dmg', 'zip'],
    category: 'public.app-category.business',
    hardenedRuntime: true,
    entitlements: 'resources/entitlements.mac.plist',
    entitlementsInherit: 'resources/entitlements.mac.plist',
    extendInfo: {
      NSCameraUsageDescription: 'Bill Book uses the camera to scan barcodes and QR codes.',
    },
  },
};
