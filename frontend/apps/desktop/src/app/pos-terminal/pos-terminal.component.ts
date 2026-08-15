import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InvoiceService, SaveInvoiceRequest } from '@bill-book/sales-core';
import { EscPosService } from './esc-pos.service';

@Component({
  selector: 'bb-pos-terminal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pos-terminal.component.html',
  styleUrl: './pos-terminal.component.scss'
})
export class PosTerminalComponent {
  private invoiceService = inject(InvoiceService);
  private escPosService = inject(EscPosService);

  checkout() {
    // POS terminal uses Invoice API to check out
    const request: SaveInvoiceRequest = {
      documentDate: new Date().toISOString().split('T')[0],
      contactId: 1, // Default walk-in customer
      exchangeRate: 1,
      currencyCode: 'USD',
      lines: []
    };
    this.invoiceService.create(request).subscribe(_res => {
      // Upon successful invoice generation, print ESC/POS receipt
      const receiptBytes = this.escPosService.generateReceipt(
        'BILL-BOOK STORE',
        request.lines.map(l => ({ name: 'Item', amount: l.unitPrice * l.quantity })),
        0 // Total would be computed from lines
      );

      this.printReceipt(receiptBytes);
    });
  }

  private printReceipt(bytes: Uint8Array) {
    // In a real desktop app (e.g. Electron/Tauri), this would be sent to the main process
    // which talks to the physical serial/USB thermal printer.
    console.log('Printing receipt. Bytes generated:', bytes.length);
  }
}
