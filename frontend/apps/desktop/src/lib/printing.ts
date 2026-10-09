import { BrowserWindow, app } from 'electron';
import { randomUUID } from 'node:crypto';
import { rm, writeFile } from 'node:fs/promises';
import * as path from 'node:path';
import { DesktopSettings, printTarget } from './desktop-settings';
import { layoutWidthPx, printPageSize } from './print-page';

/**
 * Prints a page the web app built — a receipt, a kitchen ticket, labels — in
 * a hidden window (TK-184). Silent unless the user turned that off or it is a
 * document; at the page's own paper size, measured once it has laid out.
 *
 * The page goes through a temporary file rather than a data: URL, which
 * Chromium caps at 2 MB — a sheet of labels with a barcode each can pass that.
 */
export async function printPage(html: string, kind: string, settings: DesktopSettings): Promise<void> {
  const target = printTarget(kind, settings);
  const file = path.join(app.getPath('temp'), `bill-book-print-${randomUUID()}.html`);
  const win = new BrowserWindow({
    show: false,
    width: layoutWidthPx(html),
    height: 600,
    webPreferences: { sandbox: true, contextIsolation: true, nodeIntegration: false },
  });

  try {
    win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    win.webContents.on('will-navigate', (event) => event.preventDefault());
    await writeFile(file, html, 'utf8');
    await win.loadFile(file);

    const height = Number(await win.webContents.executeJavaScript('Math.ceil(document.documentElement.scrollHeight)'));
    const pageSize = printPageSize(html, Number.isFinite(height) ? height : 0);

    await new Promise<void>((resolve, reject) => {
      win.webContents.print(
        {
          silent: target.silent,
          printBackground: true,
          margins: { marginType: 'none' },
          ...(target.deviceName ? { deviceName: target.deviceName } : {}),
          ...(pageSize ? { pageSize } : {}),
        },
        (success, reason) => {
          // Cancelling the dialog is the user's answer, not a failure to fall back from.
          if (success || reason === 'cancelled') {
            resolve();
          } else {
            reject(new Error(reason || 'The printer did not accept the page.'));
          }
        },
      );
    });
  } finally {
    win.destroy();
    await rm(file, { force: true });
  }
}
