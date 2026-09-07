import { TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { EmailInputComponent } from './email-input.component';
import { PasswordInputComponent } from './password-input.component';
import { PhoneInputComponent } from './phone-input.component';
import { UrlInputComponent } from './url-input.component';
import { bbEmail, bbPhone, bbUrl } from '../validators';

interface TextHarness {
  innerValue: () => string;
  resolvedType: () => string;
  resolvedInputmode: () => string | null;
  resolvedAutocomplete: () => string;
  resolvedPattern: () => string | null;
  resolvedPrefix: () => string;
  effectiveAriaLabel: () => string | null;
  effectiveDisabled: () => boolean;
  describedBy: () => string | null;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
}

interface PasswordHarness extends TextHarness {
  revealed: () => boolean;
  fieldType: () => string;
  toggleLabel: () => string;
  toggleReveal: () => void;
}

function build<T>(make: () => T): T {
  return TestBed.runInInjectionContext(make);
}

function type(harness: TextHarness, text: string): void {
  harness.onInput({ target: { value: text } } as unknown as Event);
}

function set(component: unknown, name: string, value: unknown): void {
  (component as Record<string, unknown>)[name] = () => value;
}

describe('Text inputs', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  describe('EmailInputComponent', () => {
    it('EML-01: renders as an email field asking for an email keyboard', () => {
      const component = build(() => new EmailInputComponent());
      const harness = component as unknown as TextHarness;

      expect(harness.resolvedType()).toBe('email');
      expect(harness.resolvedInputmode()).toBe('email');
      expect(harness.resolvedAutocomplete()).toBe('email');
      expect(harness.resolvedPattern()).not.toBeNull();
    });

    it('EML-02: a valid address reaches the form', () => {
      const component = build(() => new EmailInputComponent());
      const harness = component as unknown as TextHarness;
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, 'ravi@acme.co.in');
      expect(changed).toHaveBeenCalledWith('ravi@acme.co.in');
    });

    it('EML-03: the Angular validator and the rendered pattern agree', () => {
      // Two expressions for one rule is how a form goes green while the API
      // refuses it, so the validator imports the component's own pattern.
      const validate = bbEmail();

      expect(validate(new FormControl('ravi@acme.co.in'))).toBeNull();
      expect(validate(new FormControl('ravi@acme.co'))).toBeNull();

      expect(validate(new FormControl('ravi'))).toEqual({ bbEmail: true });
      expect(validate(new FormControl('ravi@acme'))).toEqual({ bbEmail: true });
      expect(validate(new FormControl('ravi acme@x.com'))).toEqual({ bbEmail: true });
      expect(validate(new FormControl('@acme.com'))).toEqual({ bbEmail: true });
    });

    it('EML-04: an empty optional field is valid; requiredness is stated separately', () => {
      const validate = bbEmail();
      expect(validate(new FormControl(''))).toBeNull();
      expect(validate(new FormControl(null))).toBeNull();

      const control = new FormControl('', [Validators.required, bbEmail()]);
      expect(control.hasError('required')).toBe(true);
      expect(control.hasError('bbEmail')).toBe(false);
    });
  });

  describe('PhoneInputComponent', () => {
    it('PHN-01: a telephone field with a telephone keyboard', () => {
      const component = build(() => new PhoneInputComponent());
      const harness = component as unknown as TextHarness;

      expect(harness.resolvedType()).toBe('tel');
      expect(harness.resolvedInputmode()).toBe('tel');
      expect(harness.resolvedAutocomplete()).toBe('tel');
    });

    it('PHN-02: a mobile number carries no pattern at all', () => {
      // Lengths vary too much by country for an expression to be anything but
      // a source of false rejections — the rule `MobileAttribute` states.
      const component = build(() => new PhoneInputComponent());
      expect((component as unknown as TextHarness).resolvedPattern()).toBeNull();
    });

    it('PHN-03: a landline carries the same expression as LandlineAttribute', () => {
      const component = build(() => {
        const made = new PhoneInputComponent();
        set(made, 'kind', 'landline');
        return made;
      });
      expect((component as unknown as TextHarness).resolvedPattern()).not.toBeNull();

      const validate = bbPhone('landline');
      expect(validate(new FormControl('044 28123456'))).toBeNull();
      expect(validate(new FormControl('044-28123456'))).toBeNull();
      expect(validate(new FormControl('+442012345678'))).toBeNull();
      expect(validate(new FormControl('4'))).toEqual({ bbPhone: true });
      expect(validate(new FormControl('abcd'))).toEqual({ bbPhone: true });
    });

    it('PHN-04: a mobile number is checked only for length', () => {
      const validate = bbPhone('mobile');
      expect(validate(new FormControl('9876543210'))).toBeNull();
      expect(validate(new FormControl('+14155552671'))).toBeNull();
      expect(validate(new FormControl(''))).toBeNull();
      expect(validate(new FormControl('9'.repeat(21)))).toEqual({
        bbPhone: { maxLength: 20 },
      });
    });

    it('PHN-05: the dialling code is shown, never added to the value', () => {
      // Storing it would make every local number look foreign, and the leading
      // `+` is what distinguishes the two.
      const component = build(() => {
        const made = new PhoneInputComponent();
        set(made, 'countryCode', '+91');
        return made;
      });
      const harness = component as unknown as TextHarness;
      const changed = vi.fn();
      component.registerOnChange(changed);

      expect(harness.resolvedPrefix()).toBe('+91');

      type(harness, '9876543210');
      expect(changed).toHaveBeenCalledWith('9876543210');
    });
  });

  describe('UrlInputComponent', () => {
    it('URL-01: renders as a URL field with a URL keyboard', () => {
      const component = build(() => new UrlInputComponent());
      const harness = component as unknown as TextHarness;

      expect(harness.resolvedType()).toBe('url');
      expect(harness.resolvedInputmode()).toBe('url');
      expect(harness.resolvedAutocomplete()).toBe('url');
    });

    it('URL-02: a scheme is required, and only http or https will do', () => {
      const validate = bbUrl();

      expect(validate(new FormControl('https://acme.co.in'))).toBeNull();
      expect(validate(new FormControl('http://acme.co.in/price-list'))).toBeNull();

      // A bare host stored and put into an href is read as a relative path, so
      // the link lands on our own domain and quietly goes nowhere.
      expect(validate(new FormControl('acme.co.in'))).toEqual({ bbUrl: true });
      expect(validate(new FormControl('javascript:alert(1)'))).toEqual({ bbUrl: true });
      expect(validate(new FormControl('ftp://acme.co.in'))).toEqual({ bbUrl: true });
    });

    it('URL-03: an empty optional field is valid', () => {
      expect(bbUrl()(new FormControl(''))).toBeNull();
    });
  });

  describe('PasswordInputComponent', () => {
    it('PWD-01: masked until the toggle is pressed, and masked again after', () => {
      const component = build(() => new PasswordInputComponent());
      const harness = component as unknown as PasswordHarness;

      expect(harness.fieldType()).toBe('password');
      expect(harness.revealed()).toBe(false);

      harness.toggleReveal();
      expect(harness.fieldType()).toBe('text');
      expect(harness.revealed()).toBe(true);

      harness.toggleReveal();
      expect(harness.fieldType()).toBe('password');
    });

    it('PWD-02: the toggle names what pressing it will do', () => {
      // An icon-only control needs an accessible name, and the name has to be
      // the action rather than the current state.
      const component = build(() => new PasswordInputComponent());
      const harness = component as unknown as PasswordHarness;

      expect(harness.toggleLabel()).toBe('Show password');
      harness.toggleReveal();
      expect(harness.toggleLabel()).toBe('Hide password');
    });

    it('PWD-03: a disabled field cannot be revealed', () => {
      const component = build(() => new PasswordInputComponent());
      const harness = component as unknown as PasswordHarness;

      component.setDisabledState(true);
      harness.toggleReveal();
      expect(harness.revealed()).toBe(false);
    });

    it('PWD-04: the purpose picks the autocomplete token a manager reads', () => {
      const current = build(() => new PasswordInputComponent());
      expect((current as unknown as TextHarness).resolvedAutocomplete()).toBe('current-password');

      const fresh = build(() => {
        const made = new PasswordInputComponent();
        set(made, 'purpose', 'new');
        return made;
      });
      expect((fresh as unknown as TextHarness).resolvedAutocomplete()).toBe('new-password');

      const off = build(() => {
        const made = new PasswordInputComponent();
        set(made, 'purpose', 'off');
        return made;
      });
      expect((off as unknown as TextHarness).resolvedAutocomplete()).toBe('off');
    });

    it('PWD-05: the value reaches the form and nothing else', () => {
      // Anything logging or emitting a password elsewhere would show up as an
      // extra call here.
      const component = build(() => new PasswordInputComponent());
      const harness = component as unknown as PasswordHarness;
      const changed = vi.fn();
      const emitted = vi.fn();
      const logged = vi.spyOn(console, 'log').mockImplementation(() => undefined);

      component.registerOnChange(changed);
      component.valueChange.subscribe(emitted);

      type(harness, 'correct horse battery staple');

      expect(changed).toHaveBeenCalledExactlyOnceWith('correct horse battery staple');
      expect(emitted).toHaveBeenCalledExactlyOnceWith('correct horse battery staple');
      expect(logged).not.toHaveBeenCalled();
      logged.mockRestore();
    });
  });

  describe('shared behaviour', () => {
    it('TXTBASE-01: value binding, maxlength, readonly and disabled', () => {
      const component = build(() => {
        const made = new EmailInputComponent();
        set(made, 'maxlength', 100);
        set(made, 'readonly', true);
        return made;
      });
      const harness = component as unknown as TextHarness;

      component.writeValue('ravi@acme.co.in');
      expect(harness.innerValue()).toBe('ravi@acme.co.in');
      expect(component.maxlength()).toBe(100);
      expect(component.readonly()).toBe(true);

      component.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);
    });

    it('TXTBASE-02: null and undefined read as an empty field, not "null"', () => {
      const component = build(() => new EmailInputComponent());
      const harness = component as unknown as TextHarness;

      component.writeValue(null);
      expect(harness.innerValue()).toBe('');
      component.writeValue(undefined);
      expect(harness.innerValue()).toBe('');
    });

    it('TXTBASE-03: an error describes the field and replaces the hint', () => {
      const component = build(() => {
        const made = new EmailInputComponent();
        set(made, 'id', 'user-email');
        set(made, 'hint', 'The invitation goes here.');
        set(made, 'error', 'That is not an email address.');
        return made;
      });
      expect((component as unknown as TextHarness).describedBy()).toBe('user-email-error');
    });

    it('TXTBASE-04: blur marks the control touched', () => {
      const component = build(() => new EmailInputComponent());
      const harness = component as unknown as TextHarness;
      const touched = vi.fn();

      component.registerOnTouched(touched);
      harness.onBlur(new FocusEvent('blur'));
      expect(touched).toHaveBeenCalledTimes(1);
    });
  });
});
