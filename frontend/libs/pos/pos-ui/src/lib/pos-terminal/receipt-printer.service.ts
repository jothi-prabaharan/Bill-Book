import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  DEFAULT_PRINTER_SETTINGS,
  PrinterSettings,
  ReceiptBranch,
  ReceiptRow,
  ReceiptSale,
  parsePrinterSettings,
  receiptHtml,
  receiptRows,
} from '@bill-book/pos-core';
import { printHtml } from '@bill-book/ui-components';
import { firstValueFrom } from 'rxjs';
import { CodeWriter, receiptCodes } from './receipt-codes';

const SETTINGS_KEY = 'bb.pos.printer';

/**
 * Prints a till sale's receipt through the browser (TK-41, TK-155): reads the
 * posted invoice and the branch, lays the receipt out for the till's paper,
 * and prints it as a page exactly the roll's width. The operating system's
 * driver for the receipt printer does the rest, as it would for any page —
 * no desktop app, no printer commands.
 *
 * The paper width and footer are the till's, not the branch's, so they live in
 * this browser's storage beside the till id.
 */
@Injectable({ providedIn: 'root' })
export class ReceiptPrinterService {
  private readonly http = inject(HttpClient);

  /** Where the page goes; replaced in a test. */
  print: (html: string) => void = printHtml;

  settings(): PrinterSettings {
    try {
      return parsePrinterSettings(globalThis.localStorage?.getItem(SETTINGS_KEY));
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

  /** Draws the receipt's codes; replaced in a test. */
  codeWriter?: () => Promise<CodeWriter>;

  /**
   * Prints the receipt for a posted invoice. A reprint is marked DUPLICATE.
   * Under the text: the invoice number as a barcode, and a UPI QR for what is
   * still owed, when the branch has a UPI id and something is (TK-164).
   */
  async printInvoice(invoiceId: number, reprint = false): Promise<void> {
    const settings = this.settings();
    const [sale, branch, link] = await Promise.all([
      firstValueFrom(this.http.get<ReceiptSale>(`/api/sales/invoices/${invoiceId}`)),
      firstValueFrom(this.http.get<ReceiptBranch>('/api/organizations/current')),
      this.upiLink(invoiceId),
    ]);

    const codes = await receiptCodes(sale.documentNo, link, this.codeWriter);
    this.print(
      receiptHtml(receiptRows(branch, sale, { paper: settings.paper, reprint, footer: settings.footer }), settings.paper, codes),
    );
  }

  /** The UPI link for what is owed, or null — never a reason not to print. */
  private async upiLink(invoiceId: number): Promise<string | null> {
    try {
      const answer = await firstValueFrom(
        this.http.get<{ payUri?: string | null }>(`/api/sales/invoices/${invoiceId}/upi-link`),
      );
      return answer?.payUri ?? null;
    } catch {
      return null;
    }
  }

  /** A short test page, to check the paper width and the printer the browser picks. */
  printTest(settings: PrinterSettings): void {
    const width = settings.paper === 58 ? 32 : 48;
    const rows: ReceiptRow[] = [
      { text: 'Bill Book test print', align: 'center', style: 'bold' },
      { text: `${settings.paper} mm, ${width} columns`, align: 'center', style: 'normal' },
      { text: '1234567890'.repeat(5).slice(0, width), align: 'left', style: 'normal' },
    ];
    this.print(receiptHtml(rows, settings.paper));
  }
}
