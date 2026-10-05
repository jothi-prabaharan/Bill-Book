import { ReceiptCodes } from '@bill-book/pos-core';

/** The part of `zxing-wasm/writer` used here, so a test can stand in for it. */
export interface CodeWriter {
  writeBarcode(
    input: string,
    options: { format: string; scale?: number; withHRT?: boolean; withQuietZones?: boolean },
  ): Promise<{ svg: string; error: string }>;
}

let writer: Promise<CodeWriter> | null = null;

/**
 * The zxing writer (MIT, the same project as the camera fallback, D-46),
 * loaded on the first receipt and pointed at our own copy of its wasm.
 */
async function zxingWriter(): Promise<CodeWriter> {
  writer ??= import('zxing-wasm/writer').then((module) => {
    const wasm = new URL('assets/zxing/zxing_writer.wasm', document.baseURI).href;
    module.prepareZXingModule({
      overrides: {
        locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasm : prefix + path),
      },
    });
    return module as unknown as CodeWriter;
  });
  return writer;
}

/**
 * The receipt's codes (TK-164): the invoice number as a Code128 barcode, and
 * the UPI link as a QR when there is one. A code that cannot be drawn is left
 * off rather than stopping the receipt — the text is what the customer needs.
 */
export async function receiptCodes(
  documentNo: string | null | undefined,
  payUri: string | null | undefined,
  load: () => Promise<CodeWriter> = zxingWriter,
): Promise<ReceiptCodes> {
  const codes: ReceiptCodes = {};
  const number = documentNo?.trim();
  if (!number && !payUri) {
    return codes;
  }

  let zxing: CodeWriter;
  try {
    zxing = await load();
  } catch {
    return codes;
  }

  if (number) {
    try {
      const bar = await zxing.writeBarcode(number, { format: 'Code128', scale: 2, withHRT: false, withQuietZones: true });
      codes.barcodeSvg = bar.error ? null : bar.svg;
    } catch {
      codes.barcodeSvg = null;
    }
  }

  if (payUri) {
    try {
      const qr = await zxing.writeBarcode(payUri, { format: 'QRCode', scale: 4, withQuietZones: true });
      codes.upiQrSvg = qr.error ? null : qr.svg;
    } catch {
      codes.upiQrSvg = null;
    }
  }

  return codes;
}
