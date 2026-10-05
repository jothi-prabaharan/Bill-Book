import { describe, expect, it } from 'vitest';
import { canCancel, cancelNeedsApproval, isLate, minutesSince, nextKitchenSteps } from './restaurant-kitchen';

describe('kitchen', () => {
  it('moves a dish forward only', () => {
    expect(nextKitchenSteps('Sent')).toEqual(['Preparing', 'Ready']);
    expect(nextKitchenSteps('Preparing')).toEqual(['Ready']);
    expect(nextKitchenSteps('Ready')).toEqual(['Served']);
    expect(nextKitchenSteps('Served')).toEqual([]);
    expect(nextKitchenSteps('Cancelled')).toEqual([]);
  });

  it('asks for approval to cancel once the kitchen has started', () => {
    expect(cancelNeedsApproval('Sent')).toBe(false);
    expect(cancelNeedsApproval('Preparing')).toBe(true);
    expect(canCancel('Served')).toBe(false);
    expect(canCancel('Ready')).toBe(true);
  });

  it('says how long a ticket has waited', () => {
    const now = new Date('2026-10-05T13:30:00');
    expect(minutesSince('2026-10-05T13:29:40', now)).toBe('Now');
    expect(minutesSince('2026-10-05T13:26:00', now)).toBe('4 min');
    expect(minutesSince('2026-10-05T12:18:00', now)).toBe('1 h 12 min');
    expect(isLate('2026-10-05T13:10:00', now)).toBe(true);
    expect(isLate('2026-10-05T13:11:00', now)).toBe(false);
  });
});
