/**
 * Whether an Escape keydown that reached a page's `document` listener is meant for the page's own popup or
 * sheet, and not for something opened above it (TK-269).
 *
 * A page listens for Escape on `document` so the key works wherever the focus is. Three things can sit above
 * the page's popup, and each needs handling:
 *
 * - A date calendar closes itself on Escape and marks the key as handled (`defaultPrevented`).
 * - The batch picker closes itself on Escape and lets the key carry on up to `document`. Its inner
 *   elements may already have left the page by then, so it is found in the event's path, which is fixed when
 *   the key is pressed.
 * - The confirm prompt does not take focus, so the key is pressed on the button that opened it, outside the
 *   prompt, and the prompt never hears it. It is found by being open in the page.
 *
 * Without this one key press would close the popup under the layer as well, or close the popup and leave the
 * prompt standing over the page, and a half-typed stock movement would be lost.
 */
export function escapeIsForThisLayer(event: Event): boolean {
  if (event.defaultPrevented) {
    return false;
  }

  if (event.composedPath().some((node) => LAYERS_ABOVE.has((node as Element).localName))) {
    return false;
  }

  return document.querySelector(OPEN_LAYERS) === null;
}

/** The overlays a page's popup can open above itself, by their element names. */
const LAYERS_ABOVE: ReadonlySet<string> = new Set(['bb-lookup-dialog', 'bb-confirm-dialog']);

/** Those overlays while they are open: each draws a backdrop inside its element only then. */
const OPEN_LAYERS = 'bb-confirm-dialog .dialog-backdrop, bb-lookup-dialog .backdrop';
