import { describe, expect, it, vi } from 'vitest';
import { CodeWriter, receiptCodes } from './receipt-codes';

function writer(fail: string[] = []): CodeWriter & { writeBarcode: ReturnType<typeof vi.fn> } {
  return {
    writeBarcode: vi.fn((input: string, options: { format: string }) =>
      Promise.resolve(
        fail.includes(options.format)
          ? { svg: '', error: 'cannot encode' }
          : { svg: `<svg>${options.format}:${input}</svg>`, error: '' },
      ),
    ),
  };
}

describe('receiptCodes (TK-164)', () => {
  it('draws the invoice number as a Code128 barcode and the UPI link as a QR', async () => {
    const zxing = writer();
    const codes = await receiptCodes('INV-0042', 'upi://pay?pa=a@b', () => Promise.resolve(zxing));

    expect(codes.barcodeSvg).toBe('<svg>Code128:INV-0042</svg>');
    expect(codes.upiQrSvg).toBe('<svg>QRCode:upi://pay?pa=a@b</svg>');
  });

  it('draws no QR when nothing is owed', async () => {
    const codes = await receiptCodes('INV-0042', null, () => Promise.resolve(writer()));
    expect(codes.upiQrSvg).toBeUndefined();
  });

  it('leaves off a code it cannot draw, and never stops the receipt', async () => {
    expect((await receiptCodes('INV-0042', 'upi://x', () => Promise.resolve(writer(['QRCode'])))).upiQrSvg).toBeNull();
    expect(await receiptCodes('INV-0042', 'upi://x', () => Promise.reject(new Error('no wasm')))).toEqual({});
  });

  it('loads nothing when there is nothing to draw', async () => {
    const load = vi.fn();
    expect(await receiptCodes('', null, load)).toEqual({});
    expect(load).not.toHaveBeenCalled();
  });
});
