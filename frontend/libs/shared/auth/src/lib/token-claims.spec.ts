import { describe, expect, it } from 'vitest';
import { readPermissions } from './token-claims';

/**
 * The menu is drawn from what this returns, so every way it can fail is a user
 * staring at an empty navigation and concluding the product is broken. Returning
 * nothing is the safe answer for a token that cannot be read — but only because
 * the server refuses the same requests regardless.
 */
describe('readPermissions', () => {
  /** Builds a token the way a JWT actually encodes: base64url, unpadded. */
  const tokenFor = (claims: Record<string, unknown>): string => {
    const json = JSON.stringify(claims);
    const bytes = new TextEncoder().encode(json);
    const binary = String.fromCharCode(...bytes);
    const base64url = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return `header.${base64url}.signature`;
  };

  it('reads several permissions', () => {
    const token = tokenFor({ permission: ['inventory.view', 'inventory.edit'] });

    expect(readPermissions(token)).toEqual(['inventory.view', 'inventory.edit']);
  });

  it('reads a single permission, which arrives as a string not an array', () => {
    // How JWT serialises a repeated claim that happened once. A role with
    // exactly one permission is unusual but entirely legal.
    expect(readPermissions(tokenFor({ permission: 'dashboard.view' }))).toEqual([
      'dashboard.view',
    ]);
  });

  it('survives a token whose payload is not ASCII', () => {
    // A display name in Tamil is ordinary here. This is a weaker guarantee than
    // it looks: a byte-wise decode would mangle that name but still parse, since
    // UTF-8 continuation bytes are valid string content — so permissions come
    // back either way. It is here to catch a payload that stops parsing at all,
    // and to pin the decoding for whatever claim is read next.
    const token = tokenFor({
      display_name: 'ராஜேஷ் குமார்',
      permission: ['sales.view'],
    });

    expect(readPermissions(token)).toEqual(['sales.view']);
  });

  it('handles a payload whose length needs base64 padding', () => {
    // base64url strips '=' padding. atob rejects the result unless it is put
    // back, and which lengths need it depends on the claims — so this fails
    // intermittently rather than always, which is worse.
    const lengths = [1, 2, 3, 4, 5];
    for (const n of lengths) {
      const token = tokenFor({ pad: 'x'.repeat(n), permission: ['reports.view'] });
      expect(readPermissions(token)).toEqual(['reports.view']);
    }
  });

  it('returns nothing for a token with no permissions', () => {
    expect(readPermissions(tokenFor({ sub: 'someone' }))).toEqual([]);
  });

  it('returns nothing rather than throwing on a malformed token', () => {
    expect(readPermissions(null)).toEqual([]);
    expect(readPermissions('')).toEqual([]);
    expect(readPermissions('not-a-jwt')).toEqual([]);
    expect(readPermissions('header..signature')).toEqual([]);
    expect(readPermissions('header.!!!not-base64!!!.signature')).toEqual([]);
    expect(readPermissions(`header.${btoa('[1,2,3]')}.signature`)).toEqual([]);
  });
});
