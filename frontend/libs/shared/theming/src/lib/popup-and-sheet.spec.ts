import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * How a form opens (TK-269). The global `.sheet` is a full-area editor, and a page that also carried the
 * older scoped copy of it (`margin-top: 1.25rem`, `max-width: 46rem`) got a half-width panel pinned to the
 * top left, over the page heading, with nothing dimmed. These read the stylesheets as text, as the design
 * token spec does, because the guard is on what is written and a template cannot be compiled here.
 */
describe('Popups and full-page sheets (TK-269)', () => {
  const themingDir = __dirname;
  const libsDir = resolve(__dirname, '../../../..');
  const dialog = readFileSync(resolve(themingDir, '_dialog.scss'), 'utf-8');
  const cards = readFileSync(resolve(themingDir, '_cards.scss'), 'utf-8');

  /** The phone block of the dialog stylesheet, from its media query to the end of the file. */
  const phone = dialog.slice(dialog.indexOf('@media (max-width: 640px)'));

  /** Every `.scss` under `frontend/libs`, as a path from there. */
  function stylesheets(dir: string): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      if (entry.name === 'node_modules' || entry.name.startsWith('.')) {
        return [];
      }
      const path = join(dir, entry.name);
      return entry.isDirectory() ? stylesheets(path) : entry.name.endsWith('.scss') ? [path] : [];
    });
  }

  const offenders = (stale: RegExp): string[] =>
    stylesheets(libsDir)
      .filter((path) => stale.test(readFileSync(path, 'utf-8')))
      .map((path) => relative(libsDir, path));

  it('PSH-01: the small and wide popups are full-screen on a phone', () => {
    // Their width rules sit on `.dialog.dialog-wide`, which outranks the phone rule on `.dialog`,
    // so they stayed 328px wide with a strip of the dimmed page beside them.
    expect(phone).toMatch(/\.dialog\.dialog-wide,[^{}]*\.dialog\.dialog-sm\s*\{[^}]*width:\s*100vw;/);
  });

  it('PSH-02: a long form\'s popup keeps its header and actions in place while the body scrolls', () => {
    expect(dialog).toMatch(/\.dialog\.dialog-scroll\s*\{[^}]*overflow:\s*hidden;/);
    expect(dialog).toMatch(/\.dialog-body\s*\{[^}]*overflow-y:\s*auto;/);
    expect(dialog).toMatch(/\.dialog-actions\s*\{[^}]*flex:\s*none;/);
  });

  it('PSH-03: that popup is a full-screen sheet on a phone as well', () => {
    expect(phone).toMatch(/\.dialog\.dialog-scroll\s*\{[^}]*height:\s*100dvh;/);
  });

  it('PSH-04: a full-page editor has its header and actions from the theme, not from a copy on each page', () => {
    expect(cards).toMatch(/\.sheet-header\s*\{[^}]*justify-content:\s*space-between;/);
    expect(cards).toMatch(/\.sheet-header\s*\{[\s\S]*?\.sheet-actions\s*\{[^}]*margin-top:\s*0;/);
  });

  it('PSH-07: the shell clips, rather than hides, the content area while a full-page sheet is open', () => {
    // A hidden scroller keeps the scroll position the list had, so on a phone a sheet opened from a row far
    // down a list came up scrolled away, with its header and Close above the screen. `clip` drops it.
    const shell = readFileSync(resolve(libsDir, 'app-shell/src/lib/shell/shell.component.scss'), 'utf-8');

    expect(shell).toMatch(/&:has\(\.sheet\)\s*\{[^}]*overflow:\s*clip;/);
  });

  it('PSH-05: no stylesheet carries the scoped 46rem panel that pinned a form to the top left', () => {
    expect(offenders(/^\s*\.sheet\s*\{[^}]*max-width:\s*46rem/m)).toEqual([]);
  });

  it('PSH-06: and none carries the one-line rule the card was found by', () => {
    expect(offenders(/^\.sheet \{ margin-top: 1\.25rem/m)).toEqual([]);
  });
});
