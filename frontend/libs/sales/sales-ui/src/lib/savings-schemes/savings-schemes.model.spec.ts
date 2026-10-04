import { describe, expect, it } from 'vitest';
import { actionsFor, bonusOf, emptyScheme, redeemableNow, schemeProblem, schemeRequest, type SchemeEnrolment } from './savings-schemes.model';

const enrolment = (over: Partial<SchemeEnrolment>): SchemeEnrolment => ({
  schemeEnrolmentId: 1,
  enrolmentNo: 'GOLD-00001',
  savingsSchemeId: 1,
  schemeName: 'Gold 11',
  contactId: 7,
  startDate: '2026-01-01',
  maturityDate: '2026-12-01',
  instalmentAmount: 1000,
  months: 11,
  instalmentsPaid: 0,
  paidPrincipal: 0,
  bonusAmount: 1000,
  nextDueDate: '2026-01-01',
  status: 'Active',
  ...over,
});

describe('savings schemes (TK-141)', () => {
  it('works the bonus out by the same rule as the server', () => {
    expect(bonusOf('Instalments', 1, 1000, 11)).toBe(1000);
    expect(bonusOf('Percent', 5, 1000, 11)).toBe(550);
    expect(bonusOf('FixedAmount', 750, 1000, 11)).toBe(750);
    expect(bonusOf('None', 9, 1000, 11)).toBe(0);
  });

  it('refuses a scheme with no instalment or an impossible bonus', () => {
    const d = { ...emptyScheme(), code: 'g', name: 'Gold' };
    expect(schemeProblem(d)).toBe('Enter the monthly instalment.');

    d.instalmentAmount = 1000;
    d.bonusType = 'Percent';
    d.bonusValue = 120;
    expect(schemeProblem(d)).toBe('A percentage bonus cannot exceed 100.');

    d.bonusValue = 5;
    expect(schemeProblem(d)).toBeNull();
    expect(schemeRequest(d)).toMatchObject({ code: 'G', bonusType: 'Percent', bonusValue: 5 });
  });

  it('adds the bonus only once matured', () => {
    expect(redeemableNow(enrolment({ status: 'Matured', paidPrincipal: 11000, principalAvailable: 11000 }))).toBe(12000);
    expect(redeemableNow(enrolment({ status: 'Lapsed', paidPrincipal: 3000, principalAvailable: 3000 }))).toBe(3000);
  });

  it('offers only the actions a status allows', () => {
    expect(actionsFor(enrolment({ status: 'Active' }))).toEqual({ pay: true, redeem: false, closeEarly: false, refund: false });
    expect(actionsFor(enrolment({ status: 'Active', instalmentsPaid: 2, paidPrincipal: 2000 }))).toMatchObject({ closeEarly: true, refund: true });
    expect(actionsFor(enrolment({ status: 'Matured', paidPrincipal: 11000 }))).toMatchObject({ pay: false, redeem: true });
    expect(actionsFor(enrolment({ status: 'Redeemed', paidPrincipal: 11000, principalAvailable: 0 }))).toEqual({ pay: false, redeem: false, closeEarly: false, refund: false });
  });
});
