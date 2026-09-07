/**
 * An allowlist HTML sanitiser for the rich-text editor.
 *
 * **Why one is written here rather than trusting the editor.** The value of a
 * rich-text field is HTML, it arrives from a user, and it is rendered back into
 * other people's screens and onto printed documents. Everything about that
 * sentence is a cross-site scripting hole unless the HTML is filtered, and the
 * only filtering worth relying on is an **allowlist**: a denylist of `<script>`
 * and `onerror` fails to the attacker's advantage the first time somebody
 * invents an attribute nobody listed.
 *
 * Angular's `DomSanitizer` is not the answer on its own either. It sanitises on
 * the way *out*, when a value is bound with `[innerHTML]`, and it strips
 * silently as it goes — so the field would keep storing markup that is then not
 * rendered, and what is in the database would never match what anybody sees.
 * Filtering on the way *in* means the stored value is the safe value. The
 * rendering side still goes through Angular, so the two are belt and braces
 * rather than one instead of the other.
 *
 * ## What survives
 *
 * Emphasis, headings, lists, paragraphs, line breaks and links. No images —
 * they fetch from wherever the URL says, which is a tracker at best — no
 * tables, no styles, no classes, no ids, no `data-*`. A document's layout comes
 * from the print template, not from what somebody pasted out of Word.
 *
 * Everything not on the list is **unwrapped rather than deleted**: a
 * `<span style=…>` around a sentence loses the span and keeps the sentence.
 * Deleting the subtree would silently swallow text somebody wrote. The
 * exceptions are `<script>`, `<style>`, `<iframe>` and their kind, whose
 * contents are not text at all and go with them.
 */

/** Tags that survive, in lower case. */
const ALLOWED_TAGS: ReadonlySet<string> = new Set([
  'p', 'br', 'div',
  'b', 'strong', 'i', 'em', 'u', 's', 'strike', 'sub', 'sup',
  'ul', 'ol', 'li',
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
  'blockquote', 'code', 'pre',
  'a',
]);

/** Elements whose *contents* go with them, rather than being unwrapped. */
const DROP_WHOLE: ReadonlySet<string> = new Set([
  'script', 'style', 'iframe', 'object', 'embed', 'template', 'noscript',
]);

/** Attributes that survive, per tag. Nothing else does — no `style`, no `on*`. */
const ALLOWED_ATTRIBUTES: Readonly<Record<string, readonly string[]>> = {
  a: ['href', 'title'],
};

/** The only schemes a link may use. `javascript:` is the reason this exists. */
const SAFE_SCHEME = /^(?:https?:|mailto:|tel:)/i;

/**
 * A relative link — `/sales/invoices/42`, `#section` — which carries no scheme
 * and so cannot carry `javascript:`. A protocol-relative `//host` is excluded:
 * it inherits the page's scheme and points off-site, which is not what a
 * relative link in a note is meant to do.
 */
const RELATIVE_LINK = /^\/(?!\/)|^#|^\.\.?\//;

/**
 * Whitespace and control characters.
 *
 * Stripped before the scheme is read, because a tab or a newline inside
 * `java\tscript:` is ignored by the browser's URL parser and would otherwise
 * be the thing that gets a script past the check above.
 */
/* eslint-disable-next-line no-control-regex --
   The control characters are the point: they are what an obfuscated
   `javascript:` hides behind, so the range has to include them. */
const IGNORABLE = /[\u0000-\u0020]+/g;

/**
 * Parsed HTML, filtered to the allowlist.
 *
 * Uses `DOMParser`, which parses into a document that is **never connected to
 * the page** — no script runs, no image loads, no `onerror` fires. Building the
 * tree by assigning `innerHTML` on a detached element would be almost as safe,
 * but not quite: some browsers still fetch for a detached `<img src>`.
 */
export function sanitizeRichText(html: string): string {
  if (!html) {
    return '';
  }

  const parsed = new DOMParser().parseFromString(html, 'text/html');
  scrub(parsed.body);
  return parsed.body.innerHTML;
}

function scrub(parent: Element): void {
  // Backwards, so removing or unwrapping a child does not shift the ones not
  // yet visited.
  for (let at = parent.childNodes.length - 1; at >= 0; at -= 1) {
    const node = parent.childNodes[at];

    if (node.nodeType === Node.TEXT_NODE) {
      continue;
    }

    if (node.nodeType !== Node.ELEMENT_NODE) {
      // Comments, processing instructions, CDATA. None of them are content.
      parent.removeChild(node);
      continue;
    }

    const element = node as Element;
    const tag = element.tagName.toLowerCase();

    if (DROP_WHOLE.has(tag)) {
      parent.removeChild(element);
      continue;
    }

    scrub(element);

    if (!ALLOWED_TAGS.has(tag)) {
      unwrap(parent, element);
      continue;
    }

    scrubAttributes(element, tag);
  }
}

function scrubAttributes(element: Element, tag: string): void {
  const allowed = ALLOWED_ATTRIBUTES[tag] ?? [];

  for (const attribute of Array.from(element.attributes)) {
    if (!allowed.includes(attribute.name.toLowerCase())) {
      element.removeAttribute(attribute.name);
    }
  }

  if (tag !== 'a') {
    return;
  }

  const href = element.getAttribute('href');
  if (href === null || !isSafeHref(href)) {
    element.removeAttribute('href');
    return;
  }

  // A link out of the application opens in a new tab, and `noopener` is what
  // stops the opened page reaching back through `window.opener`.
  element.setAttribute('rel', 'noopener noreferrer');
  element.setAttribute('target', '_blank');
}

function isSafeHref(href: string): boolean {
  const value = href.replace(IGNORABLE, '').toLowerCase();
  return SAFE_SCHEME.test(value) || RELATIVE_LINK.test(value);
}

/** Replaces an element with its children, keeping the text somebody wrote. */
function unwrap(parent: Node, element: Element): void {
  while (element.firstChild) {
    parent.insertBefore(element.firstChild, element);
  }
  parent.removeChild(element);
}

/** The plain text of some HTML, for a character count or a preview. */
export function richTextToPlain(html: string): string {
  if (!html) {
    return '';
  }
  const parsed = new DOMParser().parseFromString(html, 'text/html');
  return (parsed.body.textContent ?? '').replace(/\s+/g, ' ').trim();
}

/** True when the markup carries no text and no break worth keeping. */
export function isRichTextEmpty(html: string): boolean {
  if (!html) {
    return true;
  }
  return richTextToPlain(html) === '' && !/<(br|hr)\b/i.test(html);
}
