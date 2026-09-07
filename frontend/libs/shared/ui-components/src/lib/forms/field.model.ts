/**
 * The shapes the common inputs take from a page.
 *
 * Deliberately plain interfaces rather than classes: a page builds these from
 * whatever its API returned, and a constructor would make that a mapping step
 * instead of an object literal.
 */

/** One choice in a `bb-select`. */
export interface BbSelectOption<TValue = string | number> {
  value: TValue;
  label: string;
  /** An option that exists but cannot be chosen — a closed period, a locked role. */
  disabled?: boolean;
  /** Puts the option under an `<optgroup>` of this name. */
  group?: string;
}

/** One choice in a `bb-radio-group`. */
export interface BbRadioOption<TValue = string | number> {
  value: TValue;
  label: string;
  /** Rendered under the label, and pointed at by `aria-describedby`. */
  description?: string;
  disabled?: boolean;
}

/** What `bb-file-input` reports upward. It never uploads anything itself. */
export interface BbFileSelection {
  files: readonly File[];
  /** Files the component refused, with the reason, so the page can say so. */
  rejected: readonly BbRejectedFile[];
}

export interface BbRejectedFile {
  name: string;
  size: number;
  reason: 'type' | 'size' | 'count';
}

/** Common validation state a control hands its wrapper. */
export type BbControlSize = 'default' | 'compact';
