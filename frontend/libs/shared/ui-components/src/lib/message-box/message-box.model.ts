/**
 * What a message box is saying, which is the only thing that changes its colour.
 *
 * Deliberately four words rather than a colour name. A page that asks for
 * `error` keeps saying the right thing when the palette changes; a page that
 * asks for `red` has to be found and edited.
 */
export type MessageTone = 'error' | 'warning' | 'success' | 'info';

/**
 * One message on the box.
 *
 * `detail` carries the lines a rule wants to enumerate — the three items that
 * were short of stock, the two lines missing an account — so the summary stays
 * one sentence and the specifics stay beneath it.
 */
export interface UiMessage {
  tone: MessageTone;
  text: string;
  detail?: readonly string[];
}

/** Loudest first. An error present anywhere makes the whole box an error. */
const TONE_PRECEDENCE: readonly MessageTone[] = ['error', 'warning', 'success', 'info'];

/**
 * The tone the box takes, given everything it is showing.
 *
 * One box with one edge reads as one problem; four differently-coloured stripes
 * stacked up read as decoration. So a stock shortfall shown beside a
 * saved-successfully note is an error box, not a green one.
 *
 * A plain function rather than a method, so it can be tested without a
 * component fixture — signal inputs cannot be set from outside one, and this is
 * the only real decision the component makes.
 */
export function dominantTone(messages: readonly UiMessage[]): MessageTone {
  return TONE_PRECEDENCE.find((tone) => messages.some((m) => m.tone === tone)) ?? 'info';
}

/** What a screen reader announces the box as. */
export function toneLabel(tone: MessageTone): string {
  switch (tone) {
    case 'error':
      return 'Errors';
    case 'warning':
      return 'Warnings';
    case 'success':
      return 'Done';
    default:
      return 'Information';
  }
}

/**
 * `assertive` only for an error: a screen reader interrupting to announce a
 * success is worse than waiting for a pause.
 */
export function toneLiveness(tone: MessageTone): 'assertive' | 'polite' {
  return tone === 'error' ? 'assertive' : 'polite';
}
