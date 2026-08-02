/**
 * Reads the permission claims out of an access token.
 *
 * The token already carries them — Identity puts one `permission` claim on it
 * per permission the role holds — so the alternative was adding the same list to
 * the login response and having two copies that can disagree. This has one.
 *
 * **This is for the user interface only.** It decides which menu items to draw
 * and which routes to offer. Nothing here is a security boundary: the token is
 * in the browser and anyone can edit what a browser decodes. The server checks
 * the same claims against a signature on every request, and that is the check
 * that counts.
 */
export function readPermissions(token: string | null): string[] {
  const claims = readClaims(token);
  const value = claims['permission'];

  if (typeof value === 'string') {
    return [value];
  }

  // A single permission arrives as a string and several as an array — that is
  // how JWT serialises a repeated claim, not an inconsistency to normalise away
  // at the source.
  return Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];
}

function readClaims(token: string | null): Record<string, unknown> {
  const payload = token?.split('.')[1];
  if (!payload) {
    return {};
  }

  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

    // Decoded as UTF-8 rather than left as atob's binary string. Permissions are
    // ASCII, so for this function alone atob would do — the mojibake a byte-wise
    // read produces is still structurally valid JSON, because every UTF-8
    // continuation byte is ordinary string content. It matters for whatever
    // claim is read next: display_name in Tamil is an ordinary thing here, and
    // it would come back as gibberish.
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    const json = new TextDecoder().decode(bytes);

    const parsed: unknown = JSON.parse(json);
    return typeof parsed === 'object' && parsed !== null
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    // A token this build cannot read must not blank the navigation. The caller
    // treats an empty list as "show nothing extra", and the server still
    // answers every request correctly.
    return {};
  }
}
