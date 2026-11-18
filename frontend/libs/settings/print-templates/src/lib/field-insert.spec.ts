import { afterEach, describe, expect, it } from 'vitest';
import {
  bodyCellBelow,
  buildChip,
  cellPosition,
  headingLabel,
  insertChipAt,
  isBlankCell,
  isHeadingCell,
  isInView,
  nameHeading,
  pickerPlacement,
  planFieldInsert,
} from './field-insert';

/** A band holding the standard items table: headings in a thead, one body row, and a footer-style two-column row. */
function band(html: string): HTMLDivElement {
  const host = document.createElement('div');
  host.setAttribute('contenteditable', 'true');
  host.innerHTML = html;
  document.body.append(host);
  return host;
}

const ITEMS = `
  <table>
    <thead><tr><th id="h1">#</th><th id="h2"></th><th id="h3" colspan="2">Amount</th></tr></thead>
    <tbody><tr><td id="b1"><br></td><td id="b2"></td><td id="b3">x</td><td id="b4"></td></tr></tbody>
  </table>`;

const cell = (id: string) => document.getElementById(id) as HTMLTableCellElement;

describe('The print-template editor field insertion (TK-267)', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('FINS-01: a heading cell is a th or any cell in a thead, and a body cell is neither', () => {
    band(ITEMS);
    expect(isHeadingCell(cell('h1'))).toBe(true);
    expect(isHeadingCell(cell('b1'))).toBe(false);

    const plain = band('<table><tr><td id="p1">a</td></tr></table>');
    expect(isHeadingCell(plain.querySelector('td') as HTMLTableCellElement)).toBe(false);
  });

  it('FINS-02: the cell below a heading is the same column of the first body row, counting column spans', () => {
    band(ITEMS);
    expect(bodyCellBelow(cell('h1'))?.id).toBe('b1');
    expect(bodyCellBelow(cell('h2'))?.id).toBe('b2');
    // "Amount" spans columns 3 and 4: the body cell it starts above is the third.
    expect(bodyCellBelow(cell('h3'))?.id).toBe('b3');
  });

  it('FINS-03: there is no cell below when the table has no body row', () => {
    const lone = band('<table><thead><tr><th id="only">A</th></tr></thead></table>');
    expect(bodyCellBelow(lone.querySelector('#only') as HTMLTableCellElement)).toBeNull();
  });

  it('FINS-04: a field that repeats, picked for a heading, goes in the column below and names the heading', () => {
    band(ITEMS);
    const plan = planFieldInsert(cell('h2'), true);

    expect(plan.chipCell?.id).toBe('b2');
    expect(plan.labelCell?.id).toBe('h2');
  });

  it('FINS-05: any other field goes in the cell that was clicked', () => {
    band(ITEMS);
    expect(planFieldInsert(cell('h2'), false)).toEqual({ chipCell: cell('h2'), labelCell: null });
    expect(planFieldInsert(cell('b3'), true)).toEqual({ chipCell: cell('b3'), labelCell: null });
    expect(planFieldInsert(null, true)).toEqual({ chipCell: null, labelCell: null });
  });

  it('FINS-06: a heading with no column below it takes the field itself', () => {
    const lone = band('<table><thead><tr><th id="only">A</th></tr></thead></table>');
    const only = lone.querySelector('#only') as HTMLTableCellElement;

    expect(planFieldInsert(only, true)).toEqual({ chipCell: only, labelCell: null });
  });

  it('FINS-07: a heading says the fields description when it is short, and its readable name when it is not', () => {
    expect(headingLabel({ tag: 'Item.ItemName', description: 'Item name' })).toBe('Item name');
    expect(headingLabel({ tag: 'Item.ItemName', description: 'The name of the item as it is on the item master, with its variant. More.' })).toBe(
      'Item name',
    );
    expect(headingLabel({ tag: 'Item.HsnSac' })).toBe('Hsn sac');
    expect(headingLabel({ tag: 'Totals.grand_total', description: '' })).toBe('Grand total');
  });

  it('FINS-08: a cell is blank when it has no text, field, image or table in it', () => {
    band(ITEMS);
    expect(isBlankCell(cell('b1'))).toBe(true);
    expect(isBlankCell(cell('b3'))).toBe(false);

    const withChip = band('<table><tr><td id="c"><span class="pt-chip">{{A.B}}</span></td></tr></table>');
    expect(isBlankCell(withChip.querySelector('#c') as HTMLElement)).toBe(false);

    const spaces = band('<table><tr><td id="s">&nbsp; </td></tr></table>');
    expect(isBlankCell(spaces.querySelector('#s') as HTMLElement)).toBe(true);
  });

  it('FINS-09: says where a cell is in words', () => {
    band(ITEMS);
    expect(cellPosition(cell('h1'))).toBe('row 1, column 1');
    expect(cellPosition(cell('h3'))).toBe('row 1, column 3');
    expect(cellPosition(cell('b4'))).toBe('row 2, column 4');
  });

  it('FINS-10: builds a chip the way the editor always has, and a field that repeats says so', () => {
    const one = buildChip(document, 'Document.No', false);
    const holder = document.createElement('div');
    holder.append(one.fragment);

    const chip = holder.querySelector('span') as HTMLElement;
    expect(chip.getAttribute('contenteditable')).toBe('false');
    expect(chip.className).toBe('pt-chip');
    expect(chip.textContent).toBe('{{Document.No}}');
    expect(holder.innerHTML.endsWith('&nbsp;')).toBe(true);

    const many = document.createElement('div');
    many.append(buildChip(document, 'Item.ItemName', true).fragment);
    expect((many.querySelector('span') as HTMLElement).className).toBe('pt-chip pt-chip--list');
    expect(many.innerHTML).toContain('&lt;&lt;Item.ItemName&gt;&gt;');
  });

  it('FINS-11: puts the chip in an empty cell in place of its placeholder line break', () => {
    const host = band(ITEMS);
    insertChipAt(host, cell('b1'), null, 'Item.SlNo', true);

    expect(cell('b1').querySelector('br')).toBeNull();
    expect(cell('b1').querySelectorAll('.pt-chip')).toHaveLength(1);
    expect(cell('b1').textContent).toBe('<<Item.SlNo>>\u00a0');
  });

  it('FINS-12: puts the chip at the end of a cell that has text, when there is no caret to follow', () => {
    const host = band(ITEMS);
    insertChipAt(host, cell('b3'), null, 'Document.No', false);

    expect(cell('b3').firstChild?.textContent).toBe('x');
    expect((cell('b3').children[0] as HTMLElement).textContent).toBe('{{Document.No}}');
  });

  it('FINS-13: puts the chip where the click left the caret, when that is inside the cell', () => {
    const host = band('<table><tr><td id="t">before after</td></tr></table>');
    const text = (host.querySelector('#t') as HTMLElement).firstChild as Text;
    const caret = document.createRange();
    caret.setStart(text, 7);
    caret.collapse(true);

    insertChipAt(host, host.querySelector('#t') as HTMLElement, caret, 'Party.Name', false);

    expect((host.querySelector('#t') as HTMLElement).textContent).toBe('before {{Party.Name}}\u00a0after');
  });

  it('FINS-14: ignores a caret that is in another cell, so the field goes in the cell that was clicked', () => {
    const host = band('<table><tr><td id="a">one</td><td id="b">two</td></tr></table>');
    const inA = document.createRange();
    inA.setStart(((host.querySelector('#a') as HTMLElement).firstChild) as Text, 1);
    inA.collapse(true);

    insertChipAt(host, host.querySelector('#b') as HTMLElement, inA, 'Party.Name', false);

    expect((host.querySelector('#a') as HTMLElement).textContent).toBe('one');
    expect((host.querySelector('#b') as HTMLElement).textContent).toBe('two{{Party.Name}}\u00a0');
  });

  it('FINS-15: with no cell it goes at the saved caret in the band, else at the end of the band', () => {
    const host = band('<div id="line">first line</div>');
    const text = (host.querySelector('#line') as HTMLElement).firstChild as Text;
    const caret = document.createRange();
    caret.setStart(text, 5);
    caret.collapse(true);

    insertChipAt(host, null, caret, 'Totals.GrandTotal', false);
    expect((host.querySelector('#line') as HTMLElement).textContent).toBe('first{{Totals.GrandTotal}}\u00a0 line');

    const empty = band('');
    insertChipAt(empty, null, null, 'Document.No', false);
    expect(empty.querySelectorAll('.pt-chip')).toHaveLength(1);
  });

  it('FINS-16: a caret inside a chip counts as no caret, so a chip is never split', () => {
    const host = band('<table><tr><td id="c"><span class="pt-chip" contenteditable="false">{{A.B}}</span>&nbsp;</td></tr></table>');
    const chipText = (host.querySelector('.pt-chip') as HTMLElement).firstChild as Text;
    const caret = document.createRange();
    caret.setStart(chipText, 2);
    caret.collapse(true);

    insertChipAt(host, host.querySelector('#c') as HTMLElement, caret, 'C.D', false);

    expect(Array.from(host.querySelectorAll('.pt-chip')).map((c) => c.textContent)).toEqual(['{{A.B}}', '{{C.D}}']);
  });

  it('FINS-17: returns a caret just after the chip it put in', () => {
    const host = band(ITEMS);
    const after = insertChipAt(host, cell('b2'), null, 'Item.Qty', true);

    expect(after.collapsed).toBe(true);
    expect(after.startContainer).toBe(cell('b2'));
    expect(after.startOffset).toBe(cell('b2').childNodes.length);
  });

  it('FINS-18: names a heading only when it is empty, and leaves one the person wrote', () => {
    band(ITEMS);

    expect(nameHeading(cell('h2'), { tag: 'Item.ItemName', description: 'Item name' })).toBe(true);
    expect(cell('h2').textContent).toBe('Item name');

    expect(nameHeading(cell('h1'), { tag: 'Item.SlNo', description: 'Serial no' })).toBe(false);
    expect(cell('h1').textContent).toBe('#');
  });

  it('FINS-19: opens the picker under the cell when there is room, touching it', () => {
    const at = pickerPlacement({ left: 100, top: 100, right: 300, bottom: 130 }, { width: 1280, height: 720 });

    expect(at).toEqual({ left: 100, top: 136, bottom: null, maxHeight: 380 });
  });

  it('FINS-20: opens it above the cell, hanging from the cell’s top edge, when the room is above', () => {
    const at = pickerPlacement({ left: 100, top: 600, right: 300, bottom: 630 }, { width: 1280, height: 720 });

    expect(at.top).toBeNull();
    expect(at.bottom).toBe(126);
    expect(at.maxHeight).toBe(380);
  });

  it('FINS-21: takes the side with more room when neither holds the whole picker, and is only as tall as it', () => {
    const upper = pickerPlacement({ left: 10, top: 40, right: 60, bottom: 70 }, { width: 1280, height: 400 });
    expect(upper).toEqual({ left: 10, top: 76, bottom: null, maxHeight: 316 });

    const lower = pickerPlacement({ left: 10, top: 300, right: 60, bottom: 330 }, { width: 1280, height: 400 });
    expect(lower.top).toBeNull();
    expect(lower.bottom).toBe(106);
    expect(lower.maxHeight).toBe(286);
  });

  it('FINS-22: never squeezes the picker below 160 px, even when both sides are small', () => {
    const at = pickerPlacement({ left: 10, top: 70, right: 60, bottom: 130 }, { width: 1280, height: 200 });

    expect(at.maxHeight).toBe(160);
  });

  it('FINS-23: keeps the picker inside the window’s width', () => {
    const far = pickerPlacement({ left: 1200, top: 100, right: 1250, bottom: 130 }, { width: 1280, height: 720 });
    expect(far.left).toBe(1280 - 440 - 8);

    const near = pickerPlacement({ left: -50, top: 100, right: 0, bottom: 130 }, { width: 1280, height: 720 });
    expect(near.left).toBe(8);
  });

  it('FINS-24: a box is in view when any of it is, and out of sight when it is wholly outside or only touches', () => {
    const view = { left: 100, top: 100, right: 500, bottom: 400 };

    expect(isInView({ left: 150, top: 150, right: 200, bottom: 180 }, view)).toBe(true);
    expect(isInView({ left: 450, top: 380, right: 600, bottom: 500 }, view)).toBe(true);
    expect(isInView({ left: 150, top: 20, right: 200, bottom: 99 }, view)).toBe(false);
    expect(isInView({ left: 150, top: 401, right: 200, bottom: 450 }, view)).toBe(false);
    expect(isInView({ left: 0, top: 150, right: 100, bottom: 180 }, view)).toBe(false);
    expect(isInView({ left: 500, top: 150, right: 560, bottom: 180 }, view)).toBe(false);
  });
});
