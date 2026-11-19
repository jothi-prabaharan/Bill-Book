import { afterEach, describe, expect, it } from 'vitest';
import { escapeIsForThisLayer } from './overlay-escape';

/** Presses Escape on `target` and reports what a `document` listener, which is where a page listens, decides. */
function pressEscape(target: Element, beforeDocument?: (event: Event) => void): boolean {
  let decision: boolean | null = null;
  const onDocument = (event: Event): void => {
    decision = escapeIsForThisLayer(event);
  };

  document.addEventListener('keydown', onDocument);
  if (beforeDocument) {
    target.addEventListener('keydown', beforeDocument);
  }
  try {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, composed: true, cancelable: true }));
  } finally {
    document.removeEventListener('keydown', onDocument);
  }

  if (decision === null) {
    throw new Error('The Escape never reached the document listener.');
  }
  return decision;
}

function mount(host: string): { host: HTMLElement; field: HTMLInputElement } {
  const element = document.createElement(host);
  const field = document.createElement('input');
  element.append(field);
  document.body.append(element);
  return { host: element, field };
}

describe('escapeIsForThisLayer (TK-269)', () => {
  afterEach(() => {
    document.body.replaceChildren();
  });

  it('OVE-01: an Escape from an ordinary field is for the popup', () => {
    const { field } = mount('div');

    expect(pressEscape(field)).toBe(true);
  });

  it('OVE-02: an Escape raised inside the batch picker is not', () => {
    const { field } = mount('bb-lookup-dialog');

    expect(pressEscape(field)).toBe(false);
  });

  it('OVE-03: an Escape raised inside the confirm prompt is not', () => {
    const { field } = mount('bb-confirm-dialog');

    expect(pressEscape(field)).toBe(false);
  });

  it('OVE-04: an Escape another control has already handled is not', () => {
    const { field } = mount('div');

    // The date calendar prevents the default of the Escape that closes it.
    expect(pressEscape(field, (event) => event.preventDefault())).toBe(false);
  });

  it('OVE-05: the picker is still found when it has closed and left the page before the page hears the key', () => {
    const { host, field } = mount('bb-lookup-dialog');

    // The picker closes itself on the key, and Angular may have removed its contents by the time
    // the key reaches the document; the path read when the key was pressed still names it.
    expect(pressEscape(field, () => field.remove())).toBe(false);
    expect(host.contains(field)).toBe(false);
  });

  it('OVE-06: an Escape pressed outside an open confirm prompt is not for the popup, because the prompt takes no focus', () => {
    const { host } = mount('bb-confirm-dialog');
    const backdrop = document.createElement('div');
    backdrop.className = 'dialog-backdrop';
    host.append(backdrop);
    const { field: trigger } = mount('div');

    // The focus is still on the button that opened the prompt, which is outside it.
    expect(pressEscape(trigger)).toBe(false);
  });

  it('OVE-07: an Escape is for the popup again once the prompt has closed and drawn no backdrop', () => {
    mount('bb-confirm-dialog');
    const { field } = mount('div');

    expect(pressEscape(field)).toBe(true);
  });

  it('OVE-08: the same goes for the batch picker while it is open', () => {
    const { host } = mount('bb-lookup-dialog');
    const backdrop = document.createElement('div');
    backdrop.className = 'backdrop';
    host.append(backdrop);
    const { field } = mount('div');

    expect(pressEscape(field)).toBe(false);
  });
});
