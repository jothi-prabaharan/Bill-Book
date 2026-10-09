import { BrowserWindow, Menu, MenuItemConstructorOptions, PrinterInfo, app } from 'electron';
import { DesktopSettings } from './desktop-settings';

export interface MenuActions {
  settings(): DesktopSettings;
  update(change: Partial<DesktopSettings>): void;
  refreshPrinters(): void;
  checkForUpdates(): void;
}

function printerChoices(
  printers: readonly PrinterInfo[],
  chosen: string | null,
  choose: (name: string | null) => void,
): MenuItemConstructorOptions[] {
  return [
    { label: 'System default', type: 'radio', checked: chosen === null, click: () => choose(null) },
    { type: 'separator' },
    ...printers.map(
      (printer): MenuItemConstructorOptions => ({
        label: printer.displayName || printer.name,
        type: 'radio',
        checked: printer.name === chosen,
        click: () => choose(printer.name),
      }),
    ),
  ];
}

/**
 * The application menu (TK-184). Printing is where the till's receipt printer
 * and the label printer are chosen — per computer, which is where printers are
 * plugged in — and where silent printing can be switched off.
 */
export function buildMenu(printers: readonly PrinterInfo[], actions: MenuActions): void {
  const settings = actions.settings();
  const isMac = process.platform === 'darwin';

  const template: MenuItemConstructorOptions[] = [
    ...(isMac ? [{ role: 'appMenu' } as MenuItemConstructorOptions] : []),
    { label: 'File', submenu: [isMac ? { role: 'close' } : { role: 'quit' }] },
    { role: 'editMenu' },
    {
      label: 'View',
      submenu: [
        { role: 'reload' },
        ...(app.isPackaged ? [] : [{ role: 'toggleDevTools' } as MenuItemConstructorOptions]),
        { type: 'separator' },
        { role: 'resetZoom' },
        { role: 'zoomIn' },
        { role: 'zoomOut' },
        { type: 'separator' },
        { role: 'togglefullscreen' },
      ],
    },
    {
      label: 'Printing',
      submenu: [
        {
          label: 'Print receipts and labels without asking',
          type: 'checkbox',
          checked: settings.silentPrint,
          click: (item) => actions.update({ silentPrint: item.checked }),
        },
        { type: 'separator' },
        {
          label: 'Receipt printer',
          submenu: printerChoices(printers, settings.receiptPrinter, (name) => actions.update({ receiptPrinter: name })),
        },
        {
          label: 'Label printer',
          submenu: printerChoices(printers, settings.labelPrinter, (name) => actions.update({ labelPrinter: name })),
        },
        { type: 'separator' },
        { label: 'Refresh printer list', click: () => actions.refreshPrinters() },
      ],
    },
    { role: 'windowMenu' },
    {
      role: 'help',
      submenu: [
        { label: 'Check for updates', click: () => actions.checkForUpdates() },
        ...(isMac ? [] : [{ role: 'about' } as MenuItemConstructorOptions]),
      ],
    },
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

/** The printers the operating system knows, asked through a window's web contents. */
export async function listPrinters(win: BrowserWindow | null): Promise<PrinterInfo[]> {
  try {
    return win && !win.isDestroyed() ? await win.webContents.getPrintersAsync() : [];
  } catch {
    return [];
  }
}
