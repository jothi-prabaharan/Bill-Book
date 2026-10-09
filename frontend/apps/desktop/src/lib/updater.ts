import { BrowserWindow, app, dialog, ipcMain, net } from 'electron';
import { autoUpdater } from 'electron-updater';
import { IPC } from '../ipc-channels';
import { DesktopConfig } from './desktop-config';
import { assertTrustedSender } from './trust';

/**
 * Downloads and installs a new desktop build from the private update server
 * (TK-184). Whether to update, and whether the user may refuse, is the web
 * app's decision from `version.json`; this only fetches and installs, through
 * electron-updater's generic provider (`latest.yml` beside the installers).
 */
export interface Updater {
  /** Help › Check for updates: shows the update prompt, or says there is nothing newer. */
  checkFromMenu(): Promise<void>;
}

export function setupUpdater(config: DesktopConfig, mainWindow: () => BrowserWindow | null): Updater {
  autoUpdater.autoDownload = false;
  autoUpdater.autoInstallOnAppQuit = true;
  autoUpdater.allowDowngrade = false;

  if (config.updateUrl) {
    autoUpdater.setFeedURL({ provider: 'generic', url: config.updateUrl });
  }

  autoUpdater.on('download-progress', (progress) => {
    mainWindow()?.webContents.send(IPC.updateProgress, progress.percent);
  });

  // version.json, read here rather than by the page: a file server sends no CORS headers.
  ipcMain.handle(IPC.manifest, async (event) => {
    assertTrustedSender(event, config.devUrl);

    if (!config.updateManifestUrl) {
      return null;
    }

    const url = new URL(config.updateManifestUrl);
    url.searchParams.set('t', String(Date.now()));
    const response = await net.fetch(url.toString(), { cache: 'no-store' });

    if (!response.ok) {
      throw new Error(`The update server answered ${response.status}.`);
    }

    return (await response.json()) as unknown;
  });

  ipcMain.handle(IPC.updateDownload, async (event) => {
    assertTrustedSender(event, config.devUrl);

    if (!app.isPackaged) {
      throw new Error('A development build cannot update itself. Install a packaged build to test updates.');
    }

    if (!config.updateUrl) {
      throw new Error('No update server is set for this desktop app. Set updateUrl in desktop-config.json.');
    }

    const result = await autoUpdater.checkForUpdates();

    if (!result?.isUpdateAvailable) {
      return { available: false, version: result?.updateInfo.version ?? null };
    }

    await autoUpdater.downloadUpdate();
    return { available: true, version: result.updateInfo.version };
  });

  ipcMain.handle(IPC.updateInstall, (event) => {
    assertTrustedSender(event, config.devUrl);
    // After the reply has gone back, so the page is not left awaiting a closed channel.
    setImmediate(() => autoUpdater.quitAndInstall(false, true));
  });

  const tell = async (message: string): Promise<void> => {
    const win = mainWindow();
    const options = { type: 'info' as const, title: 'Bill Book', message, buttons: ['OK'] };
    await (win ? dialog.showMessageBox(win, options) : dialog.showMessageBox(options));
  };

  return {
    async checkFromMenu(): Promise<void> {
      if (!app.isPackaged || !config.updateUrl) {
        await tell('Updates are checked by installed builds that have an update server set.');
        return;
      }

      try {
        const result = await autoUpdater.checkForUpdates();

        if (result?.isUpdateAvailable) {
          // The page decides from version.json whether this one may be put off.
          mainWindow()?.webContents.send(IPC.checkRequested);
        } else {
          await tell(`Bill Book ${app.getVersion()} is the latest version.`);
        }
      } catch {
        await tell('The update server could not be reached. Check the connection and try again.');
      }
    },
  };
}
