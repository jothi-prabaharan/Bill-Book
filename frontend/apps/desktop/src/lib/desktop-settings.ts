/** The desktop app's own choices, kept in the user's profile (TK-184). */
export interface DesktopSettings {
  /** Receipts and labels print without a dialog. */
  readonly silentPrint: boolean;
  /** Printer names as the operating system lists them; null is the system default. */
  readonly receiptPrinter: string | null;
  readonly labelPrinter: string | null;
}

export const DEFAULT_SETTINGS: DesktopSettings = { silentPrint: true, receiptPrinter: null, labelPrinter: null };

export function parseSettings(text: string | null): DesktopSettings {
  if (text === null) {
    return DEFAULT_SETTINGS;
  }

  try {
    const value = JSON.parse(text) as Record<string, unknown> | null;

    if (value === null || typeof value !== 'object') {
      return DEFAULT_SETTINGS;
    }

    return {
      silentPrint: typeof value['silentPrint'] === 'boolean' ? value['silentPrint'] : DEFAULT_SETTINGS.silentPrint,
      receiptPrinter: printerName(value['receiptPrinter']),
      labelPrinter: printerName(value['labelPrinter']),
    };
  } catch {
    return DEFAULT_SETTINGS;
  }
}

function printerName(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null;
}

export type PrintTarget = { readonly silent: boolean; readonly deviceName: string | null };

/** Which printer a page goes to, and whether a dialog asks first. A document always asks. */
export function printTarget(kind: string, settings: DesktopSettings): PrintTarget {
  if (kind === 'receipt') {
    return { silent: settings.silentPrint, deviceName: settings.receiptPrinter };
  }

  if (kind === 'label') {
    return { silent: settings.silentPrint, deviceName: settings.labelPrinter };
  }

  return { silent: false, deviceName: null };
}
