import { describe, expect, it, vi } from 'vitest';
import { FxRateFields } from './fx-rate-fields';

/** The revaluation rate fields of the two Foreign Currency Gain or Loss pages (TK-220, TK-221, TK-262). */

const branchDate = (iso: string): string => iso.split('-').reverse().join('/');

function setup(rates: Record<string, number | null> = { USD: 84.5 }) {
  const lookup = vi.fn(async (currency: string): Promise<number | null> => rates[currency] ?? null);
  return { fields: new FxRateFields(lookup, branchDate), lookup };
}

describe('FxRateFields', () => {
  it('FR-01: gives each currency a field, keeps the ones it has and takes away the ones no longer listed', () => {
    const { fields } = setup();

    fields.sync(['USD', 'EUR']);
    fields.controls.controls['USD'].setValue(84);
    fields.sync(['USD', 'GBP']);

    expect(Object.keys(fields.controls.controls)).toEqual(['USD', 'GBP']);
    expect(fields.controls.controls['USD'].value).toBe(84);
  });

  it('FR-02: starts each field from the rate master’s rate and says so in the branch’s date format', async () => {
    const { fields, lookup } = setup({ USD: 84.5 });
    fields.sync(['USD', 'EUR']);

    await fields.startFromMaster(['USD', 'EUR'], 'INR', '2026-10-31', () => true);

    expect(lookup).toHaveBeenCalledWith('USD', 'INR', '2026-10-31');
    expect(fields.controls.controls['USD'].value).toBe(84.5);
    expect(fields.hint('USD')).toBe('The rate master\'s rate for 31/10/2026.');
    expect(fields.controls.controls['EUR'].value).toBeNull();
    expect(fields.hint('EUR')).toBe('No rate on file for 31/10/2026: enter one.');
  });

  it('FR-03: keeps a rate the person has typed and says it is theirs', async () => {
    const { fields } = setup({ USD: 84.5 });
    fields.sync(['USD']);

    const control = fields.controls.controls['USD'];
    control.setValue(90);
    control.markAsDirty();

    await fields.startFromMaster(['USD'], 'INR', '2026-10-31', () => true);

    expect(control.value).toBe(90);
    expect(fields.hint('USD')).toBe('The rate you entered.');
  });

  it('FR-04: drops an answer that is no longer wanted, and a lookup that fails is no rate', async () => {
    const { fields } = setup({ USD: 84.5 });
    fields.sync(['USD']);

    await fields.startFromMaster(['USD'], 'INR', '2026-10-31', () => false);
    expect(fields.controls.controls['USD'].value).toBeNull();
    expect(fields.hint('USD')).toBe('');

    const broken = new FxRateFields(async () => { throw new Error('offline'); }, branchDate);
    broken.sync(['USD']);
    await broken.startFromMaster(['USD'], 'INR', '2026-10-31', () => true);
    expect(broken.hint('USD')).toBe('No rate on file for 31/10/2026: enter one.');
  });

  it('FR-05: sends only a rate above nothing, by upper-case code', () => {
    const { fields } = setup();
    fields.sync(['USD', 'EUR']);
    fields.controls.controls['USD'].setValue(84);
    fields.controls.controls['EUR'].setValue(0);

    expect(fields.values()).toEqual({ USD: 84 });
  });
});
