import { Injectable } from '@angular/core';
import { ReceiptRow, printable } from './receipt-layout';

const ESC = 0x1b;
const GS = 0x1d;
const LF = 0x0a;

/**
 * ESC/POS commands for a thermal receipt printer (TK-41).
 *
 * The layout is `receipt-layout.ts`'s; this only turns its rows into bytes, so
 * the same rows print on any ESC/POS printer (and on an emulator listening on
 * port 9100). A receipt is text in font A, never a PDF: a browser cannot reach
 * a USB or serial printer, so the bytes go out through the desktop app's
 * Electron bridge (`ReceiptPrinterService`).
 *
 * Each call to `receipt` or `init` starts a new buffer, so one instance can be
 * shared across sales.
 */
@Injectable({ providedIn: 'root' })
export class EscPosService {
  private buffer: number[] = [];

  init() {
    this.buffer = [ESC, 0x40]; // ESC @: reset
    return this;
  }

  alignCenter() {
    this.buffer.push(ESC, 0x61, 0x01); // ESC a 1
    return this;
  }

  alignLeft() {
    this.buffer.push(ESC, 0x61, 0x00); // ESC a 0
    return this;
  }

  alignRight() {
    this.buffer.push(ESC, 0x61, 0x02); // ESC a 2
    return this;
  }

  bold(enable = true) {
    this.buffer.push(ESC, 0x45, enable ? 0x01 : 0x00); // ESC E n
    return this;
  }

  /** Double height, same width, so a title row keeps the paper's column count. */
  doubleHeight(enable = true) {
    this.buffer.push(ESC, 0x21, enable ? 0x10 : 0x00); // ESC ! n
    return this;
  }

  /** Printable ASCII only; anything else prints as `?` (see `printable`). */
  text(text: string) {
    const clean = printable(text);
    for (let i = 0; i < clean.length; i++) {
      this.buffer.push(clean.charCodeAt(i));
    }
    return this;
  }

  textLine(text: string) {
    return this.text(text).feed();
  }

  feed(lines = 1) {
    for (let i = 0; i < lines; i++) {
      this.buffer.push(LF);
    }
    return this;
  }

  cut() {
    this.buffer.push(GS, 0x56, 0x41, 0x03); // GS V A 3: feed and partial cut
    return this;
  }

  generate(): Uint8Array {
    return new Uint8Array(this.buffer);
  }

  /** A whole receipt: reset, each row in its alignment and style, feed, cut. */
  receipt(rows: readonly ReceiptRow[]): Uint8Array {
    this.init();

    for (const row of rows) {
      if (row.align === 'center') {
        this.alignCenter();
      } else {
        this.alignLeft();
      }
      if (row.style !== 'normal') {
        this.bold(true);
      }
      if (row.style === 'title') {
        this.doubleHeight(true);
      }

      this.textLine(row.text);

      if (row.style === 'title') {
        this.doubleHeight(false);
      }
      if (row.style !== 'normal') {
        this.bold(false);
      }
    }

    return this.alignLeft().feed(3).cut().generate();
  }
}
