import { describe, expect, it } from 'vitest';
import { DEFAULT_SETTINGS, parseSettings, printTarget } from './desktop-settings';

describe('parseSettings', () => {
  it('starts silent, on the system default printers', () => {
    expect(parseSettings(null)).toEqual({ silentPrint: true, receiptPrinter: null, labelPrinter: null });
  });

  it('reads what the menu saved', () => {
    expect(parseSettings('{"silentPrint":false,"receiptPrinter":"TVS RP3160","labelPrinter":"Zebra ZD220"}')).toEqual({
      silentPrint: false,
      receiptPrinter: 'TVS RP3160',
      labelPrinter: 'Zebra ZD220',
    });
  });

  it('falls back field by field on a damaged file', () => {
    expect(parseSettings('{"silentPrint":"yes","receiptPrinter":""}')).toEqual(DEFAULT_SETTINGS);
    expect(parseSettings('{oops')).toEqual(DEFAULT_SETTINGS);
  });
});

describe('printTarget', () => {
  const settings = { silentPrint: true, receiptPrinter: 'Receipt', labelPrinter: 'Label' };

  it('sends receipts and labels to their own printers', () => {
    expect(printTarget('receipt', settings)).toEqual({ silent: true, deviceName: 'Receipt' });
    expect(printTarget('label', settings)).toEqual({ silent: true, deviceName: 'Label' });
  });

  it('asks first when silent printing is off', () => {
    expect(printTarget('receipt', { ...settings, silentPrint: false }).silent).toBe(false);
  });

  it('always opens the dialog for a document', () => {
    expect(printTarget('document', settings)).toEqual({ silent: false, deviceName: null });
  });
});
