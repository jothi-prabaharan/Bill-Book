import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RichTextInputComponent } from './rich-text-input.component';
import { isRichTextEmpty, richTextToPlain, sanitizeRichText } from './sanitize-html';
import { bbRichTextRequired } from '../validators';

interface RichTextHarness {
  html: () => string;
  isEmpty: () => boolean;
  plainLength: () => number;
  editable: () => boolean;
  regionAriaLabel: () => string;
  buttons: () => { command: string }[];
}

function build<T>(make: () => T): T {
  return TestBed.runInInjectionContext(make);
}

function set(component: unknown, name: string, value: unknown): void {
  (component as Record<string, unknown>)[name] = () => value;
}

describe('Rich text', () => {
  /**
   * The sanitiser is the security boundary of the whole input system: its
   * output is stored, rendered back into other people's screens and printed
   * onto documents. Every one of these is an attack that would otherwise work.
   */
  describe('sanitizeRichText', () => {
    it('SAN-01: keeps the formatting a description field is for', () => {
      const html =
        '<p>Delivered <strong>ex works</strong>, <em>freight prepaid</em>.</p>' +
        '<ul><li>Pallet A</li><li>Pallet B</li></ul>';
      expect(sanitizeRichText(html)).toBe(html);
    });

    it('SAN-02: a script tag goes, contents and all', () => {
      const clean = sanitizeRichText('<p>Terms</p><script>alert(document.cookie)</script>');
      expect(clean).toBe('<p>Terms</p>');
      expect(clean).not.toContain('alert');
    });

    it('SAN-03: an event handler attribute is stripped from a tag that survives', () => {
      // The denylist failure mode: a tag on the allowlist carrying an attribute
      // nobody thought to name.
      const clean = sanitizeRichText('<p onclick="steal()">Terms</p>');
      expect(clean).toBe('<p>Terms</p>');
      expect(clean).not.toContain('onclick');
    });

    it('SAN-04: an image never survives, so no handler on one can fire', () => {
      const clean = sanitizeRichText('<img src="x" onerror="steal()">');
      expect(clean).not.toContain('img');
      expect(clean).not.toContain('onerror');
    });

    it('SAN-05: a javascript: link loses its href and keeps its text', () => {
      const clean = sanitizeRichText('<a href="javascript:alert(1)">Click</a>');
      expect(clean).toContain('Click');
      expect(clean).not.toContain('javascript');
      expect(clean).not.toContain('href');
    });

    it('SAN-06: a scheme obfuscated with control characters is still refused', () => {
      // A tab or newline inside the scheme is ignored by the browser's URL
      // parser, which is how this gets past a check that reads the raw string.
      for (const href of [
        'java\tscript:alert(1)',
        'java\nscript:alert(1)',
        ' javascript:alert(1)',
        'JaVaScRiPt:alert(1)',
      ]) {
        const clean = sanitizeRichText(`<a href="${href}">x</a>`);
        expect(clean).not.toContain('href');
      }
    });

    it('SAN-07: a data: link is refused too', () => {
      const clean = sanitizeRichText(
        '<a href="data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==">x</a>',
      );
      expect(clean).not.toContain('href');
    });

    it('SAN-08: an http link survives and opens safely', () => {
      const clean = sanitizeRichText('<a href="https://acme.co.in">Price list</a>');
      expect(clean).toContain('href="https://acme.co.in"');
      // `noopener` is what stops the opened page reaching back through
      // `window.opener`.
      expect(clean).toContain('rel="noopener noreferrer"');
      expect(clean).toContain('target="_blank"');
    });

    it('SAN-09: a relative link survives; a protocol-relative one does not', () => {
      expect(sanitizeRichText('<a href="/sales/invoices/42">42</a>')).toContain('href="/sales');
      expect(sanitizeRichText('<a href="//evil.example">x</a>')).not.toContain('href');
    });

    it('SAN-10: styles, classes and ids are dropped from everything', () => {
      const clean = sanitizeRichText(
        '<p class="x" id="y" style="position:fixed" data-track="z">Terms</p>',
      );
      expect(clean).toBe('<p>Terms</p>');
    });

    it('SAN-11: an unknown tag is unwrapped, so the text somebody wrote survives', () => {
      // Deleting the subtree would silently swallow content.
      const clean = sanitizeRichText('<span style="color:red">Net 30 days</span>');
      expect(clean).toBe('Net 30 days');

      const table = sanitizeRichText('<table><tr><td>Pallet A</td></tr></table>');
      expect(table).toContain('Pallet A');
      expect(table).not.toContain('<table');
    });

    it('SAN-12: nested and malformed markup is filtered all the way down', () => {
      const clean = sanitizeRichText(
        '<div><p>Ok <b onmouseover="x()">bold<script>bad()</script></b></p></div>',
      );
      expect(clean).toContain('bold');
      expect(clean).not.toContain('onmouseover');
      expect(clean).not.toContain('bad()');
    });

    it('SAN-13: an iframe and a style block go entirely', () => {
      const clean = sanitizeRichText(
        '<iframe src="https://evil.example"></iframe><style>body{display:none}</style><p>Terms</p>',
      );
      expect(clean).toBe('<p>Terms</p>');
    });

    it('SAN-14: comments are not content', () => {
      expect(sanitizeRichText('<p>Terms</p><!-- secret -->')).toBe('<p>Terms</p>');
    });

    it('SAN-15: an empty input gives an empty output', () => {
      expect(sanitizeRichText('')).toBe('');
    });

    it('SAN-16: sanitising twice changes nothing the first pass allowed', () => {
      const once = sanitizeRichText('<p>Net <b>30</b> <span>days</span></p>');
      expect(sanitizeRichText(once)).toBe(once);
    });
  });

  describe('richTextToPlain and isRichTextEmpty', () => {
    it('SAN-20: plain text is the words, without the tags', () => {
      expect(richTextToPlain('<p>Net <b>30</b> days</p>')).toBe('Net 30 days');
    });

    it('SAN-21: what an emptied editor leaves behind counts as empty', () => {
      // `Validators.required` is not enough: `<p><br></p>` is a non-empty
      // string, and it is what the browser leaves when the last character goes.
      expect(isRichTextEmpty('')).toBe(true);
      expect(isRichTextEmpty('<p></p>')).toBe(true);
      expect(isRichTextEmpty('   ')).toBe(true);
      expect(isRichTextEmpty('<p>Terms</p>')).toBe(false);
    });

    it('SAN-22: the required validator uses that rule rather than string length', () => {
      const validate = bbRichTextRequired();
      expect(validate(new FormControl('<p></p>'))).toEqual({ required: true });
      expect(validate(new FormControl('<p>Terms</p>'))).toBeNull();
    });
  });

  describe('RichTextInputComponent', () => {
    beforeEach(() => {
      TestBed.configureTestingModule({});
    });

    it('RTX-01: a value written in is filtered before it is ever held', () => {
      const component = build(() => new RichTextInputComponent());
      const harness = component as unknown as RichTextHarness;

      component.writeValue('<p onclick="x()">Terms</p><script>bad()</script>');
      expect(harness.html()).toBe('<p>Terms</p>');
    });

    it('RTX-02: null and undefined read as empty', () => {
      const component = build(() => new RichTextInputComponent());
      const harness = component as unknown as RichTextHarness;

      component.writeValue(null);
      expect(harness.html()).toBe('');
      expect(harness.isEmpty()).toBe(true);
    });

    it('RTX-03: readonly and disabled both render instead of editing', () => {
      // `contenteditable` has no readonly, so the alternative to rendering is a
      // field that looks locked and is not.
      const readOnly = build(() => {
        const made = new RichTextInputComponent();
        set(made, 'readonly', true);
        return made;
      });
      expect((readOnly as unknown as RichTextHarness).editable()).toBe(false);

      const plain = build(() => new RichTextInputComponent());
      expect((plain as unknown as RichTextHarness).editable()).toBe(true);
      plain.setDisabledState(true);
      expect((plain as unknown as RichTextHarness).editable()).toBe(false);
    });

    it('RTX-04: the region carries its own name, since a label cannot address it', () => {
      const component = build(() => {
        const made = new RichTextInputComponent();
        set(made, 'label', 'Description');
        return made;
      });
      expect((component as unknown as RichTextHarness).regionAriaLabel()).toBe('Description');
    });

    it('RTX-05: the toolbar offers only the commands asked for', () => {
      const all = build(() => new RichTextInputComponent());
      expect((all as unknown as RichTextHarness).buttons()).toHaveLength(6);

      const narrow = build(() => {
        const made = new RichTextInputComponent();
        set(made, 'toolbar', ['bold', 'italic']);
        return made;
      });
      expect(
        (narrow as unknown as RichTextHarness).buttons().map((button) => button.command),
      ).toEqual(['bold', 'italic']);
    });

    it('RTX-06: the character count is of the text, not of the markup', () => {
      const component = build(() => new RichTextInputComponent());
      component.writeValue('<p>Net <b>30</b> days</p>');
      expect((component as unknown as RichTextHarness).plainLength()).toBe('Net 30 days'.length);
    });

    it('RTX-07: the value never reaches the console', () => {
      const logged = vi.spyOn(console, 'log').mockImplementation(() => undefined);
      const component = build(() => new RichTextInputComponent());
      component.writeValue('<p>Confidential terms</p>');
      expect(logged).not.toHaveBeenCalled();
      logged.mockRestore();
    });
  });
});
