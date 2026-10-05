import { PaperWidth } from './receipt-layout';

/**
 * A till's receipt settings (TK-41, TK-155): the paper width and an optional
 * footer. There is no printer to choose any more — the browser prints through
 * whichever printer the operating system has, at the roll's exact size.
 */
export interface PrinterSettings {
  paper: PaperWidth;
  footer?: string;
}

export const DEFAULT_PRINTER_SETTINGS: PrinterSettings = { paper: 80 };

/**
 * Reads stored settings field by field rather than trusting their shape, so a
 * till still holding the desktop app's settings (with a device path or a
 * network printer in them) reads as its paper and footer and nothing else.
 */
export function parsePrinterSettings(raw: string | null | undefined): PrinterSettings {
  if (!raw) {
    return DEFAULT_PRINTER_SETTINGS;
  }
  try {
    const value = JSON.parse(raw) as Partial<PrinterSettings> | null;
    if (typeof value !== 'object' || value === null) {
      return DEFAULT_PRINTER_SETTINGS;
    }
    const paper: PaperWidth = value.paper === 58 ? 58 : 80;
    const footer = typeof value.footer === 'string' && value.footer.trim() !== '' ? value.footer : undefined;
    return footer === undefined ? { paper } : { paper, footer };
  } catch {
    return DEFAULT_PRINTER_SETTINGS;
  }
}
