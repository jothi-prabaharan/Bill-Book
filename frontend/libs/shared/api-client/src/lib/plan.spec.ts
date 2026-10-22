import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { comparePlans, PlanLimit, planLimitOf, PlanSummary, seatBanner, seatsFull, userLimitOf } from './plan.model';
import { PlanService } from './plan.service';

const professional: PlanSummary = {
  plan: 'Professional',
  licenseType: 'Pro',
  maxUsers: 5,
  usersUsed: 4,
  monthlySalesTransactionLimit: 1000,
  monthlyPurchaseTransactionLimit: 1000,
  userLimitMessage: null,
  canUpgrade: true,
  upgradeRequested: false,
};

const elite: PlanSummary = {
  ...professional,
  plan: 'Elite',
  licenseType: 'Elite',
  maxUsers: null,
  monthlySalesTransactionLimit: null,
  monthlyPurchaseTransactionLimit: null,
  canUpgrade: false,
};

describe('Plan limits', () => {
  it('PLANUI-01: says how many places are used, deactivated people included', () => {
    expect(seatBanner(professional)).toEqual({
      tone: 'info',
      text: '4 of 5 User Slots Used (Includes Deactivated Users)',
    });
  });

  it('PLANUI-02: warns once the last place is taken, and the words are the owner\'s', () => {
    const full = { ...professional, usersUsed: 5 };

    expect(seatsFull(full)).toBe(true);
    expect(seatBanner(full)).toEqual({
      tone: 'warning',
      text: '5 of 5 User Slots Used (Includes Deactivated Users)',
    });
  });

  it('PLANUI-03: a plan with no user limit has no banner and is never full', () => {
    expect(seatBanner(elite)).toBeNull();
    expect(seatsFull({ ...elite, usersUsed: 400 })).toBe(false);
  });

  it('PLANUI-04: compares what the plan allows with Elite, row by row', () => {
    expect(comparePlans(professional)).toEqual([
      { label: 'Users in a branch', current: '5', elite: 'Unlimited' },
      { label: 'Sales documents a month', current: '1,000', elite: 'Unlimited' },
      { label: 'Purchase documents a month', current: '1,000', elite: 'Unlimited' },
    ]);
  });

  it('PLANUI-08: a full branch is at a limit, in the shape a refusal would have carried it', () => {
    const message = 'Professional plan user limit reached (maximum 5 users including inactive accounts). Upgrade to the Elite Plan to add more users.';

    expect(userLimitOf({ ...professional, usersUsed: 5, userLimitMessage: message })).toEqual({
      message,
      code: 'PlanLimitReached',
      kind: 'Users',
      plan: 'Professional',
      limit: 5,
      used: 5,
    });

    // A place left, or no limit at all: not at one.
    expect(userLimitOf(professional)).toBeNull();
    expect(userLimitOf(elite)).toBeNull();
  });

  it('PLANUI-05: a 409 opens the upgrade prompt only when it is a plan limit', () => {
    const limit: PlanLimit = {
      message: 'Professional plan user limit reached (maximum 5 users including inactive accounts). Upgrade to the Elite Plan to add more users.',
      code: 'PlanLimitReached',
      kind: 'Users',
      plan: 'Professional',
      limit: 5,
      used: 5,
    };

    expect(planLimitOf(new HttpErrorResponse({ status: 409, error: limit }))).toEqual(limit);

    // Another conflict, another status, a bug in the page: none of them is an upgrade prompt.
    expect(planLimitOf(new HttpErrorResponse({ status: 409, error: { message: 'Already a member.' } }))).toBeNull();
    expect(planLimitOf(new HttpErrorResponse({ status: 400, error: limit }))).toBeNull();
    expect(planLimitOf(new HttpErrorResponse({ status: 409, error: null }))).toBeNull();
    expect(planLimitOf(new Error('boom'))).toBeNull();
  });
});

describe('PlanService', () => {
  let service: PlanService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PlanService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('PLANUI-06: reads the plan from the signed-in customer, with no id to point elsewhere', async () => {
    const pending = service.summary();
    http.expectOne('/api/plan').flush(professional);
    expect((await pending).maxUsers).toBe(5);
  });

  it('PLANUI-07: asks for a higher plan and returns the server\'s words', async () => {
    const pending = service.requestUpgrade({ toPlan: 'Elite', reason: 'Users', note: 'A second counter' });
    const request = http.expectOne('/api/plan/upgrade-requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ toPlan: 'Elite', reason: 'Users', note: 'A second counter' });
    request.flush({ message: 'Your request has been sent. We will contact you to complete the upgrade.' });
    expect(await pending).toBe('Your request has been sent. We will contact you to complete the upgrade.');
  });
});
