/**
 * Unique DOM ids for a control and the elements that describe it.
 *
 * A label needs `for`, and `aria-describedby` needs an id per hint and per
 * error. A caller may supply the control's id — pages that already write one do
 * — but most do not, and a hard-coded id would collide the moment two of the
 * same control appear on one screen. So one counter, and ids derived from it.
 *
 * Not a service: an injectable counter would reset per injector and give two
 * controls in different lazy-loaded routes the same id.
 */
let counter = 0;

export function nextFieldId(prefix: string): string {
  counter += 1;
  return `${prefix}-${counter}`;
}

/** Test seam. Resets the counter so ids in a spec are predictable. */
export function resetFieldIds(): void {
  counter = 0;
}
