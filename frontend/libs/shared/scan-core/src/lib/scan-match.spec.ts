import { describe, expect, it } from 'vitest';
import { matchScannedItem } from './scan-match';

const soap = { itemId: 1, itemCode: 'SOAP01' };
const soapLarge = { itemId: 2, itemCode: 'SOAP01-L' };

describe('matchScannedItem', () => {
  it('takes the item whose code is exactly the scan, ignoring case', () => {
    expect(matchScannedItem('soap01', [soapLarge, soap])).toBe(soap);
  });

  it('takes the only match, as a barcode search returns', () => {
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
