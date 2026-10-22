import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlanLimit, PlanSummary } from '@bill-book/api-client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  ALREADY_ASKED,
  ASK_AN_ADMINISTRATOR,
  canAskForUpgrade,
  showsComparison,
  upgradeMessages,
  upgradeTarget,
  upgradeTargetName,
  upgradeTitle,
  UpgradeDialogState,
} from './upgrade-dialog.model';
import { UpgradeDialogService } from './upgrade-dialog.service';

const professional: PlanSummary = {
  plan: 'Professional',
  licenseType: 'Pro',
  maxUsers: 5,
  usersUsed: 5,
  monthlySalesTransactionLimit: 1000,
  monthlyPurchaseTransactionLimit: 1000,
  userLimitMessage: 'Professional plan user limit reached.',
  canUpgrade: true,
  upgradeRequested: false,
};

const usersLimit: PlanLimit = {
  message:
    'Professional plan user limit reached (maximum 5 users including inactive accounts). Upgrade to the Elite Plan to add more users.',
  code: 'PlanLimitReached',
  kind: 'Users',
  plan: 'Professional',
  limit: 5,
  used: 5,
};

const state = (change: Partial<UpgradeDialogState> = {}): UpgradeDialogState => ({
  options: {},
  summary: professional,
  busy: false,
  outcome: null,
  requested: false,
  resolve: () => undefined,
  ...change,
});

const settle = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

describe('Upgrade prompt, pure', () => {
  it('UPG-01: a limit gives the prompt the server\'s sentence first, in a warning', () => {
    const messages = upgradeMessages(state({ options: { limit: usersLimit } }));

    expect(messages).toEqual([{ tone: 'warning', text: usersLimit.message }]);
    expect(upgradeTitle({ limit: usersLimit })).toBe('Plan limit reached');
    expect(upgradeTitle({})).toBe('Upgrade to Elite');
  });

  it('UPG-02: says why there is nothing to press, and does not offer the request', () => {
    const noPermission = state({ options: { canRequest: false } });
    expect(upgradeMessages(noPermission)).toEqual([{ tone: 'info', text: ASK_AN_ADMINISTRATOR }]);
    expect(canAskForUpgrade(noPermission)).toBe(false);

    const waiting = state({ summary: { ...professional, upgradeRequested: true } });
    expect(upgradeMessages(waiting)).toEqual([{ tone: 'info', text: ALREADY_ASKED }]);
    expect(canAskForUpgrade(waiting)).toBe(false);

    const elite = state({ summary: { ...professional, plan: 'Elite', licenseType: 'Elite', canUpgrade: false } });
    expect(canAskForUpgrade(elite)).toBe(false);
  });

  it('UPG-03: offers the request when it is allowed, none is waiting and a higher plan exists', () => {
    expect(canAskForUpgrade(state())).toBe(true);

    // The plan could not be read: still offered, the server decides.
    expect(canAskForUpgrade(state({ summary: null }))).toBe(true);
    expect(canAskForUpgrade(state({ requested: true }))).toBe(false);
  });

  it('UPG-09: asks for Elite unless a trial asks for Professional, and names the plan it asks for', () => {
    expect(upgradeTarget({})).toBe('Elite');
    expect(upgradeTargetName({})).toBe('Elite');
    expect(upgradeTarget({ toPlan: 'Pro' })).toBe('Pro');
    expect(upgradeTargetName({ toPlan: 'Pro' })).toBe('Professional');

    expect(upgradeTitle({ toPlan: 'Pro' })).toBe('Upgrade to Professional');
    expect(upgradeTitle({ toPlan: 'Elite' })).toBe('Upgrade to Elite');

    // A limit's prompt keeps its own title whichever plan it asks for.
    expect(upgradeTitle({ limit: usersLimit, toPlan: 'Pro' })).toBe('Plan limit reached');
  });

  it('UPG-10: sets the plan against Elite only when Elite is what is asked for and the plan was read', () => {
    expect(showsComparison(state())).toBe(true);
    expect(showsComparison(state({ options: { toPlan: 'Elite' } }))).toBe(true);
    expect(showsComparison(state({ options: { toPlan: 'Pro' } }))).toBe(false);
    expect(showsComparison(state({ summary: null }))).toBe(false);
  });
});

