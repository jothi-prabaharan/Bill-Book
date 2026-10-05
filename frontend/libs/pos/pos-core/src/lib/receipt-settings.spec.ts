import { describe, expect, it } from 'vitest';
import { DEFAULT_PRINTER_SETTINGS, parsePrinterSettings } from './receipt-settings';

describe('parsePrinterSettings (TK-155)', () => {
  it('defaults to 80 mm with no footer', () => {
    expect(parsePrinterSettings(null)).toEqual(DEFAULT_PRINTER_SETTINGS);
    expect(parsePrinterSettings('not json')).toEqual(DEFAULT_PRINTER_SETTINGS);
    expect(parsePrinterSettings('null')).toEqual(DEFAULT_PRINTER_SETTINGS);
  });

  it('keeps the paper and footer of the desktop app settings and drops the printer', () => {
    const stored = JSON.stringify({ paper: 58, target: { kind: 'device', path: '/dev/usb/lp0' }, footer: 'Thanks!' });
    expect(parsePrinterSettings(stored)).toEqual({ paper: 58, footer: 'Thanks!' });
  });

  it('reads any other paper as 80 mm and a blank footer as none', () => {
    expect(parsePrinterSettings(JSON.stringify({ paper: 112, footer: '  ' }))).toEqual({ paper: 80 });
  });
});
