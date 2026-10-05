import { describe, expect, it, vi } from 'vitest';
import { SalesPicker } from './sales-picker';

/** A scan on a sales form (TK-161): straight onto the lines, or the picker on the code. */
describe('SalesPicker.scanItem', () => {
  const soap = { itemId: 3, itemCode: 'SOAP-1', itemName: 'Sandal soap', inventoryUomCode: 'PCS', isActive: true };
  const soapLarge = { itemId: 4, itemCode: 'SOAP-1L', itemName: 'Sandal soap large', inventoryUomCode: 'PCS', isActive: true };

  const setup = (items: unknown[]) => {
    const lookups = { customers: vi.fn().mockResolvedValue([]), items: vi.fn().mockResolvedValue(items) };
    const handlers = { customer: vi.fn(), item: vi.fn(), scanned: vi.fn() };
    return { lookups, handlers, picker: new SalesPicker(lookups, handlers) };
  };

  it('hands the item a barcode names to the form without opening the picker', async () => {
    const { lookups, handlers, picker } = setup([soap]);

    await picker.scanItem(' 8901030865278 ');

    expect(lookups.items).toHaveBeenCalledWith('8901030865278');
    expect(handlers.scanned).toHaveBeenCalledWith({ id: 3, code: 'SOAP-1', name: 'Sandal soap', meta: 'PCS' });
    expect(picker.open()).toBe(false);
  });

  it('takes the exact item code among several matches', async () => {
    const { handlers, picker } = setup([soapLarge, soap]);

    await picker.scanItem('soap-1');

    expect(handlers.scanned).toHaveBeenCalledWith(expect.objectContaining({ id: 3 }));
  });

  it('opens the item picker on a code that names no single item', async () => {
    const { handlers, picker } = setup([soap, soapLarge]);

    await picker.scanItem('SOAP');

    expect(handlers.scanned).not.toHaveBeenCalled();
    expect(picker.open()).toBe(true);
    expect(picker.kind()).toBe('item');
    expect(picker.term()).toBe('SOAP');
    expect(picker.rows()).toHaveLength(2);
  });

  it('adds the item chosen after an unknown scan as a scan would, not to a line', async () => {
    const { handlers, picker } = setup([]);

    await picker.scanItem('UNKNOWN');
    picker.choose({ id: 9, code: 'X9', name: 'Found by hand' });

    expect(handlers.scanned).toHaveBeenCalledWith({ id: 9, code: 'X9', name: 'Found by hand' });
    expect(handlers.item).not.toHaveBeenCalled();
    expect(picker.open()).toBe(false);
    expect(picker.term()).toBe('');
  });

  it('a picker opened from a line afterwards fills that line again', async () => {
    const { handlers, picker } = setup([]);

    await picker.scanItem('UNKNOWN');
    picker.close();
    await picker.openItem(2);
    picker.choose({ id: 9, code: 'X9', name: 'Found by hand' });

    expect(handlers.item).toHaveBeenCalledWith(2, { id: 9, code: 'X9', name: 'Found by hand' });
    expect(handlers.scanned).not.toHaveBeenCalled();
  });

  it('opens the picker on the code when the lookup fails', async () => {
    const { lookups, handlers, picker } = setup([]);
    lookups.items.mockRejectedValueOnce(new Error('offline'));

    await picker.scanItem('8901');

    expect(handlers.scanned).not.toHaveBeenCalled();
    expect(picker.open()).toBe(true);
  });

  it('does nothing on a form that takes no scans', async () => {
    const lookups = { customers: vi.fn(), items: vi.fn() };
    const picker = new SalesPicker(lookups, { customer: vi.fn(), item: vi.fn() });

    await picker.scanItem('8901');

    expect(lookups.items).not.toHaveBeenCalled();
  });
});
