import { describe, expect, it } from 'vitest';
import { readClaim } from './portal-session';

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