describe('UpgradeDialogService', () => {
  let service: UpgradeDialogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(UpgradeDialogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Opens the prompt and answers the plan read; `closed` settles when the prompt is closed. */
  async function openWith(options = {}, summary: PlanSummary | null = professional): Promise<{ closed: Promise<void> }> {
    const closed = service.open(options);
    const request = http.expectOne('/api/plan');
    if (summary) {
      request.flush(summary);
    } else {
      request.flush({ message: 'down' }, { status: 503, statusText: 'Service Unavailable' });
    }
    await settle();
    return { closed };
  }

  it('UPG-04: opens with the plan once it has been read, and resolves when closed', async () => {
    const { closed } = await openWith({ limit: usersLimit });

    expect(service.state()?.summary?.maxUsers).toBe(5);
    expect(service.state()?.options.limit?.kind).toBe('Users');

    let resolved = false;
    void closed.then(() => (resolved = true));
    service.close();
    await settle();

    expect(service.state()).toBeNull();
    expect(resolved).toBe(true);
  });

  it('UPG-05: opens without the comparison when the plan cannot be read', async () => {
    await openWith({}, null);

    expect(service.state()).not.toBeNull();
    expect(service.state()?.summary).toBeNull();
  });

  it('UPG-06: asking again while it is up shows the same prompt and reads nothing twice', async () => {
    const first = service.open({});
    const second = service.open({});
    expect(second).toBe(first);

    http.expectOne('/api/plan').flush(professional);
    await settle();
    http.expectNone('/api/plan');
    service.close();
    await first;
  });

  it('UPG-07: sends the limit that sent them, with their note, and keeps the prompt up to say it was sent', async () => {
    await openWith({ limit: usersLimit });

    const sending = service.request('  A second counter  ');
    const request = http.expectOne('/api/plan/upgrade-requests');
    expect(request.request.body).toEqual({ toPlan: 'Elite', reason: 'Users', note: 'A second counter' });
    expect(service.state()?.busy).toBe(true);
    request.flush({ message: 'Your request has been sent. We will contact you to complete the upgrade.' });
    await sending;

    expect(service.state()?.requested).toBe(true);
    expect(service.state()?.outcome).toEqual({
      tone: 'success',
      text: 'Your request has been sent. We will contact you to complete the upgrade.',
    });
    expect(canAskForUpgrade(service.state() as UpgradeDialogState)).toBe(false);

    // A second press after it went through sends nothing.
    await service.request('');
    http.expectNone('/api/plan/upgrade-requests');
  });

  it('UPG-08: a refused request is shown in the server\'s words and can be sent again', async () => {
    await openWith({});

    const sending = service.request('');
    const request = http.expectOne('/api/plan/upgrade-requests');
    expect(request.request.body).toEqual({ toPlan: 'Elite', reason: undefined, note: undefined });
    request.flush({ message: 'Your plan cannot be moved to that one.' }, { status: 409, statusText: 'Conflict' });
    await sending;

    expect(service.state()?.requested).toBe(false);
    expect(service.state()?.busy).toBe(false);
    expect(service.state()?.outcome).toEqual({ tone: 'error', text: 'Your plan cannot be moved to that one.', detail: [] });
    expect(canAskForUpgrade(service.state() as UpgradeDialogState)).toBe(true);
  });

  it('UPG-11: a trial asking from the Plan screen for Professional sends that plan', async () => {
    await openWith({ toPlan: 'Pro' }, { ...professional, plan: 'Trial', licenseType: 'Trial' });

    const sending = service.request('');
    const request = http.expectOne('/api/plan/upgrade-requests');
    expect(request.request.body).toEqual({ toPlan: 'Pro', reason: undefined, note: undefined });
    request.flush({ message: 'Your request has been sent. We will contact you to complete the upgrade.' });
    await sending;

    expect(service.state()?.requested).toBe(true);
  });
});
