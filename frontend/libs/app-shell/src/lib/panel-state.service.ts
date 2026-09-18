import { Injectable, computed, signal } from '@angular/core';

const WIDTH_KEY = 'billbook.subpanel.width.v1';
const COLLAPSED_KEY = 'billbook.subpanel.collapsed.v1';

/** The design resizes the panel between these; anything else is a dragging bug. */
export const SUBPANEL_MIN_WIDTH = 196;
export const SUBPANEL_MAX_WIDTH = 430;
export const SUBPANEL_DEFAULT_WIDTH = 250;

/**
 * The secondary menu's own state: how wide it is, whether it is showing, and which
 * of its sections is open.
 *
 * Width and collapsed state are this browser's and survive a reload, because a
 * panel that forgets how wide you made it is a panel you resize every morning. The
 * open section is not persisted — it follows wherever you are now.
 */
@Injectable({ providedIn: 'root' })
export class ShellPanelService {
  readonly width = signal(readWidth());

  readonly collapsed = signal(readCollapsed());

  /**
   * The open section, by group code. One at a time, as the design has it: these
   * lists are long enough that two open at once means scrolling past one to reach
   * the other. Null means every named section is shut.
   */
  readonly openGroup = signal<string | null>(null);

  readonly widthPx = computed(() => `${this.width()}px`);

  toggle(): void {
    this.collapsed.update((v) => !v);
    write(COLLAPSED_KEY, JSON.stringify(this.collapsed()));
  }

  show(): void {
    this.collapsed.set(false);
    write(COLLAPSED_KEY, 'false');
  }

  /** Clamped, because a panel dragged to 3px cannot be dragged back. */
  setWidth(px: number): void {
    const clamped = Math.min(SUBPANEL_MAX_WIDTH, Math.max(SUBPANEL_MIN_WIDTH, Math.round(px)));
    this.width.set(clamped);
    write(WIDTH_KEY, String(clamped));
  }

  toggleGroup(code: string): void {
    this.openGroup.update((open) => (open === code ? null : code));
  }

  /** Whether a section should render its rows. Unnamed sections are always open. */
  isGroupOpen(code: string, hasName: boolean): boolean {
    return !hasName || this.openGroup() === code;
  }
}

function readWidth(): number {
  try {
    const raw = Number(localStorage.getItem(WIDTH_KEY));
    if (!Number.isFinite(raw) || raw <= 0) return SUBPANEL_DEFAULT_WIDTH;
    return Math.min(SUBPANEL_MAX_WIDTH, Math.max(SUBPANEL_MIN_WIDTH, Math.round(raw)));
  } catch {
    return SUBPANEL_DEFAULT_WIDTH;
  }
}

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // A private window, or storage the browser has blocked. The panel still works
    // for this session; it just will not remember across a reload.
  }
}
