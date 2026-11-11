import { describe, expect, it } from 'vitest';
import {
  GONE_TEMPLATE_LABEL,
  NO_TEMPLATE_LABEL,
  PrintTemplateOffer,
  paperSizeLabel,
  pickDefaultTemplate,
  printTemplateLabel,
  printTemplateOptions,
} from './print-template-field';

const a4: PrintTemplateOffer = { printTemplateId: 1, templateName: 'Tax invoice', paperSizeName: 'A4', isSample: true, isDefault: true };
const letterhead: PrintTemplateOffer = { printTemplateId: 2, templateName: 'Letterhead', paperSizeName: 'A4', isSample: false, isDefault: false };
const roll: PrintTemplateOffer = {
  printTemplateId: 3,
  templateName: 'Invoice - Thermal 3" (80mm)',
  paperSizeName: 'Thermal3Inch',
  isSample: true,
  isDefault: false,
};

describe('The Print template field (TK-265)', () => {
  it('PTF-01: reads a paper size as a person says it, and leaves an unknown one as it is', () => {
    expect(paperSizeLabel('A4')).toBe('A4');
    expect(paperSizeLabel('Thermal3Inch')).toBe('Thermal 3″ (80 mm)');
    expect(paperSizeLabel('Thermal4Inch')).toBe('Thermal 4″ (104 mm)');
    expect(paperSizeLabel('Foolscap')).toBe('Foolscap');
    expect(paperSizeLabel(null)).toBe('');
    expect(paperSizeLabel(undefined)).toBe('');
  });

  it('PTF-02: labels a choice with its paper size, then Sample, then Default', () => {
    expect(printTemplateLabel(a4)).toBe('Tax invoice · A4 · Sample · Default');
    expect(printTemplateLabel(letterhead)).toBe('Letterhead · A4');
  });

  it('PTF-03: does not repeat a paper size the name already carries', () => {
    expect(printTemplateLabel(roll)).toBe('Invoice - Thermal 3" (80mm) · Sample');
  });

  it('PTF-04: a new document starts on the branch default, else the first choice, else nothing', () => {
    expect(pickDefaultTemplate([letterhead, a4, roll])).toBe(1);
    expect(pickDefaultTemplate([letterhead, roll])).toBe(2);
    expect(pickDefaultTemplate([])).toBeNull();
  });

  it('PTF-05: lists every choice in the order given, keeping the id as a number', () => {
    const options = printTemplateOptions([a4, letterhead], null, true);

    expect(options.map((option) => option.value)).toEqual([1, 2]);
    expect(options[1].label).toBe('Letterhead · A4');
    expect(options.every((option) => !option.disabled)).toBe(true);
  });

  it('PTF-06: keeps a template the document names that is no longer offered, as a disabled row first', () => {
    const options = printTemplateOptions([a4], 99, true);

    expect(options[0]).toEqual({ value: 99, label: GONE_TEMPLATE_LABEL, disabled: true });
    expect(options.slice(1).map((option) => option.value)).toEqual([1]);
  });

  it('PTF-07: says nothing about a saved template until the list has loaded, or when it failed to', () => {
    expect(printTemplateOptions([], 99, false)).toEqual([]);
    expect(printTemplateOptions([a4], 99, false).map((option) => option.value)).toEqual([1]);
  });

  it('PTF-08: adds no gone row when the saved template is offered, or when the document names none', () => {
    expect(printTemplateOptions([a4, letterhead], 2, true).map((option) => option.value)).toEqual([1, 2]);
    expect(printTemplateOptions([a4], null, true).map((option) => option.value)).toEqual([1]);
  });

  it('PTF-09: the empty row says the branch default will be used', () => {
    expect(NO_TEMPLATE_LABEL).toBe('Branch default');
  });
});
