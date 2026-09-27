import { describe, expect, it } from 'vitest';
import { blankForm, formProblem, isValidKey, keyFromLabel, requestOf } from './custom-fields-settings.model';

describe('custom fields settings', () => {
  it('suggests a key from a label', () => {
    expect(keyFromLabel('Site reference #')).toBe('site_reference');
    expect(keyFromLabel('  GST Portal ID ')).toBe('gst_portal_id');
    expect(keyFromLabel('2nd phone')).toBe('f_2nd_phone');
    expect(keyFromLabel('###')).toBe('');
  });

  it('checks keys the way the server does', () => {
    expect(isValidKey('region')).toBe(true);
    expect(isValidKey('site_ref_2')).toBe(true);
    expect(isValidKey('Region')).toBe(false);
    expect(isValidKey('1region')).toBe(false);
    expect(isValidKey('a'.repeat(41))).toBe(false);
  });

  it('needs an active option on a dropdown, each with a value and label, none twice', () => {
    const form = { ...blankForm('Contact', 0), label: 'Region', key: 'region', dataType: 'Dropdown' as const };
    expect(formProblem(form)).toContain('active option');
    form.options = [
      { value: 'north', label: 'North', active: true, saved: false },
      { value: 'north', label: 'North again', active: true, saved: false },
    ];
    expect(formProblem(form)).toContain('same value');
    form.options[1].value = 'south';
    expect(formProblem(form)).toBeNull();
  });

  it('sends options only for choice fields and a length only for text', () => {
    const text = { ...blankForm('Item', 3), label: 'Shelf', key: 'shelf', maxLength: 10 };
    expect(requestOf(text)).toMatchObject({ maxLength: 10, options: [], displayOrder: 3, defaultValue: null });
    const number = { ...text, dataType: 'Integer' as const };
    expect(requestOf(number).maxLength).toBeNull();
  });
});
