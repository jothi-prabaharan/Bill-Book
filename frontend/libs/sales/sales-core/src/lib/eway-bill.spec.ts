import { describe, expect, it } from 'vitest';
import { ewayBillCancellable, ewayBillLive, EwayBillView } from './eway-bill.service';

const bill = (patch: Partial<EwayBillView> = {}): EwayBillView => ({
  ewayBillId: 1,
  origin: 'Standalone',
  status: 'Generated',
  ewbNo: '331000000001',
  ewbDate: '2026-09-20T10:00:00Z',
  transportMode: 'Road',
  distanceKm: 450,
  ...patch,
});

describe('e-way bills (TK-93)', () => {
  it('is live while generated or pending, and not once cancelled', () => {
    expect(ewayBillLive(bill())).toBe(true);
    expect(ewayBillLive(bill({ status: 'Pending' }))).toBe(true);
    expect(ewayBillLive(bill({ status: 'Cancelled' }))).toBe(false);
    expect(ewayBillLive(null)).toBe(false);
  });

  it('a generated bill cancels within 24 hours and not after', () => {
    expect(ewayBillCancellable(bill(), new Date('2026-09-21T09:00:00Z'))).toBe(true);
    expect(ewayBillCancellable(bill(), new Date('2026-09-21T11:00:00Z'))).toBe(false);
  });

  it('a typed bill can always be cancelled, since the portal never had it', () => {
    expect(ewayBillCancellable(bill({ origin: 'Manual' }), new Date('2027-01-01T00:00:00Z'))).toBe(true);
  });
});
