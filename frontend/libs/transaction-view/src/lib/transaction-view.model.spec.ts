import { describe, expect, it } from 'vitest';
import {
  DESK_COLOUR,
  MAX_ZOOM,
  MIN_ZOOM,
  clampZoom,
  documentHeading,
  fitScale,
  isEditableStatus,
  nextZoom,
  statusLabel,
  statusNotice,
  statusTone,
  templateNote,
  viewerDocument,
  zoomPercent,
} from './transaction-view.model';

describe('viewerDocument', () => {
  const printed = '<style>@page{size:210mm 297mm;margin:0}</style><div class="pt-doc"><section class="pt-page">a</section></div>';

  it('TXV-10: the document the template produced is inside the viewer page, unchanged', () => {
    const page = viewerDocument(printed);

    expect(page).toContain(printed);
    expect(page.startsWith('<!doctype html>')).toBe(true);
    expect(page).toContain('<meta charset="utf-8">');
  });

  it('TXV-11: the pages lie on a grey desk with a gap and a shadow between them, for the screen only', () => {
    const page = viewerDocument(printed);

    expect(page).toContain(`html{background:${DESK_COLOUR}}`);
    expect(page).toContain('.pt-page+.pt-page{margin-top:16px}');
    expect(page).toContain('box-shadow');
  });

  it('TXV-12: printing takes the desk, the gap and the shadow away so the pages meet exactly', () => {
    const page = viewerDocument(printed);
    const print = page.slice(page.indexOf('@media print'));

    expect(print).toContain('html{background:#fff}');
    expect(print).toContain('.pt-page+.pt-page{margin-top:0}');
    expect(print).toContain('box-shadow:none');
  });

  it('TXV-13: the viewer adds nothing that could run', () => {
    const page = viewerDocument(printed);

    expect(page).not.toContain('<script');
  });
});

describe('fitScale', () => {
  it('TXV-14: a page wider than the room is scaled down to the room', () => {
    expect(fitScale(397, 794)).toBeCloseTo(0.5, 5);
    expect(fitScale(330, 794)).toBeCloseTo(0.4156, 3);
  });

  it('TXV-15: a page that fits is shown at its own size, never blown up', () => {
    expect(fitScale(1600, 794)).toBe(1);
    expect(fitScale(794, 794)).toBe(1);
  });

  it('TXV-16: a room or a page not measured yet gives life size rather than nothing', () => {
    expect(fitScale(0, 794)).toBe(1);
    expect(fitScale(500, 0)).toBe(1);
    expect(fitScale(Number.NaN, 794)).toBe(1);
  });

  it('TXV-17: it never goes below the smallest scale the viewer allows', () => {
    expect(fitScale(50, 794)).toBe(MIN_ZOOM);
  });
});

describe('zoom', () => {
  it('TXV-18: plus and minus step through the usual levels and stop at the ends', () => {
    expect(nextZoom(1, 1)).toBe(1.25);
    expect(nextZoom(1, -1)).toBe(0.75);
    expect(nextZoom(MAX_ZOOM, 1)).toBe(MAX_ZOOM);
    expect(nextZoom(MIN_ZOOM, -1)).toBe(MIN_ZOOM);
  });

  it('TXV-19: from a fitted scale between two levels, the next step is the one beyond it', () => {
    expect(nextZoom(0.4156, 1)).toBe(0.5);
    expect(nextZoom(0.4156, -1)).toBe(0.25);
    expect(nextZoom(0.9, 1)).toBe(1);
    expect(nextZoom(0.9, -1)).toBe(0.75);
  });

  it('TXV-20: a chosen scale is kept between the smallest and the largest', () => {
    expect(clampZoom(0.01)).toBe(MIN_ZOOM);
    expect(clampZoom(9)).toBe(MAX_ZOOM);
    expect(clampZoom(1.5)).toBe(1.5);
  });

  it('TXV-21: the scale reads as a rounded percentage', () => {
    expect(zoomPercent(1)).toBe(100);
    expect(zoomPercent(0.755)).toBe(76);
    expect(zoomPercent(0.4156)).toBe(42);
  });
});

