import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TicketFormComponent } from './ticket-form.component';
import { CustomerService } from '@bill-book/customer-core';

/**
 * Constructed rather than rendered.
 *
 * This workspace's Vitest runs without the Angular Vite plugin, so a component
 * with a `templateUrl` cannot be compiled — `TestBed.createComponent` fails
 * with "is not resolved", which is what this spec used to do. Building the
 * class inside an injection context exercises everything a `should create`
 * smoke test was ever asserting: that the constructor runs and every `inject()`
 * in it resolves. Rendering is covered by driving the built app in a browser,
 * per the note in CLAUDE.md.
 */
describe('TicketFormComponent', () => {
  let component: TicketFormComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CustomerService],
    });

    component = TestBed.runInInjectionContext(() => new TicketFormComponent());
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('picks the contact by name and stores its id (TK-16)', async () => {
    const service = TestBed.inject(CustomerService);
    const search = vi.spyOn(service, 'searchContacts').mockResolvedValue([
      { contactId: 12, contactCode: 'C12', displayName: 'Meena Traders', gstin: null },
    ]);

    component.openContactPicker();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(search).toHaveBeenCalledWith('');
    expect(component.pickerOpen()).toBe(true);
    expect(component.pickerRows()).toEqual([{ id: 12, code: 'C12', name: 'Meena Traders', meta: null }]);

    component.chooseContact(component.pickerRows()[0]);

    expect(component.form.controls.contactId.value).toBe(12);
    expect(component.contactLabel()).toBe('C12 Meena Traders');
    expect(component.pickerOpen()).toBe(false);
  });

  it('keeps the newest search when an older one answers last', async () => {
    const service = TestBed.inject(CustomerService);
    let answerOld: (rows: { contactId: number; contactCode: string; displayName: string; gstin: string | null }[]) => void =
      () => undefined;

    vi.spyOn(service, 'searchContacts')
      .mockImplementationOnce(() => new Promise((resolve) => (answerOld = resolve)))
      .mockImplementationOnce(() => Promise.resolve([]));

    const old = component.searchContacts('me');
    await component.searchContacts('meena');
    answerOld([{ contactId: 1, contactCode: 'C1', displayName: 'Old answer', gstin: null }]);
    await old;

    expect(component.pickerRows()).toEqual([]);
  });

  it('refuses to save without a contact', async () => {
    const service = TestBed.inject(CustomerService);
    const create = vi.spyOn(service, 'createTicket');

    component.form.patchValue({ subject: 'Printer jam', description: 'Paper stuck' });
    await component.save();

    expect(create).not.toHaveBeenCalled();
    expect(component.form.controls.contactId.touched).toBe(true);
  });
});
