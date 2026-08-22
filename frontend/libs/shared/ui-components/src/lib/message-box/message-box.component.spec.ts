import { describe, expect, it } from 'vitest';
import {
  dominantTone,
  MessageTone,
  toneLabel,
  toneLiveness,
  UiMessage,
} from './message-box.model';

/**
 * The box's only real decision is which tone wins when several are present, and
 * that is worth pinning: a stock shortfall shown beside a saved-successfully
 * note must not render as a green box.
 */
describe('MessageBox tone', () => {
  const message = (tone: MessageTone, text: string): UiMessage => ({ tone, text });

  it('MSG-01: an empty box is info, so a page can bind it unconditionally', () => {
    expect(dominantTone([])).toBe('info');
  });

  it('MSG-02: an error outranks every other tone present', () => {
    const tone = dominantTone([
      message('success', 'Sales order saved.'),
      message('info', 'Reserved 4 units.'),
      message('error', 'There is not enough stock available to reserve: C7 - Name 7.'),
    ]);

    expect(tone).toBe('error');
    expect(toneLabel(tone)).toBe('Errors');
  });

  it('MSG-03: a warning outranks success and info but not an error', () => {
    expect(
      dominantTone([message('info', 'Draft saved.'), message('warning', 'Delivery date is in the past.')]),
    ).toBe('warning');

    expect(
      dominantTone([message('warning', 'Delivery date is in the past.'), message('error', 'Only a draft can be edited.')]),
    ).toBe('error');
  });

  it('MSG-04: only an error interrupts a screen reader', () => {
    expect(toneLiveness('success')).toBe('polite');
    expect(toneLiveness('warning')).toBe('polite');
    expect(toneLiveness('info')).toBe('polite');
    expect(toneLiveness('error')).toBe('assertive');
  });

  it('MSG-05: info is the tone when nothing louder is present', () => {
    const tone = dominantTone([message('info', 'Converted from QT/2026/0007.')]);

    expect(tone).toBe('info');
    expect(toneLabel(tone)).toBe('Information');
  });
});
