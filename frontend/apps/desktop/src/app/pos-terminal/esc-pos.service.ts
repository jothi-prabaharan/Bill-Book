import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class EscPosService {
  private buffer: number[] = [];

  init() {
    this.buffer = [0x1B, 0x40]; // ESC @
    return this;
  }

  alignCenter() {
    this.buffer.push(0x1B, 0x61, 0x01); // ESC a 1
    return this;
  }

  alignLeft() {
    this.buffer.push(0x1B, 0x61, 0x00); // ESC a 0
    return this;
  }

  alignRight() {
    this.buffer.push(0x1B, 0x61, 0x02); // ESC a 2
    return this;
  }

  bold(enable = true) {
    this.buffer.push(0x1B, 0x45, enable ? 0x01 : 0x00); // ESC E n
    return this;
  }

  text(text: string) {
    for (let i = 0; i < text.length; i++) {
      this.buffer.push(text.charCodeAt(i));
    }
    return this;
  }

  textLine(text: string) {
    return this.text(text).feed();
  }

  feed(lines = 1) {
    for (let i = 0; i < lines; i++) {
      this.buffer.push(0x0A); // LF
    }
    return this;
  }

  cut() {
    this.buffer.push(0x1D, 0x56, 0x41, 0x03); // GS V A 3 (partial cut with feed)
    return this;
  }

  generate(): Uint8Array {
    return new Uint8Array(this.buffer);
  }

  // Example helper to print a receipt
  generateReceipt(storeName: string, items: any[], total: number): Uint8Array {
    this.init()
      .alignCenter()
      .bold(true)
      .textLine(storeName)
      .bold(false)
      .feed()
      .alignLeft()
      .textLine('--------------------------------')
      .textLine('Item                      Amount')
      .textLine('--------------------------------');

    for (const item of items) {
      const name = (item.name || '').padEnd(24, ' ').substring(0, 24);
      const amount = (item.amount || 0).toFixed(2).padStart(8, ' ');
      this.textLine(`${name}${amount}`);
    }

    this.textLine('--------------------------------')
      .alignRight()
      .bold(true)
      .textLine(`TOTAL: ${total.toFixed(2)}`)
      .bold(false)
      .feed(3)
      .cut();

    return this.generate();
  }
}
