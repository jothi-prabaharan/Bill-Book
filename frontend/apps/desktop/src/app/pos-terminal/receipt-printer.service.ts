import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { EscPosService } from './esc-pos.service';
import { PaperWidth, ReceiptBranch, ReceiptSale, receiptRows } from './receipt-layout';

/**
 * Where the receipt bytes go.
 *
 * - `device`: a path the operating system exposes, written as a file. On
 *   Linux a USB printer is `/dev/usb/lp0` and a serial one `/dev/ttyS0` or
 *   `/dev/ttyUSB0` (set its baud rate with `stty` once); on Windows a serial
 *   port is `\\.\COM3` and a shared printer `\\localhost\Receipt`.
 * - `network`: raw TCP, port 9100 by default, which is how an Ethernet or
 *   Wi-Fi receipt printer and every ESC/POS emulator listen.
 */
export type PrinterTarget =
  | { kind: 'device'; path: string }
  | { kind: 'network'; host: string; port: number };

export interface PrinterSettings {
  paper: PaperWidth;
  target: PrinterTarget | null;
  footer?: string;
}

/** What `apps/desktop/preload.js` puts on `window` (Electron only). */
export interface PrinterBridge {
  print(bytes: Uint8Array, target: PrinterTarget): Promise<void>;
}

const SETTINGS_KEY = 'bb.pos.printer';

export const DEFAULT_PRINTER_SETTINGS: PrinterSettings = { paper: 80, target: null };

/**
 * Prints a till sale's receipt (TK-41): reads the posted invoice and the
 * branch, lays the receipt out for the till's paper, and sends the ESC/POS
 * bytes to the printer through the Electron bridge.
 *
 * The printer's settings are the till's, not the branch's, so they live in
 * this machine's storage beside the till id.
 */
@Injectable({ providedIn: 'root' })
export class ReceiptPrinterService {
  private readonly http = inject(HttpClient);
  private readonly escPos = inject(EscPosService);

  settings(): PrinterSettings {
    try {
      const raw = globalThis.localStorage?.getItem(SETTINGS_KEY);
      return raw ? ReceiptPrinterService.parseSettings(raw) : DEFAULT_PRINTER_SETTINGS;
    } catch {
      return DEFAULT_PRINTER_SETTINGS;
    }
  }

  saveSettings(settings: PrinterSettings): void {
    try {
      globalThis.localStorage?.setItem(SETTINGS_KEY, JSON.stringify(settings));
    } catch {
      // Storage refused (private window): the settings last until the till is closed.
    }
  }

  /** Whether this window can reach a printer at all: only the Electron app can. */
  canPrint(): boolean {
    return ReceiptPrinterService.bridge() !== null;
  }

  /**
   * Prints the receipt for a posted invoice. A reprint is marked DUPLICATE.
   * Throws with a sentence a cashier can act on when there is no printer.
   */
  async printInvoice(invoiceId: number, reprint = false): Promise<void> {
    const settings = this.settings();
    const bridge = ReceiptPrinterService.bridge();
    if (bridge === null) {
      throw new Error('Receipts print only from the desktop app; this window cannot reach a printer.');
    }
    if (settings.target === null) {
      throw new Error('No receipt printer is set up on this till. Open Printer settings to choose one.');
    }

    const [sale, branch] = await Promise.all([
      firstValueFrom(this.http.get<ReceiptSale>(`/api/sales/invoices/${invoiceId}`)),
      firstValueFrom(this.http.get<ReceiptBranch>('/api/organizations/current')),
    ]);

    const bytes = this.escPos.receipt(
      receiptRows(branch, sale, { paper: settings.paper, reprint, footer: settings.footer }),
    );
    await bridge.print(bytes, settings.target);
  }

  /** A short test page, to check the connection and the paper width. */
  async printTest(settings: PrinterSettings): Promise<void> {
    const bridge = ReceiptPrinterService.bridge();
    if (bridge === null || settings.target === null) {
      throw new Error('Receipts print only from the desktop app, to a printer chosen here.');
    }
    const width = settings.paper === 58 ? 32 : 48;
    const bytes = this.escPos
      .init()
      .alignCenter()
      .bold(true)
      .textLine('Bill Book test print')
      .bold(false)
      .textLine(`${settings.paper} mm, ${width} columns`)
      .alignLeft()
      .textLine('1234567890'.repeat(5).slice(0, width))
      .feed(3)
      .cut()
      .generate();
    await bridge.print(bytes, settings.target);
  }

  /** Reads stored settings, falling back field by field rather than trusting the shape. */
  static parseSettings(raw: string): PrinterSettings {
    const value = JSON.parse(raw) as Partial<PrinterSettings>;
    const paper: PaperWidth = value.paper === 58 ? 58 : 80;
    const target = ReceiptPrinterService.parseTarget(value.target);
    return { paper, target, footer: typeof value.footer === 'string' ? value.footer : undefined };
  }

  private static parseTarget(value: unknown): PrinterTarget | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }
    const target = value as Record<string, unknown>;
    if (target['kind'] === 'device' && typeof target['path'] === 'string' && target['path'].trim() !== '') {
      return { kind: 'device', path: target['path'].trim() };
    }
    if (target['kind'] === 'network' && typeof target['host'] === 'string' && target['host'].trim() !== '') {
      const port = Number(target['port']);
      return { kind: 'network', host: target['host'].trim(), port: Number.isInteger(port) && port > 0 ? port : 9100 };
    }
    return null;
  }

  private static bridge(): PrinterBridge | null {
    const candidate = (globalThis as { billBookPrinter?: PrinterBridge }).billBookPrinter;
    return candidate && typeof candidate.print === 'function' ? candidate : null;
  }
}
