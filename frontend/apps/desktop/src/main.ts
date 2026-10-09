import { BrowserWindow, app, ipcMain, powerMonitor, session, shell } from 'electron';
import { readFileSync, writeFileSync } from 'node:fs';
import * as path from 'node:path';
import { IPC } from './ipc-channels';
import { APP_ORIGIN } from './lib/app-protocol';
import { registerAppScheme, serveApp } from './lib/app-server';
import { parseConfigLayer, resolveDesktopConfig } from './lib/desktop-config';
import { DesktopSettings, parseSettings } from './lib/desktop-settings';
import { createMainWindow } from './lib/main-window';
import { buildMenu, listPrinters } from './lib/menu';
import { printPage } from './lib/printing';
import { assertTrustedSender, isTrustedUrl } from './lib/trust';
import { Updater, setupUpdater } from './lib/updater';

// The Bill Book desktop app (TK-184): a window around the bundled web build,
// with silent printing and self-updating. Everything the page may ask of the
// computer goes through preload.ts.

registerAppScheme();

const readText = (file: string): string | null => {
  try {
    return readFileSync(file, 'utf8');
  } catch {
    return null;
  }
};

const userFile = (name: string): string => path.join(app.getPath('userData'), name);

const config = resolveDesktopConfig(
  parseConfigLayer(readText(path.join(__dirname, 'desktop-config.json'))),
  parseConfigLayer(readText(userFile('desktop-config.json'))),
  process.env,
);

let mainWindow: BrowserWindow | null = null;
let updater: Updater | null = null;
let settings: DesktopSettings = parseSettings(readText(userFile('desktop-settings.json')));

const PERMISSIONS = new Set(['media', 'clipboard-sanitized-write', 'clipboard-read', 'fullscreen', 'notifications']);

function updateSettings(change: Partial<DesktopSettings>): void {
  settings = { ...settings, ...change };

  try {
    writeFileSync(userFile('desktop-settings.json'), JSON.stringify(settings, null, 2));
  } catch {
    // Kept for this session only.
  }

  void refreshMenu();
}

async function refreshMenu(): Promise<void> {
  buildMenu(await listPrinters(mainWindow), {
    settings: () => settings,
    update: updateSettings,
    refreshPrinters: () => void refreshMenu(),
    checkForUpdates: () => void updater?.checkFromMenu(),
  });
}

function openMainWindow(): void {
  mainWindow = createMainWindow(path.join(__dirname, 'preload.js'), config.devUrl ?? `${APP_ORIGIN}/`);
  mainWindow.on('closed', () => {
    mainWindow = null;
  });
  mainWindow.webContents.once('did-finish-load', () => void refreshMenu());
}

function lockDown(): void {
  // The camera for scanning, the clipboard and notifications, for the app's own pages only.
  session.defaultSession.setPermissionRequestHandler((_contents, permission, callback, details) => {
    callback(PERMISSIONS.has(permission) && isTrustedUrl(details.requestingUrl, config.devUrl));
  });
  session.defaultSession.setPermissionCheckHandler((_contents, permission, origin) => {
    return PERMISSIONS.has(permission) && isTrustedUrl(origin, config.devUrl);
  });

  app.on('web-contents-created', (_event, contents) => {
    contents.on('will-attach-webview', (event) => event.preventDefault());

    contents.on('will-navigate', (event, url) => {
      if (!isTrustedUrl(url, config.devUrl)) {
        event.preventDefault();
        openOutside(url);
      }
    });

    // A print preview or a report opened in a new window stays in the app, on
    // the same monitor as the window that opened it; any other link goes to
    // the browser.
    contents.setWindowOpenHandler(({ url }) => {
      const blob = url.startsWith('blob:') && isTrustedUrl(url.slice(5), config.devUrl);

      if (isTrustedUrl(url, config.devUrl) || blob || url === 'about:blank') {
        const parent = BrowserWindow.fromWebContents(contents)?.getBounds();
        return {
          action: 'allow',
          overrideBrowserWindowOptions: {
            autoHideMenuBar: true,
            ...(parent ? { x: parent.x + 40, y: parent.y + 40 } : {}),
            webPreferences: { contextIsolation: true, sandbox: true, nodeIntegration: false },
          },
        };
      }

      openOutside(url);
      return { action: 'deny' };
    });
  });
}

function openOutside(url: string): void {
  if (/^(https?|mailto|tel):/i.test(url)) {
    void shell.openExternal(url);
  }
}

function registerIpc(): void {
  ipcMain.handle(IPC.info, (event) => {
    assertTrustedSender(event, config.devUrl);
    return { version: app.getVersion(), os: process.platform, updateManifestUrl: config.updateManifestUrl };
  });

  ipcMain.handle(IPC.print, async (event, html: unknown, kind: unknown) => {
    assertTrustedSender(event, config.devUrl);

    if (typeof html !== 'string' || html.length === 0) {
      throw new Error('There is nothing to print.');
    }

    await printPage(html, typeof kind === 'string' ? kind : 'document', settings);
  });
}

if (!app.requestSingleInstanceLock()) {
  // A till opened twice would print every receipt twice; the first copy takes focus instead.
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) {
        mainWindow.restore();
      }
      mainWindow.focus();
    }
  });

  app
    .whenReady()
    .then(() => {
      serveApp(path.join(__dirname, 'web'), () => config.serverUrl);
      lockDown();
      registerIpc();
      updater = setupUpdater(config, () => mainWindow);
      powerMonitor.on('resume', () => mainWindow?.webContents.send(IPC.resume));
      openMainWindow();

      app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
          openMainWindow();
        }
      });
    })
    .catch((error: unknown) => {
      console.error('Bill Book could not start.', error);
      app.quit();
    });

  app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
      app.quit();
    }
  });
}