describe('status', () => {
  it('TXV-22: only a draft or one waiting to be posted can still be edited', () => {
    expect(isEditableStatus('Draft')).toBe(true);
    expect(isEditableStatus('ReadyToPost')).toBe(true);
    expect(isEditableStatus('Posted')).toBe(false);
    expect(isEditableStatus('Void')).toBe(false);
    expect(isEditableStatus('Reversed')).toBe(false);
    expect(isEditableStatus(null)).toBe(false);
  });

  it('TXV-23: a status is worded for a person and coloured the way every screen colours it', () => {
    expect(statusLabel('ReadyToPost')).toBe('Ready to post');
    expect(statusLabel('Posted')).toBe('Posted');
    expect(statusLabel(null)).toBe('');

    expect(statusTone('Posted')).toBe('tag-success');
    expect(statusTone('Void')).toBe('tag-danger');
    expect(statusTone('Draft')).toBe('tag-warning');
    expect(statusTone('Reversed')).toBe('tag-warning');
    expect(statusTone(null)).toBe('tag-info');
  });

  it('TXV-24: a document that is not final says so in words, and a posted one says nothing', () => {
    expect(statusNotice('quote', 'Draft')).toBe('This quote is a draft. It is stamped so, and is not final until it is posted.');
    expect(statusNotice('bill', 'ReadyToPost')).toContain('This bill is a draft');
    expect(statusNotice('invoice', 'Void')).toBe('This invoice is void.');
    expect(statusNotice('journal voucher', 'Reversed')).toBe('This journal voucher has been reversed by another entry.');
    expect(statusNotice('invoice', 'Posted')).toBeNull();
    expect(statusNotice('invoice', null)).toBeNull();
  });

  it('TXV-25: the heading names the document and its number, or says it has none yet', () => {
    expect(documentHeading('goods receipt', 'GRN-0007')).toBe('Goods receipt GRN-0007');
    expect(documentHeading('receipt', null)).toBe('Receipt (no number yet)');
    expect(documentHeading('journal voucher', '')).toBe('Journal voucher (no number yet)');
  });
});

describe('templateNote (TK-265)', () => {
  it('TXV-40: names the template a document was laid out with, when it is the one the document chose', () => {
    expect(templateNote('invoice', { printTemplateId: 7, printTemplateName: 'Tax invoice · A4', chosenPrintTemplateId: 7 })).toEqual({
      warning: false,
      text: 'Template: Tax invoice · A4.',
    });
  });

  it('TXV-41: says the branch default was used when the document chose none', () => {
    const note = templateNote('bill', { printTemplateId: 3, printTemplateName: 'Bill · A4', chosenPrintTemplateId: null });

    expect(note.warning).toBe(false);
    expect(note.text).toBe('Template: Bill · A4, the branch default. No template was chosen for this bill.');
  });

  it('TXV-42: warns, naming the stand-in, when the template the document chose could not be used', () => {
    const note = templateNote('quote', { printTemplateId: 3, printTemplateName: 'Quote · A4', chosenPrintTemplateId: 9 });

    expect(note.warning).toBe(true);
    expect(note.text).toBe('The template chosen for this quote is no longer available, so it is laid out with “Quote · A4”, the branch default.');
  });

  it('TXV-43: warns that the standard layout was used when no stored template could be found at all', () => {
    expect(templateNote('receipt', { printTemplateId: null, printTemplateName: null, chosenPrintTemplateId: null })).toEqual({
      warning: true,
      text: 'No print template is set up for this kind of document, so this receipt is laid out in the standard layout.',
    });

    expect(templateNote('receipt', { printTemplateId: null, chosenPrintTemplateId: 4 }).text).toContain('is no longer available, and none is set as the default');
  });

  it('TXV-44: tolerates a service that does not send the new fields yet, and still names the layout', () => {
    expect(templateNote('journal voucher', { printTemplateId: 2 }).text).toBe(
      'Template: its template, the branch default. No template was chosen for this journal voucher.',
    );
  });
});
