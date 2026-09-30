import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TicketFormComponent } from './ticket-form.component';
import { CustomerService, Ticket, TicketChannel } from '@bill-book/customer-core';
import { SessionContext, SessionContextService } from '@bill-book/auth';

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
      providers: [provideHttpClient(), provideHttpClientTesting(), CustomerService, SessionContextService],
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

  it('omits Call channel for Standard/Basic plan and includes it for Pro/Elite (TK-129)', () => {
    const session = TestBed.inject(SessionContextService);

    // Default / Standard plan
    session.context.set({ planTier: 'Standard' } as unknown as SessionContext);
    expect(component.isProOrElite()).toBe(false);
    expect(component.channelOptions().map(o => o.value)).toEqual([
      TicketChannel.Web,
      TicketChannel.Email,
      TicketChannel.Chat,
    ]);

    // Pro plan
    session.context.set({ planTier: 'Pro' } as unknown as SessionContext);
    expect(component.isProOrElite()).toBe(true);
    expect(component.channelOptions().map(o => o.value)).toEqual([
      TicketChannel.Web,
      TicketChannel.Email,
      TicketChannel.Chat,
      TicketChannel.Call,
    ]);

    // Elite plan
    session.context.set({ planTier: 'Elite' } as unknown as SessionContext);
    expect(component.isProOrElite()).toBe(true);
    expect(component.channelOptions().map(o => o.value)).toContain(TicketChannel.Call);
  });

  it('blocks saving with Call channel on Standard plan and allows on Pro (TK-129)', async () => {
    const service = TestBed.inject(CustomerService);
    const session = TestBed.inject(SessionContextService);
    const create = vi.spyOn(service, 'createTicket').mockResolvedValue({} as unknown as Ticket);

    session.context.set({ planTier: 'Standard' } as unknown as SessionContext);
    component.form.patchValue({
      subject: 'Issue',
      description: 'Detail',
      contactId: 10,
      channel: TicketChannel.Call,
    });

    await component.save();
    expect(create).not.toHaveBeenCalled();
    expect(component.errorMessage()).toBe('Call support requires a Pro or Elite plan.');

    // Now switch to Pro
    session.context.set({ planTier: 'Pro' } as unknown as SessionContext);
    await component.save();
    expect(create).toHaveBeenCalledWith(expect.objectContaining({
      channel: TicketChannel.Call,
      contactId: 10,
    }));
  });
});
