import { describe, expect, it } from 'vitest';
import { EscPosService } from './esc-pos.service';

/** The receipt's bytes (TK-41). */
describe('EscPosService', () => {
  const bytes = (data: Uint8Array) => Array.from(data);

  it('starts with a reset and ends with a feed and a cut', () => {
    const out = bytes(new EscPosService().receipt([{ text: 'Hi', align: 'left', style: 'normal' }]));

    expect(out.slice(0, 2)).toEqual([0x1b, 0x40]);
    expect(out.slice(-4)).toEqual([0x1d, 0x56, 0x41, 0x03]);
  });

  it('turns bold and double height on for a title row and off after it', () => {
    const out = bytes(new EscPosService().receipt([{ text: 'T', align: 'center', style: 'title' }]));
    const text = out.indexOf('T'.charCodeAt(0));

    expect(out.slice(2, 5)).toEqual([0x1b, 0x61, 0x01]);
    expect(out.slice(text - 6, text)).toEqual([0x1b, 0x45, 0x01, 0x1b, 0x21, 0x10]);
    expect(out.slice(text + 2, text + 8)).toEqual([0x1b, 0x21, 0x00, 0x1b, 0x45, 0x00]);
  });

  it('never sends a byte outside printable ASCII in text', () => {
    const out = bytes(new EscPosService().init().text('₹5 சோப்').generate()).slice(2);

    expect(String.fromCharCode(...out)).toBe('Rs5 ????');
  });

  it('starts a fresh buffer for every receipt', () => {
    const service = new EscPosService();
    const first = service.receipt([{ text: 'one', align: 'left', style: 'normal' }]);
    const second = service.receipt([{ text: 'one', align: 'left', style: 'normal' }]);

    expect(bytes(second)).toEqual(bytes(first));
  });
});
