import { describe, expect, it } from 'vitest';
import { MIN_RENEW_DELAY_MS, isFresh, readClaim, renewDelayMs } from './portal-session';

const encode = (payload: object) => `h.${btoa(JSON.stringify(payload)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')}.s`;

describe('readClaim (TK-69)', () => {
  it('reads a claim from the payload', () => {
    expect(readClaim(encode({ app: 'School', contact_id: '42' }), 'app')).toBe('School');
    expect(readClaim(encode({ contact_id: '42' }), 'contact_id')).toBe('42');
  });

  it('answers null for a missing claim or a malformed token', () => {
    expect(readClaim(encode({ contact_id: '42' }), 'app')).toBeNull();
    expect(readClaim('not-a-token', 'app')).toBeNull();
    expect(readClaim(null, 'app')).toBeNull();
  });
});

describe('renewDelayMs and isFresh (TK-94)', () => {
  const now = Date.parse('2026-09-26T06:00:00Z');

  it('renews five minutes before the hour runs out', () => {
    expect(renewDelayMs('2026-09-26T07:00:00Z', now)).toBe(55 * 60_000);
  });

  it('never renews sooner than thirty seconds, even for a spent or unreadable expiry', () => {
    expect(renewDelayMs('2026-09-26T06:02:00Z', now)).toBe(MIN_RENEW_DELAY_MS);
    expect(renewDelayMs('2026-09-26T05:00:00Z', now)).toBe(MIN_RENEW_DELAY_MS);
    expect(renewDelayMs('not a date', now)).toBe(MIN_RENEW_DELAY_MS);
  });

  it('treats a session with a minute or less to run as spent', () => {
    expect(isFresh('2026-09-26T07:00:00Z', now)).toBe(true);
    expect(isFresh('2026-09-26T06:00:30Z', now)).toBe(false);
    expect(isFresh(null, now)).toBe(false);
  });
});
