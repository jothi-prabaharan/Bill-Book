import { ChangeDetectionStrategy } from '@angular/core';
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InvoiceService, SaveInvoiceRequest } from '@bill-book/sales-core';
import { EscPosService } from './esc-pos.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-pos-terminal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pos-terminal.component.html',
  styleUrl: './pos-terminal.component.scss'
})
export class PosTerminalComponent {
  private invoiceService = inject(InvoiceService);
  private escPosService = inject(EscPosService);

  /**
   * A till sale, which is an ordinary invoice with a till on it.
   *
   * Still a scaffold: no cart, a hardcoded walk-in customer and no tender
   * handling. **POS is Phase 3** — what is worth knowing is that when it is
   * built it reuses this path rather than gaining one of its own, because a POS
   * sale is an `sal.Invoices` row with `TransactionTypeCode = 'POS'` and two
   * tables for one document would mean two places to fix a GST bug.
   */
  async checkout(): Promise<void> {
    const request: SaveInvoiceRequest = {
      documentDate: new Date().toISOString().split('T')[0],
      contactId: 1, // Default walk-in customer
      exchangeRate: 1,
      lines: []
    };

    // Awaited rather than subscribed: InvoiceService returns promises, so a
    // refusal can be caught here instead of vanishing into an error callback.
    await this.invoiceService.create(request);

    const receiptBytes = this.escPosService.generateReceipt(
      'BILL-BOOK STORE',
      request.lines.map(l => ({
        name: l.description ?? 'Item',
        // Rupees on the wire, so the receipt line is a plain multiplication.
        amount: (l.unitPrice ?? 0) * (l.quantity ?? 0)
      })),
      0 // Total would be computed from lines
    );

    this.printReceipt(receiptBytes);
  }

  private printReceipt(bytes: Uint8Array) {
    // In a real desktop app (e.g. Electron/Tauri), this would be sent to the main process
    // which talks to the physical serial/USB thermal printer.
    console.log('Printing receipt. Bytes generated:', bytes.length);
  }
}

