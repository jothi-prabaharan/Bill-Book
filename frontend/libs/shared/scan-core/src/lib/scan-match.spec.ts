import { describe, expect, it } from 'vitest';
import { matchScannedItem } from './scan-match';

const soap = { itemId: 1, itemCode: 'SOAP01' };
const soapLarge = { itemId: 2, itemCode: 'SOAP01-L' };

describe('matchScannedItem', () => {
  it('takes the item whose code is exactly the scan, ignoring case', () => {
    expect(matchScannedItem('soap01', [soapLarge, soap])).toBe(soap);
  });

  it('takes the only match from a list that does not carry barcodes', () => {
    expect(matchScannedItem('8901030865278', [soap])).toBe(soap);
  });

  it('leaves several matches for a person to choose', () => {
    expect(matchScannedItem('SOAP', [soap, soapLarge])).toBeNull();
  });

  it('finds nothing for no match or an empty code', () => {
    expect(matchScannedItem('X', [])).toBeNull();
    expect(matchScannedItem('  ', [soap])).toBeNull();
  });
});

describe('matchScannedItem with barcodes (TK-164)', () => {
  const zinc = { itemId: 3, itemCode: 'Z-1', barcodeValues: ['1234567'] };
  const promo = { itemId: 4, itemCode: 'A-1', barcodeValues: [] as string[] };

  it('confirms the item one of whose barcodes is exactly the scan, wherever it ranks', () => {
    expect(matchScannedItem('1234567', [promo, zinc])).toBe(zinc);
  });

  it('a lone match whose barcodes do not include the scan is not added', () => {
    // Found because its name contains the digits, not because it carries the code.
    expect(matchScannedItem('1234567', [promo])).toBeNull();
  });

  it('still takes an exact item code from a list with barcodes', () => {
    expect(matchScannedItem('a-1', [zinc, promo])).toBe(promo);
  });
});
