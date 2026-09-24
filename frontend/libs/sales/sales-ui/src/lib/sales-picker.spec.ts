import { describe, expect, it, vi } from 'vitest';
import { SalesPicker } from './sales-picker';

/**
 * The picker every sales form shares: what it asks, what it hands the form, and
 * that an old search's late answer never replaces a newer one.
 */
describe('SalesPicker', () => {
  const customer = {
    contactId: 7,
    contactCode: 'C7',
    displayName: 'Ravi Stores',
    isCustomer: true,
    gstin: '33ABCDE1234F1Z5',
    currencyCode: 'INR',
    isActive: true,
  };

  const item = {
    itemId: 3,
    itemCode: 'SOAP-1',
    itemName: 'Sandal soap',
    inventoryUomCode: 'PCS',
    isActive: true,
  };

  const setup = () => {
    const lookups = {
      customers: vi.fn().mockResolvedValue([customer]),
      items: vi.fn().mockResolvedValue([item]),
    };
    const handlers = { customer: vi.fn(), item: vi.fn() };

    return { lookups, handlers, picker: new SalesPicker(lookups, handlers) };
  };

  it('opens on customers and maps them to rows with the GSTIN as meta', async () => {
    const { lookups, picker } = setup();

    await picker.openCustomer();

    expect(lookups.customers).toHaveBeenCalledWith('');
    expect(picker.open()).toBe(true);
    expect(picker.title()).toBe('Choose a customer');
    expect(picker.rows()).toEqual([{ id: 7, code: 'C7', name: 'Ravi Stores', meta: '33ABCDE1234F1Z5' }]);
    expect(picker.loading()).toBe(false);
  });

  it('hands a chosen item to the form with the line it was opened from, then closes', async () => {
    const { handlers, picker } = setup();

    await picker.openItem(2);
    picker.choose(picker.rows()[0]);

    expect(handlers.item).toHaveBeenCalledWith(2, { id: 3, code: 'SOAP-1', name: 'Sandal soap', meta: 'PCS' });
    expect(handlers.customer).not.toHaveBeenCalled();
    expect(picker.open()).toBe(false);
    expect(picker.rows()).toEqual([]);
  });

  it('keeps the newest search when an older one answers last', async () => {
    const { lookups, picker } = setup();
    let answerOld: (rows: unknown[]) => void = () => undefined;

    lookups.customers
      .mockImplementationOnce(() => Promise.resolve([customer]))
      .mockImplementationOnce(() => new Promise((resolve) => (answerOld = resolve)))
      .mockImplementationOnce(() => Promise.resolve([]));

    await picker.openCustomer();
    const old = picker.search('ra');
    await picker.search('ravi');
    answerOld([customer]);
    await old;

    expect(picker.rows()).toEqual([]);
  });

  it('a failed search shows no rows rather than the previous ones', async () => {
    const { lookups, picker } = setup();

    await picker.openCustomer();
    lookups.customers.mockRejectedValueOnce(new Error('offline'));
    await picker.search('x');

    expect(picker.rows()).toEqual([]);
    expect(picker.loading()).toBe(false);
  });

  it('labels a saved document by code and name, or by id when it has neither', () => {
    expect(SalesPicker.savedLabel('C7', 'Ravi Stores', 7)).toBe('C7 Ravi Stores');
    expect(SalesPicker.savedLabel(null, 'Ravi Stores', 7)).toBe('Ravi Stores');
    expect(SalesPicker.savedLabel(undefined, undefined, 7)).toBe('Customer 7');
    expect(SalesPicker.savedLabel(null, null, 0)).toBe('');
  });
});
